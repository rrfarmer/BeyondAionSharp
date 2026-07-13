# LoginServer image. Build context = repo root.
# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ ./src/
RUN dotnet publish src/Aion.LoginServer/Aion.LoginServer.csproj \
      -c Release -o /publish -p:UseAppHost=false

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
# LoginServer resolves config by walking up from CWD (/app) to find <dir>/login-server/config.
COPY login-server/config/ ./login-server/config/
COPY --from=build /publish/ ./
COPY docker/loginserver/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
EXPOSE 2106
ENTRYPOINT ["/entrypoint.sh"]
