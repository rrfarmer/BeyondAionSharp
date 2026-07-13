# RepurchaseService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/RepurchaseService.java`

## Likely C# Surface

- No obvious `Repurchase`-named service appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- Repurchase behavior is not obvious in the current C# service naming surface.
- A detailed pass should inspect NPC trade and inventory rollback/repurchase paths.