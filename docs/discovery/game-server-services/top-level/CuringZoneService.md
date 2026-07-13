# CuringZoneService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/CuringZoneService.java`

## Likely C# Surface

- No obvious `CuringZone`-named service appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- Zone-based healing or cleansing behavior is not obvious from current C# service names.
- A detailed pass should inspect world-region, zone-state, and effect-cleanup code.