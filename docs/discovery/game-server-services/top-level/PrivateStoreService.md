# PrivateStoreService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/PrivateStoreService.java`

## Likely C# Surface

- No obvious `PrivateStore`-named service appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- Private-store behavior is not obvious in the current C# services surface.
- A detailed pass should inspect trade, broker, and packet handler code for renamed coverage.