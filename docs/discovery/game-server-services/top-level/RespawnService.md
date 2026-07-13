# RespawnService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/RespawnService.java`

## Likely C# Surface

- `PlayerReviveRestoreService.cs`
- `WorldNpcSpawnService.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- Player and NPC recovery/spawn surfaces exist, but the Java respawn-service boundary is not mirrored directly.
- A detailed pass should verify timers, corpse-state cleanup, and instance/world spawn rules.