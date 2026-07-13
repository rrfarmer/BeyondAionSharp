# AccountService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/AccountService.java`

## Likely C# Surface

- `PlayerAccountRuntimeStateService.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- Some account-facing runtime behavior is visible in C#, but no close `AccountService` equivalent is obvious.
- A detailed pass should also inspect repositories and cross-process account flows.