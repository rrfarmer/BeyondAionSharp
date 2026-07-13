# cron Package Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/cron`
- 6 Java files.

## Likely C# Surface

- `JavaCronSchedule.cs`
- `JavaQuartzCronExpression.cs`
- `ExpirableTaskService.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- The C# side has cron-expression and scheduling primitives, but the Java cron package shape is not mirrored directly.
- A detailed pass should verify runnable dispatch, execution semantics, and scheduler ownership.