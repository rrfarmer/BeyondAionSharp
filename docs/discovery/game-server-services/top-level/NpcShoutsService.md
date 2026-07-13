# NpcShoutsService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/NpcShoutsService.java`

## Likely C# Surface

- No obvious `NpcShouts`-named service appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- NPC shout/broadcast behavior is not obvious in current C# service naming.
- A detailed pass should inspect AI event, chat packet, and scripted NPC surfaces.