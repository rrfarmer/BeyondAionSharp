#!/bin/bash
# One-command first-time deploy for the Aion server stack (Linux/macOS/WSL).
#   1. ensures a .env exists (creates from .env.example on first run)
#   2. builds the server images
#   3. starts MySQL (auto-creates + seeds the databases) and the 3 servers
set -e
cd "$(dirname "$0")"

if [ ! -f .env ]; then
  cp .env.example .env
  echo "Created docker/.env from the template."
  echo ">> Open docker/.env and set SERVER_HOST to the address players will use,"
  echo ">> then run this script again.  (Default 127.0.0.1 works for this PC only.)"
fi

echo "Building images (the first build can take several minutes)..."
docker compose --env-file .env build

echo "Starting the stack..."
docker compose --env-file .env up -d

echo
echo "Aion server stack is starting up."
echo "  Status:   docker compose -f docker/docker-compose.yml ps"
echo "  Logs:     docker compose -f docker/docker-compose.yml logs -f"
echo "  Stop:     docker/stop.sh   (or: docker compose -f docker/docker-compose.yml down)"
