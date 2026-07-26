#define _GNU_SOURCE

#include <errno.h>
#include <fcntl.h>
#include <poll.h>
#include <signal.h>
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/types.h>
#include <sys/wait.h>
#include <unistd.h>

#define INPUT_PROTOCOL_NAME "ALGOJUDGE_BATCH_INPUT_V1"
#define RESULT_PROTOCOL_NAME "ALGOJUDGE_RESULT_V1\n"
#define MAX_CASE_COUNT 5000
#define HEADER_CAPTURE_BYTES 4096
#define IO_CHUNK_BYTES 8192

typedef struct
{
    unsigned char data[HEADER_CAPTURE_BYTES];
    size_t length;
} header_capture;

static void fail(const char *message)
{
    fprintf(stderr, "judge-batch-runner: %s\n", message);
    exit(125);
}

static void make_nonblocking(int descriptor)
{
    const int flags = fcntl(descriptor, F_GETFL, 0);
    if (flags < 0 || fcntl(descriptor, F_SETFL, flags | O_NONBLOCK) < 0)
        fail("could not configure a pipe");
}

static void read_line(char *buffer, size_t capacity)
{
    if (fgets(buffer, (int)capacity, stdin) == NULL)
        fail("batch input ended before its framing was complete");

    const size_t length = strlen(buffer);
    if (length == 0 || buffer[length - 1] != '\n')
        fail("batch input contained an oversized framing line");
    buffer[length - 1] = '\0';
}

static size_t parse_size_field(
    const char *line,
    const char *prefix,
    size_t maximum,
    const char *name)
{
    const size_t prefix_length = strlen(prefix);
    if (strncmp(line, prefix, prefix_length) != 0)
        fail("batch input contained an unexpected framing field");

    const char *value = line + prefix_length;
    char *end = NULL;
    errno = 0;
    const unsigned long long parsed = strtoull(value, &end, 10);
    if (errno != 0 || end == value || *end != '\0' || parsed > maximum)
    {
        fprintf(stderr, "judge-batch-runner: invalid %s\n", name);
        exit(125);
    }

    return (size_t)parsed;
}

static unsigned char *read_case_input(size_t *input_length)
{
    char line[128];
    read_line(line, sizeof(line));
    *input_length = parse_size_field(
        line,
        "input_length=",
        SIZE_MAX,
        "input length");
    read_line(line, sizeof(line));
    if (line[0] != '\0')
        fail("batch testcase header was not terminated");

    unsigned char *input = malloc(*input_length == 0 ? 1 : *input_length);
    if (input == NULL)
        fail("could not allocate testcase input");
    if (*input_length > 0 &&
        fread(input, 1, *input_length, stdin) != *input_length)
    {
        free(input);
        fail("batch testcase payload was truncated");
    }

    return input;
}

static void capture_header(
    header_capture *capture,
    const unsigned char *bytes,
    size_t length)
{
    const size_t available = sizeof(capture->data) - capture->length;
    const size_t copy_count = length < available ? length : available;
    if (copy_count > 0)
    {
        memcpy(capture->data + capture->length, bytes, copy_count);
        capture->length += copy_count;
    }
}

static bool header_reports_success(const header_capture *capture)
{
    const char success[] = "\nstatus=success\n";
    if (capture->length < sizeof(RESULT_PROTOCOL_NAME) - 1 ||
        memcmp(
            capture->data,
            RESULT_PROTOCOL_NAME,
            sizeof(RESULT_PROTOCOL_NAME) - 1) != 0)
    {
        fail("single-case runner returned an invalid protocol");
    }

    for (size_t index = 0;
         index + sizeof(success) - 1 <= capture->length;
         index++)
    {
        if (memcmp(capture->data + index, success, sizeof(success) - 1) == 0)
            return true;
    }

    return false;
}

static void close_descriptor(int *descriptor)
{
    if (*descriptor >= 0)
    {
        close(*descriptor);
        *descriptor = -1;
    }
}

