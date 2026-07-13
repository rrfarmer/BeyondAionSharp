# abyss Package Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/abyss`
- 6 Java files.

## Likely C# Surface

- `AbyssPointsService.cs`
- `AbyssSkillService.cs`
- `GloryPointsService.cs`
- AP reward helpers such as `PvpApRewardService.cs`

## Discovery Status

- `Refactored`

## High-Level Notes

- The Java abyss area appears split across direct abyss services and broader PvP reward services.
- A detailed pass should confirm ranking-cache behavior, rank updates, and siege-linked side effects.