# HTMLService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/HTMLService.java`

## Likely C# Surface

- `GuideHtmlLevelChangePlanService.cs`
- NPC dialog services may own part of the same presentation surface

## Discovery Status

- `Partial`

## High-Level Notes

- Some HTML-facing behavior exists, but there is no close general-purpose `HTMLService` counterpart in C#.
- A detailed pass should verify dialog HTML, guide pages, and packet construction.