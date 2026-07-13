# UpgradeArcadeService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/UpgradeArcadeService.java`

## Likely C# Surface

- No obvious `UpgradeArcade`-named service appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- This feature is not obvious in the current C# services surface.
- A detailed pass should inspect event, reward, and item-upgrade code for renamed coverage.