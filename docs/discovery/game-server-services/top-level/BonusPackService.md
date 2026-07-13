# BonusPackService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/BonusPackService.java`

## Likely C# Surface

- `CustomLevelRewardPlanService.cs`
- `CustomLevelRewardExecutionService.cs`
- `CustomLevelRewardRepository.cs`
- `SystemMailRewardPlanService.cs`

## Discovery Status

- `Refactored`

## High-Level Notes

- Bonus-pack ownership exists in C#, but the Java boundary is represented through renamed custom-reward planning, execution, persistence, and system-mail helpers.

## Ownership Trace

- `CustomLevelRewardExecutionService.cs` contains an explicit Java parity breadcrumb for `BonusPackService.addPlayerCustomReward`.
- `CustomLevelRewardPlanService.cs` and `CustomLevelRewardRepository.cs` provide the concrete reward-planning and receipt boundary.
- `SystemMailRewardPlanService.cs` provides the delivery side of the represented reward flow.

## Remaining Risks

- A detailed pass should verify how much of the original bonus-pack trigger surface is live beyond the current planning and mail-delivery path.