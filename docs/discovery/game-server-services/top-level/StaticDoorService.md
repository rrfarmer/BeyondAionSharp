# StaticDoorService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/StaticDoorService.java`

## Likely C# Surface

- `HouseDoorStateService.cs`
- `StaticPlaceableStateService.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- Static-state and door ownership exists in C#, but not as one obvious door service.
- A detailed pass should confirm non-housing door lifecycle and zone/event triggers.