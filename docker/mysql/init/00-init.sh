#!/bin/bash
# Runs ONCE on first MySQL container boot (placed in /docker-entrypoint-initdb.d).
# Creates the three Aion databases and loads each schema + seed into the correct DB.
# The schema .sql files are pure table DDL (no CREATE DATABASE / USE), so we create
# the databases here and pipe each file into its target DB.
#
# The repo SQL trees are mounted read-only into the container by docker-compose:
#   /sql/login  -> login-server/sql
#   /sql/game   -> game-server/sql
#   /sql/chat   -> chat-server/sql
#
# NOTE: no `set -e` — a failing migration must NOT abort the whole init (see load_optional).
set -uo pipefail

mysql_exec() { mysql --protocol=socket -uroot -p"${MYSQL_ROOT_PASSWORD}" "$@"; }

echo "[aion-init] creating databases aion_ls / aion_gs / aion_cs ..."
mysql_exec <<'SQL'
CREATE DATABASE IF NOT EXISTS aion_ls CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
CREATE DATABASE IF NOT EXISTS aion_gs CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
CREATE DATABASE IF NOT EXISTS aion_cs CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
SQL

# Full, current schema + seed — these are authoritative for a fresh install and must load.
load_strict() {
  local db="$1" file="$2"
  if [ -f "$file" ]; then
    echo "[aion-init] loading $file -> $db"
    mysql_exec "$db" < "$file" || echo "[aion-init] !! ERROR loading $file -> $db"
  else
    echo "[aion-init] (skip, not found: $file)"
  fi
}

# Historical migration scripts. They target OLDER databases and do not apply cleanly to the
# full current schema (e.g. DROP a table the fresh schema never created) — tolerate per-statement
# errors with --force and never abort the init.
load_optional() {
  local db="$1" file="$2"
  [ -f "$file" ] || return 0
  echo "[aion-init] (optional) applying $file -> $db"
  mysql_exec --force "$db" < "$file" 2>/dev/null \
    || echo "[aion-init] (optional $file had errors — expected on a fresh install, ignored)"
}

# Full schemas (authoritative)
load_strict aion_ls /sql/login/aion_ls.sql
load_strict aion_gs /sql/game/aion_gs.sql
load_strict aion_cs /sql/chat/aion_cs.sql

# Seed: register the game server so the login server will authorize it
load_strict aion_ls /sql/login/seed_gameservers.sql

# Optional historical migrations (non-fatal)
load_optional aion_ls /sql/login/update.sql
load_optional aion_gs /sql/game/update.sql

echo "[aion-init] done. Databases ready."
