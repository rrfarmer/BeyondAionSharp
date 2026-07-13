# trade Package Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/trade`
- 1 Java file.

## Likely C# Surface

- `PricesService.cs`
- `TradeApFormulaService.cs`
- `TradeListTable.cs`
- `NpcDialogServiceSelectPlanService.cs`
- `NpcDialogLimitedItemFactAdapterService.cs`
- trade packet planners such as `SmTradeListPacketPlanService.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- Pricing and trade-list surfaces are obvious, but the Java trade package is not mirrored directly.

## Ownership Trace

- `NpcDialogServiceSelectPlanService.cs` contains Java parity breadcrumbs for `DialogService BUY` and trade-in packet selection.
- `NpcDialogLimitedItemFactAdapterService.cs` contains Java parity breadcrumbs for `LimitedItemTradeService.start` static discovery and limited-item buy-count shaping.
- `TradeListTable.cs` contains Java parity breadcrumbs for `TradeListData` trade-list and trade-in indexing.

## Remaining Risks

- A detailed pass should verify live limited-item purchase mutation and buy-count persistence, not just static list assembly.