# Aion Server — Docker Deploy (work in progress)

Goal: a non-technical user runs **one script** and gets the whole stack (MySQL +
LoginServer + GameServer + ChatServer) running in Docker, configured by editing a
single `.env` file (no hand-editing of `.properties`).

## Architecture

```
                 ┌─────────────────────── docker network "aion" ───────────────────────┐
  Aion client ──▶│  loginserver  :2106 (client)  :9014 (GS bridge)                      │
  (host:2106)    │  gameserver   :7777 (client)  ──▶ loginserver:9014, chatserver:9021  │
                 │  chatserver   :10241 (client) :9021 (GS bridge)                       │
                 │  mysql        :3306  (aion_ls / aion_gs / aion_cs)                    │
                 └──────────────────────────────────────────────────────────────────────┘
```

- Each .NET server is published in a multi-stage image (build with the SDK, run on the
  ASP.NET runtime). The server's working tree (`login-server/`, `game-server/`,
  `chat-server/` config + data) is copied in so the bootstrap's relative-path + walk-up
  config resolution works unchanged.
- **GameServer carries `game-server/data` (333 MB) + config** — biggest image.
- **Cross-server addresses** are the only thing that must change for Docker:
  - GS→LS `gameserver.network.login.address` = `loginserver:9014` (was localhost)
  - GS→CS `gameserver.network.chat.address` = `chatserver:9021`
  - client-facing host (what LS advertises for the GS, `connect_address` / seed_gameservers)
    = the user's `SERVER_HOST` from `.env`
  - all `database.url` = `mysql` service
- **env → config bridge:** each container's entrypoint writes a `*.properties` override
  (the existing highest-precedence `mygs.properties` / `myls.properties` / `mycs.properties`)
  from env vars before launching the server. No server code change required.

## Databases (auto-created on first MySQL boot)

Schemas are pure table DDL (no CREATE DATABASE). `mysql/init/00-init.sh` creates the
3 DBs and loads each schema + seed into the correct DB:

| DB        | schema                         | extra                                   |
|-----------|--------------------------------|-----------------------------------------|
| `aion_ls` | `login-server/sql/aion_ls.sql` | `seed_gameservers.sql`, `update.sql`    |
| `aion_gs` | `game-server/sql/aion_gs.sql`  | `update.sql`                            |
| `aion_cs` | `chat-server/sql/aion_cs.sql`  |                                         |

## Layout

```
docker/
  .env.example          # copy to .env, edit; the ONLY file a user touches
  DEPLOY-PROGRESS.md     # this file
  mysql/init/00-init.sh  # create DBs + load schemas/seeds (first boot only)
  docker-compose.yml     # [todo] mysql + ls + gs + cs
  deploy.sh / deploy.ps1 # [todo] one-command first-time deploy
  start.sh stop.sh       # [todo] run/stop afterward
  <server>/Dockerfile    # [todo] multi-stage .NET build+runtime per server
  <server>/entrypoint.sh # [todo] env -> .properties override, then run
```

## Progress

- [x] Recon (projects, SQL, config, ports, data footprint)
- [x] `.env.example` (user config surface)
- [x] `mysql/init/00-init.sh` (auto DB create + schema/seed load)
- [x] Dockerfiles (LS / GS / CS) — multi-stage .NET 10 build (`sdk:10.0`) + `runtime:10.0`; build context = repo root; GS copies game-server/{config,data,cache}, LS/CS copy their config tree
- [x] entrypoints (env → `my{ls,gs,cs}.properties` override; DB url→mysql, GS login→loginserver:9014/chat→chatserver:9021, client connect_address→SERVER_HOST)
- [x] `.dockerignore` (exclude bin/obj/.git/target)
- [x] `docker-compose.yml` (mysql:8.4 + 3 servers; healthcheck; depends_on healthy; ports from .env; internal MySQL, no host clash with the 3307 dev DB)
- [x] `deploy.sh` / `deploy.ps1` (auto-create .env, build + up) and `stop.sh` / `stop.ps1`
- [x] End-to-end verify (on this machine): all 3 images build; `up` → MySQL auto-init creates
      aion_ls(9)/aion_gs(63)/aion_cs(1) tables + gameservers seed; **all 3 servers boot with 0
      restarts**; GS↔LS bridge authenticates ("1 game servers registered"); listeners on
      2106/7777/10241. Fixed during verify: init resilience (optional `update.sql` migrations no
      longer abort the run) + MySQL healthcheck means "init done over TCP" (no startup crash-race).

**PART 2 COMPLETE.**

## Usage (for the README later)
```
# Linux / macOS / WSL
./docker/deploy.sh
# Windows
powershell -ExecutionPolicy Bypass -File docker\deploy.ps1
```
First run creates `docker/.env` — set `SERVER_HOST` (players' address), re-run. That's it.
