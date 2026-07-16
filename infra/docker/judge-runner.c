#define _GNU_SOURCE

#include <errno.h>
#include <fcntl.h>
#include <poll.h>
#include <signal.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/resource.h>
#include <sys/types.h>
#include <sys/wait.h>
#include <time.h>
#include <unistd.h>

#define PROTOCOL_NAME "ALGOJUDGE_RESULT_V1"
#define POLL_INTERVAL_MS 5

typedef struct
{
    unsigned char *data;
    size_t length;
    size_t limit;
    bool exceeded;
} bounded_buffer;

static void fail(const char *message)
{
    fprintf(stderr, "judge-runner: %s\n", message);
    exit(125);
}

static long long parse_positive_number(const char *value, const char *name)
{
    char *end = NULL;
    errno = 0;
    const long long parsed = strtoll(value, &end, 10);
    if (errno != 0 || end == value || *end != '\0' || parsed <= 0)
    {
        fprintf(stderr, "judge-runner: invalid %s\n", name);
        exit(125);
    }

    return parsed;
}

static long long elapsed_microseconds(
    const struct timespec *start,
    const struct timespec *end)
{
    const long long seconds = end->tv_sec - start->tv_sec;
    const long long nanoseconds = end->tv_nsec - start->tv_nsec;
    return seconds * 1000000LL + nanoseconds / 1000LL;
}

static void make_nonblocking(int descriptor)
{
    const int flags = fcntl(descriptor, F_GETFL, 0);
    if (flags < 0 || fcntl(descriptor, F_SETFL, flags | O_NONBLOCK) < 0)
        fail("could not configure an output pipe");
}

static void append_output(int descriptor, bounded_buffer *buffer, bool *closed)
{
    unsigned char chunk[8192];

    while (true)
    {
        const ssize_t read_count = read(descriptor, chunk, sizeof(chunk));
        if (read_count > 0)
        {
            const size_t available = buffer->limit - buffer->length;
            const size_t copy_count = (size_t)read_count < available
                ? (size_t)read_count
                : available;

            if (copy_count > 0)
            {
                memcpy(buffer->data + buffer->length, chunk, copy_count);
                buffer->length += copy_count;
            }

            if ((size_t)read_count > copy_count)
                buffer->exceeded = true;

            continue;
        }

        if (read_count == 0)
        {
            *closed = true;
            close(descriptor);
            return;
        }

        if (errno == EAGAIN || errno == EWOULDBLOCK)
            return;

        if (errno == EINTR)
            continue;

        *closed = true;
        close(descriptor);
        return;
    }
}

static long long read_named_counter(const char *path, const char *counter_name)
{
    FILE *file = fopen(path, "r");
    if (file == NULL)
        return -1;

    char name[128];
    long long value = 0;
    long long result = -1;
    while (fscanf(file, "%127s %lld", name, &value) == 2)
    {
        if (strcmp(name, counter_name) == 0)
        {
            result = value;
            break;
        }
    }

    fclose(file);
    return result;
}

static long long read_single_counter(const char *path)
{
    FILE *file = fopen(path, "r");
    if (file == NULL)
        return -1;

    long long value = -1;
    if (fscanf(file, "%lld", &value) != 1)
        value = -1;

    fclose(file);
    return value;
}

static long long read_oom_counter(void)
{
    const long long cgroup_v2 = read_named_counter(
        "/sys/fs/cgroup/memory.events",
        "oom_kill");
    if (cgroup_v2 >= 0)
        return cgroup_v2;

    return read_single_counter(
        "/sys/fs/cgroup/memory/memory.failcnt");
}

static void kill_process_group(pid_t child)
{
    if (kill(-child, SIGKILL) < 0 && errno != ESRCH)
        fprintf(stderr, "judge-runner: could not kill the solution process group\n");
}

static void emit_result(
    const char *status,
    long long elapsed_us,
    long long memory_bytes,
    const bounded_buffer *stdout_buffer,
    const bounded_buffer *stderr_buffer)
{
    printf(
        PROTOCOL_NAME "\n"
        "status=%s\n"
        "elapsed_us=%lld\n"
        "memory_bytes=%lld\n"
        "stdout_length=%zu\n"
        "stderr_length=%zu\n"
        "\n",
        status,
        elapsed_us,
        memory_bytes,
        stdout_buffer->length,
        stderr_buffer->length);

    if (stdout_buffer->length > 0)
        fwrite(stdout_buffer->data, 1, stdout_buffer->length, stdout);
    if (stderr_buffer->length > 0)
        fwrite(stderr_buffer->data, 1, stderr_buffer->length, stdout);

    fflush(stdout);
}

