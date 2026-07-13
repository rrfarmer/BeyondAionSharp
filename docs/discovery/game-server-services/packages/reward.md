# reward Package Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/reward`
- 5 Java files.

## Likely C# Surface

- `StarterKitLevelChangePlanService.cs`
- `CustomLevelRewardPlanService.cs`
- `CustomLevelRewardExecutionService.cs`
- `CustomLevelRewardRepository.cs`
- `SystemMailRewardPlanService.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- Some reward behavior is clearly present, but the Java reward package is not mirrored directly.

## Ownership Trace

- `CustomLevelRewardExecutionService.cs` contains explicit Java parity breadcrumbs for `BonusPackService.addPlayerCustomReward` and `FactionPackService.sendRewards`.
- `CustomLevelRewardRepository.cs` persists bonus/faction reward receipts and gives the current C# reward path a concrete data boundary.
- The C# options surface already carries starter-kit and web-reward feature flags.

## Remaining Risks

- A detailed pass should confirm veteran, web, and advent reward ownership.
- Current C# comments indicate web rewards are still deferred rather than fully ported.