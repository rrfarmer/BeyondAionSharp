# HousingService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/HousingService.java`

## Likely C# Surface

- `HousingWorldService.cs`
- `HousingVisibilityService.cs`
- `HouseDoorStateService.cs`
- `HouseMaintenanceTimingService.cs`
- `HouseAuctionTimingService.cs`
- `IHouseAuctionRepository` / `MySqlHouseAuctionRepository`

## Discovery Status

- `Refactored`

## High-Level Notes

- Housing is present in C#, but spread across world, visibility, door, and maintenance ownership.

## Ownership Trace

- `Program.cs` registers `HousingWorldService`, `HousingVisibilityService`, `HouseAuctionTimingService`, `HouseMaintenanceTimingService`, `IHouseAuctionRepository`, and `IHouseDoorStateService` together, which gives housing a clear runtime ownership cluster.
- `GameServerConnection.cs` already calls the house-auction repository for bid-page load, auction registration, bid placement, rent payment, and settings updates.

## Remaining Risks

- A detailed pass should confirm bid-result coverage breadth, visit flow, and broader house-instance side effects.