int main(int argc, char **argv)
{
    long long time_limit_ms = 0;
    long long stdout_limit = 0;
    long long stderr_limit = 0;
    int executable_index = -1;

    for (int index = 1; index < argc; index++)
    {
        if (strcmp(argv[index], "--") == 0)
        {
            executable_index = index + 1;
            break;
        }

        if (index + 1 >= argc)
            fail("missing option value");

        if (strcmp(argv[index], "--time-limit-ms") == 0)
            time_limit_ms = parse_positive_number(argv[++index], "time limit");
        else if (strcmp(argv[index], "--stdout-limit-bytes") == 0)
            stdout_limit = parse_positive_number(argv[++index], "stdout limit");
        else if (strcmp(argv[index], "--stderr-limit-bytes") == 0)
            stderr_limit = parse_positive_number(argv[++index], "stderr limit");
        else
            fail("unknown option");
    }

    if (time_limit_ms <= 0 || stdout_limit <= 0 || stderr_limit <= 0)
        fail("required limits were not supplied");
    if (executable_index < 0 || executable_index >= argc)
        fail("solution executable was not supplied");

    bounded_buffer stdout_buffer = {
        .data = malloc((size_t)stdout_limit),
        .length = 0,
        .limit = (size_t)stdout_limit,
        .exceeded = false
    };
    bounded_buffer stderr_buffer = {
        .data = malloc((size_t)stderr_limit),
        .length = 0,
        .limit = (size_t)stderr_limit,
        .exceeded = false
    };
    if (stdout_buffer.data == NULL || stderr_buffer.data == NULL)
        fail("could not allocate bounded output buffers");

    int stdout_pipe[2];
    int stderr_pipe[2];
    if (pipe(stdout_pipe) < 0 || pipe(stderr_pipe) < 0)
        fail("could not create output pipes");

    const long long oom_count_before = read_oom_counter();
    struct timespec started_at;
    if (clock_gettime(CLOCK_MONOTONIC, &started_at) < 0)
        fail("could not start the monotonic clock");

    const pid_t child = fork();
    if (child < 0)
        fail("could not create the solution process");

    if (child == 0)
    {
        setpgid(0, 0);

        struct rlimit core_limit = { .rlim_cur = 0, .rlim_max = 0 };
        setrlimit(RLIMIT_CORE, &core_limit);

        close(stdout_pipe[0]);
        close(stderr_pipe[0]);
        if (dup2(stdout_pipe[1], STDOUT_FILENO) < 0 ||
            dup2(stderr_pipe[1], STDERR_FILENO) < 0)
        {
            _exit(126);
        }

        close(stdout_pipe[1]);
        close(stderr_pipe[1]);
        execv(argv[executable_index], &argv[executable_index]);
        dprintf(STDERR_FILENO, "could not execute solution: %s\n", strerror(errno));
        _exit(127);
    }

    setpgid(child, child);
    close(stdout_pipe[1]);
    close(stderr_pipe[1]);
    make_nonblocking(stdout_pipe[0]);
    make_nonblocking(stderr_pipe[0]);

    bool stdout_closed = false;
    bool stderr_closed = false;
    bool child_exited = false;
    bool timed_out = false;
    bool kill_sent = false;
    int child_status = 0;
    struct rusage usage;
    memset(&usage, 0, sizeof(usage));

    while (!child_exited)
    {
        if (!stdout_closed)
            append_output(stdout_pipe[0], &stdout_buffer, &stdout_closed);
        if (!stderr_closed)
            append_output(stderr_pipe[0], &stderr_buffer, &stderr_closed);

        struct timespec current_time;
        clock_gettime(CLOCK_MONOTONIC, &current_time);
        const long long current_elapsed_us = elapsed_microseconds(
            &started_at,
            &current_time);

        if (!timed_out && current_elapsed_us >= time_limit_ms * 1000LL)
            timed_out = true;

        if (!kill_sent &&
            (timed_out || stdout_buffer.exceeded || stderr_buffer.exceeded))
        {
            kill_process_group(child);
            kill_sent = true;
        }

        const pid_t waited = wait4(child, &child_status, WNOHANG, &usage);
        if (waited == child)
        {
            child_exited = true;
            kill_process_group(child);
            break;
        }
        if (waited < 0 && errno != EINTR)
            fail("could not wait for the solution process");

        struct pollfd descriptors[2] = {
            { .fd = stdout_closed ? -1 : stdout_pipe[0], .events = POLLIN },
            { .fd = stderr_closed ? -1 : stderr_pipe[0], .events = POLLIN }
        };
        poll(descriptors, 2, POLL_INTERVAL_MS);
    }

    if (!stdout_closed)
        append_output(stdout_pipe[0], &stdout_buffer, &stdout_closed);
    if (!stderr_closed)
        append_output(stderr_pipe[0], &stderr_buffer, &stderr_closed);
    if (!stdout_closed)
        close(stdout_pipe[0]);
    if (!stderr_closed)
        close(stderr_pipe[0]);

    struct timespec finished_at;
    clock_gettime(CLOCK_MONOTONIC, &finished_at);
    const long long total_elapsed_us = elapsed_microseconds(
        &started_at,
        &finished_at);
    const long long memory_bytes = (long long)usage.ru_maxrss * 1024LL;
    const long long oom_count_after = read_oom_counter();
    const bool memory_exceeded =
        oom_count_before >= 0 &&
        oom_count_after > oom_count_before;

    const char *result_status = "runtime_error";
    if (timed_out)
        result_status = "time_limit_exceeded";
    else if (memory_exceeded)
        result_status = "memory_limit_exceeded";
    else if (stdout_buffer.exceeded || stderr_buffer.exceeded)
        result_status = "output_limit_exceeded";
    else if (WIFEXITED(child_status) && WEXITSTATUS(child_status) == 0)
        result_status = "success";

    emit_result(
        result_status,
        total_elapsed_us,
        memory_bytes,
        &stdout_buffer,
        &stderr_buffer);

    free(stdout_buffer.data);
    free(stderr_buffer.data);
    return 0;
}
