# WeatherService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/WeatherService.java`

## Likely C# Surface

- `WorldMapRegionMaterialZoneWeatherBroadcastPlanService.cs`
- `WorldMapRegionMaterialZoneWeatherTransitionPlanService.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- Weather behavior appears to exist within world-region material-zone services rather than a single dedicated weather service.
- A detailed pass should confirm scheduling, zone scoping, and packet fanout.