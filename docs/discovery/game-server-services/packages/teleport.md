# teleport Package Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/teleport`
- 3 Java files.

## Likely C# Surface

- `PlayerTeleportService.cs`
- `TeleportTransportationPricePlanService.cs`
- the `BindPointTeleport*` cluster
- portal-entry services such as `PortalEntryInteractionService.cs`

## Discovery Status

- `Refactored`

## High-Level Notes

- Teleport behavior is present, but it has been decomposed heavily in C# into request, price, runtime, packet, and persistence steps.
- A detailed pass should verify plain teleport, portal interaction, and bind-point teleport separately.