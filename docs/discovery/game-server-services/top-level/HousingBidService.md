# HousingBidService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/HousingBidService.java`

## Likely C# Surface

- `HouseAuctionTimingService.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- Auction timing is obvious in C#, but the full housing-bid workflow is not mirrored directly.
- A detailed pass should confirm bidding, settlement, and notification behavior.