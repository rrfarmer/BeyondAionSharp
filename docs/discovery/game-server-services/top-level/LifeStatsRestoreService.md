# LifeStatsRestoreService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/LifeStatsRestoreService.java`

## Likely C# Surface

- `PlayerExperienceRecoveryService.cs`
- `WorldNpcLifeStatsService.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- Recovery-related C# services exist, but the Java life-stats restore ownership is not mirrored directly.
- A detailed pass should verify timers, restore triggers, and enter-world restoration behavior.