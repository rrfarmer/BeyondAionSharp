# instance Package Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/instance`
- 3 Java files.

## Likely C# Surface

- `InstanceRuntimeService.cs`
- `InstanceEntranceCooldownService.cs`
- `InstanceCooldownRateService.cs`

## Discovery Status

- `Present`

## High-Level Notes

- Instance behavior has a close C# surface and appears fairly direct.
- A detailed pass should confirm periodic-instance management and arena-specific logic.