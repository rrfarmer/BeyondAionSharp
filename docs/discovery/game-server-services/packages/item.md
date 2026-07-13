# item Package Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/item`
- 12 Java files.

## Likely C# Surface

- `ItemChargeService.cs`, `ItemRemodelService.cs`, `ItemSocketService.cs`
- the `ItemPurification*` cluster
- inventory helpers such as `InventoryAddService.cs` and `InventoryExpansionService.cs`

## Discovery Status

- `Refactored`

## High-Level Notes

- This is one of the clearest examples of the C# refactor from broad Java services into many focused workflow services.
- A detailed pass should verify which Java item helpers still lack direct C# ownership.