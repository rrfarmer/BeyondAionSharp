# AutoGroupService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/AutoGroupService.java`

## Likely C# Surface

- No obvious `AutoGroup`-named service appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- Auto-group behavior may have been deferred or merged into player-group flow.
- A detailed pass should inspect queueing, grouping, and instance-entry handlers.