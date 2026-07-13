# ban Package Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/ban`
- 3 Java files.

## Likely C# Surface

- No obvious `Ban`-named service appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- Chat and hardware-ban behavior is not obviously represented in the current C# game-server service surface.
- A detailed pass should also inspect chat-server and login-server boundaries before calling this fully missing.