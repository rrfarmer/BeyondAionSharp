# TradeService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/TradeService.java`

## Likely C# Surface

- `SmTradeListPacketPlanService.cs`
- `SmTradeInListPacketPlanService.cs`
- `TradeApFormulaService.cs`
- `PlayerExchangeRequestService.cs`

## Discovery Status

- `Refactored`

## High-Level Notes

- Trade behavior exists in C#, but split across pricing, packet, and exchange flows.
- A detailed pass should verify NPC trade, player trade, and trade-in logic separately.