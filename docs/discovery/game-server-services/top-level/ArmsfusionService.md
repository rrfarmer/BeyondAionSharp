# ArmsfusionService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/ArmsfusionService.java`

## Likely C# Surface

- `ArmsfusionPricePlanService.cs`
- `AssemblyItemService.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- C# has armsfusion-related pricing and assembly surfaces, but the full Java service boundary is not mirrored directly.
- A detailed pass should confirm item mutation and packet ordering parity.