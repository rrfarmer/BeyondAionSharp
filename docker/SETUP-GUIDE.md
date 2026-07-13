# Aion Server — Setup Guide (Docker)

Run the full Aion server — database, login, chat, and game servers — on one machine with
**Docker**. No coding, no manual database setup, no editing config files by hand. Honestly,
it's just Docker. 🙂

Everything below assumes you're in the project folder (the one that contains the `docker/`
directory).

---

## 1. Install Docker

Install **Docker Desktop** and make sure it's running before you do anything else:

- Windows / macOS: https://www.docker.com/products/docker-desktop/
- Linux: install Docker Engine + the Compose plugin from your distro, or Docker Desktop.

Check it's working:

```bash
docker version
docker compose version
```

Both commands should print version numbers. If they error, Docker isn't installed or isn't
running yet — start Docker Desktop and try again.

---

## 2. Configure your server (the `.env` file)

All of your settings live in **one file**: `docker/.env`. You never edit the `.properties`
files — each server generates its own config from `.env` when it starts.

### Create it

Copy the template:

**Windows (PowerShell):**
```powershell
Copy-Item docker\.env.example docker\.env
```

**Linux / macOS / WSL:**
```bash
cp docker/.env.example docker/.env
```

> Tip: the deploy script (Step 3) creates this file for you automatically on the first run if
> it doesn't exist, then stops so you can edit it. Either way works.

### Edit it

Open `docker/.env` in any text editor and set the values:

| Setting | What it does | Typical value |
|---------|--------------|---------------|
| **`SERVER_HOST`** | The address players type into their client to reach your server. **This is the one you almost always change.** | See the table below |
| `DB_PASSWORD` | Database password. Stays inside Docker; pick anything. | `aion` |
| `DB_USER` | Database user. Leave as `root` unless you know why you'd change it. | `root` |
| `LOGIN_CLIENT_PORT` | Port clients use to log in. | `2106` |
| `GAME_CLIENT_PORT` | Port clients use to enter the world. | `7777` |
| `CHAT_CLIENT_PORT` | Port clients use for in-game chat. | `10241` |
| `RESPAWN_TIME_MULTIPLIER` | Mob respawn speed. `1.0` = normal, `0.5` = twice as fast, `2.0` = twice as slow. | `1.0` |

**Choosing `SERVER_HOST`:**

| You want players to connect from… | Set `SERVER_HOST` to |
|-----------------------------------|----------------------|
| Only this same computer | `127.0.0.1` |
| Other PCs on your home network | this PC's LAN IP, e.g. `192.168.1.50` |
| The internet | your public/WAN IP or a domain name, e.g. `203.0.113.10` |

Save the file. That's all the configuration there is.

---

## 3. Start it

**Windows (PowerShell):**
```powershell
powershell -ExecutionPolicy Bypass -File docker\deploy.ps1
```

**Linux / macOS / WSL:**
```bash
./docker/deploy.sh
```

The deploy script builds the server images and starts everything. **The first build takes
several minutes** (it compiles all three C# servers and bundles the game data — a few hundred
MB), and the **first startup** also creates and seeds all three databases automatically. Later
runs are much faster because nothing needs rebuilding.

Prefer to run it by hand instead of the script? This does the same thing:

```bash
docker compose -f docker/docker-compose.yml --env-file docker/.env up -d --build
```

---

## What happens on startup (and the order)

The services come up in a strict order, each waiting for the previous one to settle:

```
MySQL (waits until DB init is fully done)
  └─▶ Login Server
        └─▶  +15 seconds  ─▶ Chat Server
                              └─▶  +15 seconds  ─▶ Game Server
```

- **MySQL** starts first. The next service doesn't start until MySQL has finished creating and
  seeding the databases (a real readiness check — on a first boot this is usually well over 15
  seconds anyway).
- **Login → Chat** and **Chat → Game** each have a deliberate **15-second pause** so the heavier
  servers don't all start at the same instant.
- You can change those pauses in `docker/docker-compose.yml` — look for `interval: 15s` on the
  `loginserver` and `chatserver` healthchecks.

The game server takes ~20 seconds to load its data on boot, so give the whole stack a minute or
two to fully come up the first time.

---

## Everyday commands

Run these from the project folder.

```bash
# See what's running
docker compose -f docker/docker-compose.yml ps

# Watch live logs (Ctrl+C to stop watching — servers keep running)
docker compose -f docker/docker-compose.yml logs -f

# Logs for just one server
docker compose -f docker/docker-compose.yml logs -f gameserver
```

**Stop the server** (your database is kept):

```bash
# Windows
docker\stop.ps1
# Linux / macOS / WSL
./docker/stop.sh
```

**Start again** (no rebuild needed):

```bash
docker compose -f docker/docker-compose.yml --env-file docker/.env up -d
```

**After you change a setting in `.env`** — just start again (above); the servers pick up the new
values on restart.

**After the server code changes** — rebuild the images:

```bash
docker compose -f docker/docker-compose.yml --env-file docker/.env up -d --build
```

---

## Letting people on the internet connect

If you set `SERVER_HOST` to a public/WAN IP, players still can't reach you until your **router
forwards** these ports to the machine running Docker:

| Port | Protocol | Service |
|------|----------|---------|
| 2106 | TCP | Login |
| 7777 | TCP | Game (enter world) |
| 10241 | TCP | Chat |

Do **not** forward ports 9014 or 9021 — those are internal server-to-server links inside Docker
and should never be exposed.

> Testing from a PC on the *same* network as the server? Connecting to your own public IP can
> fail due to "NAT hairpinning." Test from outside your network, or use the LAN IP locally.

---

## Troubleshooting

| Symptom | What to check |
|---------|---------------|
| `docker` / `docker compose` "command not found" | Docker Desktop isn't installed or isn't started. |
| Build fails immediately | Make sure Docker Desktop is running and you have a few GB of free disk space. |
| A server keeps restarting | Check its logs: `docker compose -f docker/docker-compose.yml logs <name>` (e.g. `gameserver`). |
| Players can't connect over the internet | Confirm `SERVER_HOST` is your public IP and the three ports above are forwarded on your router. |
| "Port is already allocated" | Something else on the PC is using 2106 / 7777 / 10241. Change the matching `*_CLIENT_PORT` in `.env`. |
| Want a totally clean slate (wipes the database) | `docker compose -f docker/docker-compose.yml down -v` then start again. **This deletes all characters/accounts.** |

---

For the technical build details, see [`DEPLOY-PROGRESS.md`](DEPLOY-PROGRESS.md).
