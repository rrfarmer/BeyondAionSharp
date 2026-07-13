#!/bin/bash
# Stop the Aion server stack (keeps the database volume).
cd "$(dirname "$0")"
docker compose --env-file .env down
echo "Stopped. (Database data is preserved. To wipe it too: docker compose down -v)"