static bool run_case(
    int argc,
    char **argv,
    const unsigned char *input,
    size_t input_length)
{
    int input_pipe[2];
    int output_pipe[2];
    int error_pipe[2];
    if (pipe(input_pipe) < 0 || pipe(output_pipe) < 0 || pipe(error_pipe) < 0)
        fail("could not create runner pipes");

    const pid_t child = fork();
    if (child < 0)
        fail("could not create the single-case runner process");

    if (child == 0)
    {
        close(input_pipe[1]);
        close(output_pipe[0]);
        close(error_pipe[0]);
        if (dup2(input_pipe[0], STDIN_FILENO) < 0 ||
            dup2(output_pipe[1], STDOUT_FILENO) < 0 ||
            dup2(error_pipe[1], STDERR_FILENO) < 0)
        {
            _exit(126);
        }

        close(input_pipe[0]);
        close(output_pipe[1]);
        close(error_pipe[1]);

        char **runner_arguments = calloc((size_t)argc + 1, sizeof(char *));
        if (runner_arguments == NULL)
            _exit(126);
        runner_arguments[0] = "/usr/local/bin/algojudge-runner";
        for (int index = 1; index < argc; index++)
            runner_arguments[index] = argv[index];
        runner_arguments[argc] = NULL;
        execv(runner_arguments[0], runner_arguments);
        _exit(127);
    }

    close(input_pipe[0]);
    close(output_pipe[1]);
    close(error_pipe[1]);
    make_nonblocking(input_pipe[1]);
    make_nonblocking(output_pipe[0]);
    make_nonblocking(error_pipe[0]);

    int input_descriptor = input_pipe[1];
    int output_descriptor = output_pipe[0];
    int error_descriptor = error_pipe[0];
    size_t input_offset = 0;
    bool child_exited = false;
    int child_status = 0;
    header_capture capture = { .length = 0 };

    while (!child_exited ||
           input_descriptor >= 0 ||
           output_descriptor >= 0 ||
           error_descriptor >= 0)
    {
        if (input_descriptor >= 0)
        {
            if (input_offset == input_length)
            {
                close_descriptor(&input_descriptor);
            }
            else
            {
                const ssize_t written = write(
                    input_descriptor,
                    input + input_offset,
                    input_length - input_offset);
                if (written > 0)
                    input_offset += (size_t)written;
                else if (written < 0 && errno == EPIPE)
                    close_descriptor(&input_descriptor);
                else if (written < 0 &&
                         errno != EAGAIN &&
                         errno != EWOULDBLOCK &&
                         errno != EINTR)
                    fail("could not write testcase input");
            }
        }

        unsigned char chunk[IO_CHUNK_BYTES];
        if (output_descriptor >= 0)
        {
            const ssize_t read_count = read(
                output_descriptor,
                chunk,
                sizeof(chunk));
            if (read_count > 0)
            {
                capture_header(&capture, chunk, (size_t)read_count);
                if (fwrite(chunk, 1, (size_t)read_count, stdout) !=
                    (size_t)read_count)
                    fail("could not emit a testcase result");
            }
            else if (read_count == 0)
                close_descriptor(&output_descriptor);
            else if (errno != EAGAIN && errno != EWOULDBLOCK && errno != EINTR)
                fail("could not read a testcase result");
        }

        if (error_descriptor >= 0)
        {
            const ssize_t read_count = read(
                error_descriptor,
                chunk,
                sizeof(chunk));
            if (read_count > 0)
            {
                if (fwrite(chunk, 1, (size_t)read_count, stderr) !=
                    (size_t)read_count)
                    fail("could not emit runner diagnostics");
            }
            else if (read_count == 0)
                close_descriptor(&error_descriptor);
            else if (errno != EAGAIN && errno != EWOULDBLOCK && errno != EINTR)
                fail("could not read runner diagnostics");
        }

        if (!child_exited)
        {
            const pid_t waited = waitpid(child, &child_status, WNOHANG);
            if (waited == child)
                child_exited = true;
            else if (waited < 0 && errno != EINTR)
                fail("could not wait for the single-case runner");
        }

        if (!child_exited ||
            input_descriptor >= 0 ||
            output_descriptor >= 0 ||
            error_descriptor >= 0)
        {
            struct pollfd descriptors[3] = {
                {
                    .fd = input_descriptor,
                    .events = input_descriptor >= 0 ? POLLOUT : 0
                },
                {
                    .fd = output_descriptor,
                    .events = output_descriptor >= 0 ? POLLIN : 0
                },
                {
                    .fd = error_descriptor,
                    .events = error_descriptor >= 0 ? POLLIN : 0
                }
            };
            poll(descriptors, 3, 5);
        }
    }

    fflush(stdout);
    fflush(stderr);
    if (!WIFEXITED(child_status) || WEXITSTATUS(child_status) != 0)
        fail("single-case runner failed");

    return header_reports_success(&capture);
}

int main(int argc, char **argv)
{
    signal(SIGPIPE, SIG_IGN);
    setvbuf(stdout, NULL, _IONBF, 0);
    setvbuf(stderr, NULL, _IONBF, 0);

    bool continue_after_failure = false;
    char **runner_argv = calloc((size_t)argc + 1, sizeof(char *));
    if (runner_argv == NULL)
        fail("could not allocate runner arguments");
    int runner_argc = 1;
    runner_argv[0] = argv[0];
    for (int index = 1; index < argc; index++)
    {
        if (strcmp(argv[index], "--continue-after-failure") == 0)
            continue_after_failure = true;
        else
            runner_argv[runner_argc++] = argv[index];
    }

    char line[128];
    read_line(line, sizeof(line));
    if (strcmp(line, INPUT_PROTOCOL_NAME) != 0)
        fail("batch input protocol name was invalid");
    read_line(line, sizeof(line));
    const size_t case_count = parse_size_field(
        line,
        "case_count=",
        MAX_CASE_COUNT,
        "case count");
    if (case_count == 0)
        fail("batch input contained no testcases");
    read_line(line, sizeof(line));
    if (line[0] != '\0')
        fail("batch header was not terminated");

    for (size_t index = 0; index < case_count; index++)
    {
        size_t input_length = 0;
        unsigned char *input = read_case_input(&input_length);
        const bool success = run_case(
            runner_argc,
            runner_argv,
            input,
            input_length);
        free(input);
        if (!success && !continue_after_failure)
            break;
    }

    free(runner_argv);
    return 0;
}
