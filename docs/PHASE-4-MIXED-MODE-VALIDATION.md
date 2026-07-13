# Phase 4 Mixed-Mode Validation

This runbook validates C# login + C# chat with the Java game server.

## Database

Start or refresh the shared MySQL container:

```powershell
powershell -ExecutionPolicy Bypass -File dotnetConversion/scripts/start-mixed-mode-db.ps1
```

To reset schemas:

```powershell
powershell -ExecutionPolicy Bypass -File dotnetConversion/scripts/start-mixed-mode-db.ps1 -ResetSchema
```

The script initializes `aion_ls`, `aion_gs`, and `aion_cs` from the Java schema files and seeds:

```sql
REPLACE INTO gameservers (id, mask, password) VALUES (1, '*', '1234');
```

## C# Login Server

Run from the repository root:

```powershell
dotnet run --project dotnetConversion/src/Aion.LoginServer/Aion.LoginServer.csproj
```

Expected listeners:
- client: `0.0.0.0:2106`
- game-server bridge: `0.0.0.0:9014`

## C# Chat Server

Run from the repository root:

```powershell
dotnet run --project dotnetConversion/src/Aion.ChatServer/Aion.ChatServer.csproj
```

Expected listeners:
- chat client: `0.0.0.0:10241`
- game-server bridge: `0.0.0.0:9021`

Ensure `chat-server/config/mycs.properties` or the default Java config has:

```properties
chatserver.network.gameserver.password = 1234
database.url = jdbc:mysql://localhost:3306/aion_cs?serverTimezone=&characterEncoding=UTF-8
database.user = root
database.password = aion
```

## Java Game Server In Docker

If an older Java chat/game mixed-mode stack is already running, stop it before starting this C# chat validation path. In particular, `10241` must be free for the host C# chat server and `7777` must be free for the Java game server container:

```powershell
docker ps --format "{{.Names}} {{.Ports}}"
powershell -ExecutionPolicy Bypass -File dotnetConversion/scripts/stop-mixed-mode-java.ps1
```

Build and run only the Java game server, pointed at host C# login and host C# chat:

```powershell
powershell -ExecutionPolicy Bypass -File dotnetConversion/scripts/start-mixed-mode-csharp-chat.ps1 -Build
```

For background mode:

```powershell
powershell -ExecutionPolicy Bypass -File dotnetConversion/scripts/start-mixed-mode-csharp-chat.ps1 -Build -Detached
```

To stop:

```powershell
powershell -ExecutionPolicy Bypass -File dotnetConversion/scripts/stop-mixed-mode-csharp-chat.ps1
```

The compose file mounts `docker/config/mygs.csharp-login-chat.properties`, where:
- login address is `host.docker.internal:9014`
- chat address is `host.docker.internal:9021`
- game-server id/password are `1` / `1234`
- advertised game address is `127.0.0.1:7777`

## Expected Signals

- C# login logs Java GS registration on `9014`.
- C# chat logs Java GS registration on `9021`.
- Java GS logs connected to login and chat.
- A real Aion client can target login at:

```batch
start /affinity 7FFFFFFF "" "bin64\AION.bin" -ip:127.0.0.1 -port:2106 -cc:2 -lang:ENG -loginex
```

## Smoke Result

Passed on May 20, 2026. A real Aion client connected through C# login, entered the Java game server, sent chat messages through C# chat, and displayed chat-window combat/info traffic during play.

Local automated coverage also validates:
- game-server auth/player registration over TCP
- client chat init/auth/channel request over TCP
- two-client channel broadcast over TCP
- hosted listener accept/read/write smoke for both chat listeners
- live `ChatLogRepository` insert against the Java `aion_cs.chatlog` schema
