FROM gcc:14.3.0-bookworm@sha256:5e927c284bf55a7dc796262e311a0703344f62f41f5621eb56843111b1d37e15 AS runner-build

COPY judge-runner.c /tmp/judge-runner.c
RUN gcc -O2 -std=c17 -Wall -Wextra -Werror \
    /tmp/judge-runner.c \
    -o /usr/local/bin/algojudge-runner

FROM gcc:14.3.0-bookworm@sha256:5e927c284bf55a7dc796262e311a0703344f62f41f5621eb56843111b1d37e15

COPY --from=runner-build /usr/local/bin/algojudge-runner /usr/local/bin/algojudge-runner
RUN groupadd --gid 10001 judge \
    && useradd --no-create-home --uid 10001 --gid 10001 --shell /usr/sbin/nologin judge \
    && mkdir -p /artifact \
    && touch /artifact/solution \
    && chown -R 10001:10001 /artifact

WORKDIR /artifact
USER 10001:10001
