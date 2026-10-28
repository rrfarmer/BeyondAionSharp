# Running the C# server stack (local dev)

Three executable projects in `AionServer.slnx`: **Aion.LoginServer**, **Aion.GameServer**, **Aion.ChatServer** (the rest are libraries/tests). Target framework: **net10.0**.

## 1. Prerequisites

- **.NET 10 SDK** (and a Visual Studio that supports it).
- **MySQL/MariaDB** reachable from the host with three databases — `aion_ls`, `aion_gs`, `aion_cs`.
- Free TCP ports: **2106** (LS client), **7777** (GS client), **10241** (CS client), **9014** (LS↔GS bridge), **9021** (CS bridge).

## 2. Database

Base configuration uses **`localhost:3306`**, user **`root`**, and a blank password. For local overrides, create
`login-server/config/myls.properties`, `game-server/config/mygs.properties`, and `chat-server/config/mycs.properties`.
Those files are ignored by Git. Set `database.url`, `database.user`, and `database.password` there.
**Run the servers on the host (via VS), so use `localhost:<published-port>` — not `host.docker.internal`.**

Load schema + the required gameservers seed (example uses the local Docker container `aion-gameserver-integration-mysql` published on 3307):

```bash
# from repo root
docker exec aion-gameserver-integration-mysql mysql -uroot -paion -e \
  "CREATE DATABASE IF NOT EXISTS aion_ls; CREATE DATABASE IF NOT EXISTS aion_gs; CREATE DATABASE IF NOT EXISTS aion_cs;"

docker exec -i aion-gameserver-integration-mysql mysql -uroot -paion aion_ls < login-server/sql/aion_ls.sql
docker exec -i aion-gameserver-integration-mysql mysql -uroot -paion aion_gs < game-server/sql/aion_gs.sql
docker exec -i aion-gameserver-integration-mysql mysql -uroot -paion aion_cs < chat-server/sql/aion_cs.sql

# REQUIRED: authorize the game server to register with the login server
docker exec -i aion-gameserver-integration-mysql mysql -uroot -paion aion_ls < login-server/sql/seed_gameservers.sql
```

The GS registers as **id=1 / password=`1234`** (`gameserver.network.id` / `gameserver.network.login.password`). Without the
`gameservers` row the login server returns `NOT_AUTHED` and the GS never registers — this is the most common first-run failure.

## 3. Config

At startup each server walks **up** from its `bin/` output (`AppContext.BaseDirectory`) via `FindRepoRoot`
until it finds `game-server/config`, then reads the real `*/config` directories + the per-instance `my*.properties` overrides.
Static data (665 XMLs under `game-server/data/static_data`) is loaded the same way — the first GS boot is a little slow while it parses.

## 4. Run in Visual Studio

1. Open `AionServer.slnx`.
2. Right-click the **solution** → **Configure Startup Projects…** → **Multiple startup projects**.
3. Action = **Start** for `Aion.LoginServer`, `Aion.ChatServer`, `Aion.GameServer`; **None** for everything else.
4. Order: **LoginServer → ChatServer → GameServer** (drag up). Order is forgiving — GS/CS retry the bridge every ~2s — but LS-first keeps logs clean.
5. **F5** (debug) or **Ctrl+F5** (run).

Or from the CLI (one terminal each, in order):

```bash
dotnet run --project src/Aion.LoginServer
dotnet run --project src/Aion.ChatServer
dotnet run --project src/Aion.GameServer
```

## 5. Healthy startup looks like

- **LoginServer:** `Loaded 1 registered game servers`, then listening on 2106 (client) + 9014 (GS bridge).
- **ChatServer:** connects to LS, listening on 10241.
- **GameServer:** static data loads, then `Authenticated with login server; 1 game servers registered`, then steady ping/pong keep-alive.

If the GameServer logs `NOT_AUTHED` or keeps retrying: missing `gameservers` seed row, or a DB port/credential mismatch.

## 6. Private test hosts

The separate private test hosts use their own ports, copied databases and a pinned
retail statistics package. Package13 is selected for those hosts only; see
[Private test server retail package](docs/private-test-retail-package.md) for
startup, verification and rollback. Ordinary host and live Docker defaults are
unchanged.

## 7. Companion bots

`.companion <class>` brings a player a companion that follows and plays its role (D41, `docs/companion-bots.md`).
It needs a game server started with companions on, and the host program beside it (`docs/companion-live-host.md`).
**The Docker stack has both on by default** (the maintainer, 2026-10-13): `docker compose up -d` starts the game
server with companions on and the service `companionhost`, which plays them.

- **Game server:** `AION_COMPANIONS=true` and `AION_COMPANION_ACCOUNTS=companion*` in its environment. The
  pattern is every account named `companion` and then digits; the names can also be listed outright. These are
  the stack's defaults (`docker/docker-compose.yml`); `AION_COMPANIONS=false` in `docker/.env` turns companions
  off, for the server and the host alike. A server started without the variables, by `dotnet run`, has them off.
  A seat account holds no character of its own; the login server makes the account at its first login.
- **Host, in the stack:** the service `companionhost` (`docker/companionhost`). It reaches the login and game
  servers over the stack's own network, keeps five seats waiting and logs in another whenever a player takes one,
  up to fifty (`AION_COMPANION_SPARE_SEATS`, `AION_COMPANION_MAX_SEATS` in `docker/.env`). Its log is
  `docker logs aion-companionhost`; its runs, with every seat's trace, are under `run/companion-host-stack` and
  are kept fourteen days. The nightly `docker/RestartAll.ps1` builds and starts it after the servers. Its image
  bakes the navmeshes of every map itself; the first build takes several minutes longer for that.
- **Host, from the checkout:** `pwsh -NoProfile -File scripts/live/companion-host.ps1 -Target aion`, and the same
  with `-Stop` from another terminal, runs the checkout's bot code in place of the stack's host. Stop that one
  first (`docker compose -f docker/docker-compose.yml stop companionhost`); the script refuses while it runs,
  because both would log the same seats in. It only reads Docker state and speaks the client protocol.
- **In game** (an Asmodian, on a map the bots have ground for): `.companion cleric`, `.companion list`,
  `.companion dismiss`.
