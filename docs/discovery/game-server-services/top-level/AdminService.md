# AdminService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/AdminService.java`

## Likely C# Surface

- No obvious `Admin`-named service appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- Admin-command behavior may still be Java-only or may live outside the C# services folder.
- A detailed pass should check command handlers and privileged packet paths.