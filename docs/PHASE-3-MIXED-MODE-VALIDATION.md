# Phase 3 Mixed-Mode Validation

This runbook starts the Java chat and game servers in Docker and the C# login server from Visual Studio or `dotnet run`.

## Database Initialization

The Java README is explicit: initialize each server database with the `*.sql` file in that server's `sql` folder. The login DB also needs a row in `gameservers` before a game server can authenticate to login.

For local mixed-mode validation, run:

```powershell
powershell -ExecutionPolicy Bypass -File dotnetConversion/scripts/start-mixed-mode-db.ps1
```

The script starts a MySQL 8.4 container named `aion-mixed-mysql` on `localhost:3306`, creates `aion_ls`, `aion_gs`, and `aion_cs`, initializes them from:

- `login-server/sql/aion_ls.sql`
- `game-server/sql/aion_gs.sql`
- `chat-server/sql/aion_cs.sql`

On first container creation, schemas are applied automatically. To intentionally recreate the schema tables in an existing container, rerun with:

```powershell
powershell -ExecutionPolicy Bypass -File dotnetConversion/scripts/start-mixed-mode-db.ps1 -ResetSchema
```

The script also seeds the login DB with the Java default game-server credentials:

```sql
REPLACE INTO gameservers (id, mask, password) VALUES (1, '*', '1234');
```

No account rows are seeded by default. The Java login config has `loginserver.accounts.autocreate = true`, and the C# login port follows that behavior, so the first valid client login creates the account row. The game and chat databases start empty apart from schema.

## C# Login Server

Start `Aion.LoginServer` from Visual Studio, or run:

```powershell
dotnet run --project dotnetConversion/src/Aion.LoginServer/Aion.LoginServer.csproj
```

With the default Java login config, it binds:

- client listener: `0.0.0.0:2106`
- game-server listener: `0.0.0.0:9014`
- database: `localhost:3306/aion_ls`, user `root`, password `aion`
- logs: `login-server/log/server_console.log`, `login-server/log/server_warnings.log`, and `login-server/log/server_errors.log`

If you need a different local DB port or password, create ignored local override file `login-server/config/myls.properties`.

## Java Chat And Game Servers In Docker

Build and run the Java chat and game servers, pointed at the host C# login server:

```powershell
powershell -ExecutionPolicy Bypass -File dotnetConversion/scripts/start-mixed-mode-java.ps1 -Build
```

For background mode:

```powershell
powershell -ExecutionPolicy Bypass -File dotnetConversion/scripts/start-mixed-mode-java.ps1 -Build -Detached
```

To stop the Java containers:

```powershell
powershell -ExecutionPolicy Bypass -File dotnetConversion/scripts/stop-mixed-mode-java.ps1
```

The compose file mounts:

- `docker/config/mycs.csharp-login.properties` as the Java chat server's `config/mycs.properties`
- `docker/config/mygs.csharp-login.properties` as the Java game server's `config/mygs.properties`

Important mixed-mode settings:

- game DB: `host.docker.internal:3306/aion_gs`
- login address: `host.docker.internal:9014`
- GS id/password: `1` / `1234`
- chat DB: `host.docker.internal:3306/aion_cs`
- chat bridge address from GS: `chat:9021`
- chat bridge password: `1234`
- advertised client address: `127.0.0.1:7777`
- advertised chat address: `127.0.0.1:10241`

## Expected Signals

The C# login server should log that game server `1` connected successfully. The Java game server should report that it connected to login. The Java chat server should report that game server `1` is online. After that, a patched Aion client can target:

```batch
start /affinity 7FFFFFFF "" "bin64\AION.bin" -ip:127.0.0.1 -port:2106 -cc:2 -lang:ENG -loginex
```

If you only need login-server bridge validation, the game server can run without chat by setting `gameserver.chatserver.enable = false` in `docker/config/mygs.csharp-login.properties`.

## Last Smoke Result

Validated on May 19, 2026:

- `powershell -ExecutionPolicy Bypass -File dotnetConversion/scripts/start-mixed-mode-java.ps1 -Build -Detached` completed successfully.
- Java chat built, started, connected to `aion_cs`, listened on `10241` for clients, and listened on `9021` for game servers.
- `dotnet run --project dotnetConversion/src/Aion.LoginServer/Aion.LoginServer.csproj` started against `aion_ls`.
- Java game built, started, connected to `aion_gs`, listened on `7777`, logged `Connected to login server`, and logged `Connected to chat server`.
- Java chat logged `Gameserver #1 is now online`.
- The C# login server loaded one registered game server and accepted the Java GS bridge connection on `9014`.
- A real client completed login through the C# login server, selected/entered the Java game server, created a character, and logged out successfully.
- This closes the known Phase 3 mixed-mode validation gap.
