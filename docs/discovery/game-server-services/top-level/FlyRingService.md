# FlyRingService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/FlyRingService.java`

## Likely C# Surface

- No obvious `FlyRing`-named service appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- Flight-ring logic is not easy to identify from current C# service names.
- A detailed pass should inspect world object triggers, movement, and flight-state code.