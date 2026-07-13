# DebugService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/DebugService.java`

## Likely C# Surface

- No obvious `DebugService` counterpart appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- Debug-only helpers may exist outside production service naming or may not be ported yet.
- A detailed pass should inspect diagnostics, packet tracing, and admin tooling.