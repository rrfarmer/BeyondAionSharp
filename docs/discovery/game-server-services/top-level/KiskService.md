# KiskService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/KiskService.java`

## Likely C# Surface

- the `PlayerKisk*` service cluster

## Discovery Status

- `Refactored`

## High-Level Notes

- Kisk behavior is clearly present in C#, but decomposed into bind, spawn, revive, cleanup, and packet services.
- A detailed pass should verify authorization, lifetime, and resurrection parity.