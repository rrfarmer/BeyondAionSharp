# SkillLearnService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/SkillLearnService.java`

## Likely C# Surface

- `SkillLearnService.cs`
- `MotionLearnService.cs`
- `EmotionLearnService.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- Core skill-learning behavior is present, but the broader Java service boundary may now be split by content type.
- A detailed pass should confirm stigma, motion, emotion, and class-based unlock interactions.