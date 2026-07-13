# NameRestrictionService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/NameRestrictionService.java`

## Likely C# Surface

- No obvious `NameRestriction`-named service appears under `Aion.GameServer.Services`.

## Discovery Status

- `Not obvious`

## High-Level Notes

- Name-validation behavior may live outside services or may not yet be ported.
- A detailed pass should inspect character creation, rename, and pet/item naming paths.