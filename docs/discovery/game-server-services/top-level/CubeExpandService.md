# CubeExpandService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/CubeExpandService.java`

## Likely C# Surface

- `InventoryExpansionService.cs`
- `StorageExpansionNpcService.cs`

## Discovery Status

- `Refactored`

## High-Level Notes

- Cube expansion appears present in C#, but split between inventory mutation and NPC-facing flow.
- A detailed pass should confirm pricing, packet behavior, and storage-boundary rules.