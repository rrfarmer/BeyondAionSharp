# DuelService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/DuelService.java`

## Likely C# Surface

- `PlayerDuelRequestService.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- A duel request surface exists in C#, but a broader duel lifecycle is not obvious by name.

## Ownership Trace

- `PlayerDuelRequestService.cs` covers request, accept, reject, withdraw, start, lose, and draw packet planning with direct Java parity breadcrumbs.
- `PlayerEnterWorldService.cs` already routes duel question-response handling through the represented duel request flow.
- World-map duel flags are represented in `WorldMapRuntimeState.cs` and `WorldMapSummary.cs`.

## Remaining Risks

- This should remain a high-signal audit area because draw scheduling is still not ported and current death-workflow reporting says some duel loss and HP/MP restoration side effects are planned but not executed live.