# CronJobService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/CronJobService.java`

## Likely C# Surface

- `JavaCronSchedule.cs`
- `JavaQuartzCronExpression.cs`
- `ExpirableTaskService.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- C# has schedule-parsing and task primitives, but the Java cron job service boundary is not obvious.
- A detailed pass should verify orchestration, ownership, and callback timing.