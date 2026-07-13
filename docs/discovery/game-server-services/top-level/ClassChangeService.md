# ClassChangeService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/ClassChangeService.java`

## Likely C# Surface

- `CharacterCreationService.cs`
- `PlayerLevelChangeUpgradePlanService.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- Class-change related behavior may be spread across character setup and level-change flows in C#.
- A detailed pass should confirm whether true class-change operations exist beyond creation-time logic.