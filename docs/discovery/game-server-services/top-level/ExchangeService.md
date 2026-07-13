# ExchangeService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/ExchangeService.java`

## Likely C# Surface

- `PlayerExchangeRequestService.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- Exchange request behavior is obvious in C#, but the full Java exchange-service lifecycle is not mirrored directly.
- A detailed pass should confirm accept, cancel, trade-state mutation, and packet fanout.