# SocialService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/SocialService.java`

## Likely C# Surface

- the `PlayerGroup*`, `PlayerAlliance*`, and `PlayerLeague*` service families

## Discovery Status

- `Refactored`

## High-Level Notes

- Social coordination appears spread across party, alliance, and league runtime services.
- A detailed pass should verify friend, block, and non-group social behavior separately.