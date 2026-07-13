# player Package Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/player`
- 9 Java files.

## Likely C# Surface

- `PlayerEnterWorldService.cs`
- the `PlayerRevive*`, `PlayerKisk*`, `PlayerKnownList*`, `PlayerGroup*`, and `PlayerAlliance*` clusters
- broader player runtime helpers across the `Player*` service family

## Discovery Status

- `Refactored`

## High-Level Notes

- Java player services have been decomposed heavily in C# and now span many smaller orchestration services.
- A detailed pass should verify enter-world, leave-world, revive, mailbox, chat, and limit-control parity separately.