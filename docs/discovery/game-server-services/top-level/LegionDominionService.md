# LegionDominionService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/LegionDominionService.java`

## Likely C# Surface

- No obvious `LegionDominion`-named service appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- Dominion-specific legion behavior is not obvious in current C# service naming.
- A detailed pass should inspect alliance, siege, and faction-control code for renamed coverage.