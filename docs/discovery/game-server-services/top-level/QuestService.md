# QuestService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/QuestService.java`

## Likely C# Surface

- the `Quest*` service cluster

## Discovery Status

- `Refactored`

## High-Level Notes

- Quest behavior is clearly present in C#, but spread across completion, reward, dialog, persistence, and XP planning services.
- A detailed pass should verify start, update, finish, bonus reward, and persistence ownership.