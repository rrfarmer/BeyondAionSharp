# craft Package Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/craft`
- 3 Java files.

## Likely C# Surface

- `CraftService.cs`
- `CraftLearnService.cs`
- `CraftSkillUpdateService.cs`

## Discovery Status

- `Present`

## High-Level Notes

- Crafting has a close C# surface and appears to be one of the clearer ports.
- A detailed pass should verify any missing helper behavior such as relinquish-state handling.