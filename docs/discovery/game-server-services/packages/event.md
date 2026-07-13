# event Package Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/event`
- 3 Java files.

## Likely C# Surface

- No obvious `EventService`-style counterpart appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- Some event behavior may exist under reward, world, or packet-specific services, but it is not obvious from names.
- A detailed pass should inspect seasonal reward, scheduled event, and buff-related C# surfaces.