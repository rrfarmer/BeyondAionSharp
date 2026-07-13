# AnnouncementService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/AnnouncementService.java`

## Likely C# Surface

- No obvious `Announcement`-named service appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- Announcement broadcasting is not obvious in the current C# service naming surface.
- A detailed pass should inspect packet factories and GM/system message helpers.