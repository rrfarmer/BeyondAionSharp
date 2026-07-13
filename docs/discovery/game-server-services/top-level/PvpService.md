# PvpService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/PvpService.java`

## Likely C# Surface

- `CreaturePvpZoneStateService.cs`
- `PvpApRewardService.cs`
- `PvpDpRewardService.cs`
- instance reward helpers such as `PvpInstanceApRewardService.cs`

## Discovery Status

- `Refactored`

## High-Level Notes

- PvP behavior is present, but decomposed into zone-state and reward-specific C# services.
- A detailed pass should verify kill credit, reward triggers, and non-reward side effects.