# LimitedItemTradeService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/LimitedItemTradeService.java`

## Likely C# Surface

- `NpcDialogLimitedItemFactAdapterService.cs`
- trade packet planners such as `SmTradeListPacketPlanService.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- Limited-item trade behavior is visible around NPC dialog and trade-list assembly, but not as one direct service.
- A detailed pass should verify purchase limits, persistence, and refresh timing.