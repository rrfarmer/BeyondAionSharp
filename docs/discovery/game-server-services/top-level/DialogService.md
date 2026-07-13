# DialogService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/DialogService.java`

## Likely C# Surface

- `DialogActionRegistry.cs`
- the `NpcDialog*` service cluster

## Discovery Status

- `Refactored`

## High-Level Notes

- Java dialog behavior appears to have been decomposed in C# into controller, validation, targeting, and packet-planning services.
- A detailed pass should verify trade, quest, and limited-item branches.