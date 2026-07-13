# summons Package Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/summons`
- 2 Java files.

## Likely C# Surface

- `SummonPanelPacketPlanService.cs`
- `SummonUpdatePacketPlanService.cs`
- `SummonOwnerRemovePacketPlanService.cs`
- summon skill helpers such as `PlayerSummonSkillExecutionService.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- Summon-related packet and execution services exist, but the Java package shape is not mirrored directly.
- A detailed pass should confirm owner lifecycle, trap handling, and summon persistence rules.