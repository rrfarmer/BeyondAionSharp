# ChatServer image. Build context = repo root.
# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ ./src/
RUN dotnet publish src/Aion.ChatServer/Aion.ChatServer.csproj \
      -c Release -o /publish -p:UseAppHost=false

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
# ChatServer resolves config by walking up from CWD (/app) to find <dir>/chat-server/config.
COPY chat-server/config/ ./chat-server/config/
COPY --from=build /publish/ ./
COPY docker/chatserver/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
EXPOSE 10241
ENTRYPOINT ["/entrypoint.sh"]
