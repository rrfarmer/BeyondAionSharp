# LegionService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/LegionService.java`

## Likely C# Surface

- the `PlayerAlliance*` service cluster
- `PlayerLeagueRuntime.cs`

## Discovery Status

- `Refactored`

## High-Level Notes

- Grouped-player organization seems to have been renamed and decomposed in C# toward alliance and league runtime services.
- A detailed pass should confirm whether true legion-specific persistence and permissions are present.