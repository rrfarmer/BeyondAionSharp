# DatabaseCleaningService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/DatabaseCleaningService.java`

## Likely C# Surface

- No obvious `DatabaseCleaning`-named service appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- Cleanup and maintenance ownership may sit in repository or bootstrap code rather than services.
- A detailed pass should inspect startup jobs and maintenance-hosted services.