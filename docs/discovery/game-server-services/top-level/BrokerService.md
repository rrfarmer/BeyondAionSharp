# BrokerService Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/BrokerService.java`

## Likely C# Surface

- `BrokerRegistrationCommissionPlanService.cs`
- `BrokerItemMaskMatcher.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- Broker-specific C# services exist, but the Java service boundary is narrower and more obvious than the current C# split.
- A detailed pass should confirm registration workflow, pricing, and message coverage.