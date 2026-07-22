FROM mcr.microsoft.com/dotnet/sdk:10.0.203-noble-amd64@sha256:6d7f69bc7bc9d4510ca255977b1f53ce52a79307e048a91450b2aecd63627cc3

RUN mkdir -p /workspace /artifact /sdk \
    && chown -R 10001:10001 /workspace /artifact /sdk

WORKDIR /workspace
USER 10001:10001
