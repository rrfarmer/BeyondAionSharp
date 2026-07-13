# CommandsAccessService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/CommandsAccessService.java`

## Likely C# Surface

- No obvious `CommandsAccess`-named service appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- Command access control likely lives outside the current C# service surface or is still pending.
- A detailed pass should inspect command registration and permission gates.