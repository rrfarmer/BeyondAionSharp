# WarehouseService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/WarehouseService.java`

## Likely C# Surface

- `StorageExpansionNpcService.cs`
- inventory/storage helpers may own part of the same workflow

## Discovery Status

- `Partial`

## High-Level Notes

- Storage-related behavior exists in C#, but there is no direct warehouse service match.
- A detailed pass should verify account warehouse, legion warehouse, and packet/state behavior.