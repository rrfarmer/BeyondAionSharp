# mail Package Discovery

Date: 2026-05-29

## Java Surface

- `game-server/src/com/aionemu/gameserver/services/mail`
- 6 Java files.

## Likely C# Surface

- `MailSendCostPlanService.cs`
- `SystemMailRewardPlanService.cs`
- `SystemMailRewardPersistencePlanService.cs`
- `MailRepository.cs`
- `HouseAuctionRepository.cs`

## Discovery Status

- `Partial`

## High-Level Notes

- System-mail and reward-persistence behavior is obvious in C#, but the full Java mail surface is not mirrored by name.

## Ownership Trace

- `MailRepository.cs` contains explicit Java parity breadcrumbs for `SystemMailService.sendMail` letter and attached-item persistence.
- `HouseAuctionRepository.cs` contains explicit Java parity breadcrumbs for `MailFormatter.sendHouseAuctionMail(..., AuctionResult.FAILED_BID, ...)` and uses system-mail insertion during auction refund flow.
- Online mailbox update behavior is also wired through the game-server socket server.

## Remaining Risks

- A detailed pass should confirm formatter behavior, auction-result mail breadth, and siege-result mail coverage.