# transfers Package Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/transfers`
- 4 Java files.

## Likely C# Surface

- No obvious `Transfer`-named service cluster appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- Character-transfer behavior is not obvious in the current C# service naming surface.
- A detailed pass should inspect account, player, and cross-server boundaries before classifying this as fully missing.