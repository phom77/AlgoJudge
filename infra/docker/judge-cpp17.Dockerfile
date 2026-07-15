FROM gcc:14

RUN useradd --create-home --uid 10001 judge
WORKDIR /sandbox
USER judge

# The worker supplies the compile or run command. Pin this base image by digest
# before a staging or production deployment.
