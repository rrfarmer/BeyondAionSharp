# siege Package Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/siege`
- 14 Java files.

## Likely C# Surface

- No obvious `Siege`-named service cluster appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- This is a high-signal gap because the Java siege package is large and distinct while the C# services surface does not show a matching cluster.
- A detailed pass should inspect abyss, reward, and faction-control code for any hidden or renamed siege work.