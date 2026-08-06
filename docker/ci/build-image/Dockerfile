# Build container — datactive-build
# Job container olarak kullanılır (build.yml, runner-test.yml)
# İçerik: git + curl + buildkit (buildkitd + buildctl) — daemon'sız image build için

FROM ubuntu:24.04

RUN apt-get update && apt-get install -y --no-install-recommends \
    git \
    curl \
    ca-certificates \
    bash \
    tar \
    gzip \
    && rm -rf /var/lib/apt/lists/*

# Buildkit resmi binary (linux-arm64) — sürüm pinned
ARG BUILDKIT_VERSION=v0.32.2
RUN curl -sSLo /tmp/buildkit.tar.gz \
      "https://github.com/moby/buildkit/releases/download/${BUILDKIT_VERSION}/buildkit-${BUILDKIT_VERSION}.linux-arm64.tar.gz" \
    && tar -xzf /tmp/buildkit.tar.gz -C /usr/local/bin --strip-components=1 \
    && rm /tmp/buildkit.tar.gz \
    && buildkitd --version && buildctl --version

ENV PATH="/usr/local/bin:${PATH}"
