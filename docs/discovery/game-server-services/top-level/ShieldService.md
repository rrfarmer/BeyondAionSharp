# ShieldService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/ShieldService.java`

## Likely C# Surface

- `ShieldEffectPacketPlanService.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- Shield-facing packet planning exists, but there is no obvious broad `ShieldService` counterpart.
- A detailed pass should confirm damage interception, effect state, and removal behavior.