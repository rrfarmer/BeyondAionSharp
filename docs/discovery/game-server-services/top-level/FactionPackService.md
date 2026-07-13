# FactionPackService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/FactionPackService.java`

## Likely C# Surface

- `CustomLevelRewardPlanService.cs`
- `CustomLevelRewardExecutionService.cs`
- `CustomLevelRewardRepository.cs`
- `SystemMailRewardPlanService.cs`

## Discovery Status

- `Refactored`

## High-Level Notes

- Faction-pack ownership exists in C#, but the Java boundary is represented through renamed custom-reward planning, execution, persistence, and system-mail helpers.

## Ownership Trace

- `CustomLevelRewardExecutionService.cs` contains an explicit Java parity breadcrumb for `FactionPackService.sendRewards`.
- `CustomLevelRewardPlanService.cs` supplies faction-pack planning, including account-creation-window gating.
- `CustomLevelRewardRepository.cs` and `SystemMailRewardPlanService.cs` provide the receipt and delivery boundaries.

## Remaining Risks

- A detailed pass should verify whether all original progression triggers and reward-distribution entry points are now routed through the custom-level reward path.