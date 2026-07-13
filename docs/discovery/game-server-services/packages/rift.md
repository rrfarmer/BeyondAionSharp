# rift Package Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/rift`
- 4 Java files.

## Likely C# Surface

- `RiftService.cs`
- `RiftManagerService.cs`
- `RiftInformerService.cs`
- portal-related services such as `RiftPortalUseService.cs`

## Discovery Status

- `Refactored`

## High-Level Notes

- The core rift service area is clearly present in C#, although some ownership is split into portal services.
- A detailed pass should verify opening schedules, informer behavior, and portal interaction rules.