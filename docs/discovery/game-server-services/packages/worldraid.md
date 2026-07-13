# worldraid Package Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/worldraid`
- 2 Java files.

## Likely C# Surface

- No obvious `WorldRaid`-named service appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- This looks like another high-signal gap because the Java package is explicit while the C# services surface shows no clear counterpart.
- A detailed pass should check reward, raid, and world-event code for renamed coverage.