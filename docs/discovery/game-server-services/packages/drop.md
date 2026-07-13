# drop Package Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/drop`
- 3 Java files.

## Likely C# Surface

- `WorldNpcDropRegistrationService.cs`
- `WorldNpcDropRegistrationWorkflowService.cs`
- `WorldNpcDeathDropWorkflowService.cs`
- related drop helpers such as `WorldNpcGlobalDropService.cs`

## Discovery Status

- `Refactored`

## High-Level Notes

- Java drop services appear to have moved into the broader C# NPC death/drop workflow surface.
- A detailed pass should confirm registration timing, distribution rules, and packet fanout.