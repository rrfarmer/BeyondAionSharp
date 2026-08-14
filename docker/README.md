# Aion Server — One-Command Docker Deploy

Run the whole Aion server (database + login + game + chat) on any machine with
[Docker](https://www.docker.com/products/docker-desktop/) installed. No coding, no
database setup, no editing config files by hand.

## Quick start

**Windows:**
```powershell
powershell -ExecutionPolicy Bypass -File docker\deploy.ps1
```

**Linux / macOS / WSL:**
```bash
./docker/deploy.sh
```

The first run creates **`docker/.env`** and stops. Open it, set **`SERVER_HOST`** to the
address your players will use, then run the command again:

| You want players to connect from… | Set `SERVER_HOST` to |
|-----------------------------------|----------------------|
| Only this same computer           | `127.0.0.1` (default) |
| Other PCs on your home network    | this PC's LAN IP, e.g. `192.168.1.50` |
| The internet                      | your public IP or domain name |

That's it. The script builds the server images, starts a MySQL database (creating and
seeding all three databases automatically on first run), and launches the three servers.

## Everyday use

```bash
# Status
docker compose -f docker/docker-compose.yml ps
# Live logs
docker compose -f docker/docker-compose.yml logs -f
# Stop (keeps your database)            # Windows: docker\stop.ps1
docker/stop.sh
# Start again (no rebuild)
docker compose -f docker/docker-compose.yml --env-file docker/.env up -d
```

## What `.env` controls

| Setting | Meaning |
|---------|---------|
| `SERVER_HOST` | Address players type into their client to reach your server |
| `DB_PASSWORD` | Database password (stays inside Docker) |
| `LOGIN_CLIENT_PORT` / `GAME_CLIENT_PORT` / `CHAT_CLIENT_PORT` | Ports clients connect to (2106 / 7777 / 10241) |
| `RESPAWN_TIME_MULTIPLIER` | Mob respawn speed — `1.0` normal, `0.5` faster, `2.0` slower |

You never need to touch the `.properties` files — each server's container generates its
own config from these values at start-up.

## How it fits together

```
docker/deploy.sh / deploy.ps1
        │  builds images, runs docker compose
        ▼
docker compose (docker/docker-compose.yml)
        ├─ mysql        — auto-creates aion_ls / aion_gs / aion_cs on first boot
        ├─ loginserver  — :2106 clients, :9014 game-server bridge
        ├─ gameserver   — :7777 clients  (carries the game data)
        └─ chatserver   — :10241 clients, :9021 game-server bridge
```

See [`SETUP-GUIDE.md`](SETUP-GUIDE.md) for a step-by-step walkthrough and troubleshooting.
