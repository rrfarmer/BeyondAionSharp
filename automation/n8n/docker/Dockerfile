FROM mcr.microsoft.com/dotnet/sdk:10.0.301-noble

ARG NODE_VERSION=24.16.0
ARG N8N_VERSION=2.30.5
ARG CODEX_VERSION=0.144.4
ARG TARGETARCH

ENV DEBIAN_FRONTEND=noninteractive \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    NPM_CONFIG_AUDIT=false \
    NPM_CONFIG_FUND=false \
    NPM_CONFIG_UPDATE_NOTIFIER=false

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        git \
        jq \
        less \
        openssh-client \
        procps \
        python3 \
        python-is-python3 \
        ripgrep \
        xz-utils \
    && case "$TARGETARCH" in \
        amd64) node_arch="x64" ;; \
        arm64) node_arch="arm64" ;; \
        *) echo "Unsupported architecture: $TARGETARCH" >&2; exit 1 ;; \
       esac \
    && curl -fsSL "https://nodejs.org/dist/v${NODE_VERSION}/node-v${NODE_VERSION}-linux-${node_arch}.tar.xz" \
        | tar -xJ --strip-components=1 -C /usr/local \
    && curl -fsSL https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb \
        -o /tmp/packages-microsoft-prod.deb \
    && dpkg -i /tmp/packages-microsoft-prod.deb \
    && rm /tmp/packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y --no-install-recommends powershell \
    && npm install --global "n8n@${N8N_VERSION}" "@openai/codex@${CODEX_VERSION}" \
    && npm cache clean --force \
    && rm -rf /var/lib/apt/lists/* \
    && node --version \
    && dotnet --version \
    && pwsh --version \
    && codex --version \
    && n8n --version

RUN install -d -o app -g app \
    /home/app/.codex \
    /home/app/.n8n \
    /home/app/.nuget \
    /home/app/.nuget/packages \
    /workspace/csharp \
    /workspace/java

COPY beyond-aion-entrypoint.sh /usr/local/bin/beyond-aion-entrypoint
RUN chmod 0755 /usr/local/bin/beyond-aion-entrypoint

ENV HOME=/home/app \
    DOTNET_CLI_HOME=/home/app \
    NUGET_PACKAGES=/home/app/.nuget/packages

USER app
WORKDIR /workspace/csharp

ENTRYPOINT ["/usr/local/bin/beyond-aion-entrypoint"]
CMD ["n8n", "start"]
