# TribeRelationService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/TribeRelationService.java`

## Likely C# Surface

- No obvious `TribeRelation`-named service appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- Tribe/faction-relation behavior is not obvious from the current C# service naming surface.
- A detailed pass should inspect AI hostility, faction, and world-rule helpers.