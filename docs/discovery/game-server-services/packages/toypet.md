# toypet Package Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/toypet`
- 8 Java files.

## Likely C# Surface

- `dotnetConversion/src/Aion.GameServer/Services/ToyPet/`
- pet-adjacent services such as `PlayerPetOrderSkillService.cs`
- known-list pet visibility helpers under `PlayerKnownListPet*`
- toy-pet spawn handling in `GameServerConnection.cs`
- pet persistence planning in `PlayerPetsRepositoryPlan.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- Toy-pet behavior is clearly present in C#, but the main services live in a separate subfolder rather than the flat services root.

## Ownership Trace

- `GameServerConnection.cs` contains explicit Java parity breadcrumbs for `ToyPetSpawnAction.canAct`, delayed spawn completion, and follow-up bind/dialog behavior.
- `Services/ToyPet/` contains direct feed-calculation, feed-progress, and feed-operation planning surfaces.
- `PlayerPetsRepositoryPlan.cs` and `PlayerPetRowProjection.cs` give the toy-pet mood/feed data a concrete repository boundary.

## Remaining Risks

- A detailed pass should confirm adoption ownership, full live mood lifecycle, and whether the whole Java `PetService` surface is represented.