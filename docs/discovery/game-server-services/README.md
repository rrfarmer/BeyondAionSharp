# Game-Server Services Discovery Index

Date: 2026-06-07 (index refreshed); per-area docs below still dated 2026-05-29.

## Scope Warning (read first)

This directory tracks only the Java **`services`** surface (169 files). That surface is roughly **7%** of the 2,324-file Java gameserver and roughly **4%** of the full ~4,056-file gameplay surface once content-handler scripts are counted.

Do **not** read this index as a whole-server parity view. The largest remaining work — `skillengine` (combat/effects, 292 files, 0 ported), `controllers` (61→2), `questEngine` + ~1,153 quest scripts, the 1,732 `data/handlers` content scripts, and the `model` layer (801→89) — lives outside this index entirely.

For the authoritative, full-surface picture and the modeled-vs-live distinction, see the Completion Estimate.

## Purpose

This directory tracks high-level Java-to-C# discovery for the Java game-server service surface.

The organizing rule is Java-first:

- one document per Java top-level service file under `game-server/src/com/aionemu/gameserver/services`
- one document per Java services subpackage under `game-server/src/com/aionemu/gameserver/services/*`

These documents are discovery-only. They do not claim runtime parity. A ported file is not the same as a live behavior: ~34% of C# service files are non-live `*PlanService` boundaries.

See also: [Completion Estimate](Completion-Estimate.md) — authoritative full-surface summary.
See also: [Parity-Risk Ownership Trace](Parity-Risk-Ownership-Trace.md)

## Status Legend

- `Present`: a close C# service counterpart is obvious.
- `Partial`: some C# coverage is obvious, but parity is incomplete or unclear.
- `Refactored`: the Java area appears present, but spread across multiple C# services.
- `Not obvious`: no clear C# counterpart is obvious from the current C# game-server service surface.

## Package Areas

- [abyss](packages/abyss.md)
- [antihack](packages/antihack.md)
- [autogroup](packages/autogroup.md)
- [ban](packages/ban.md)
- [conquerorAndProtectorSystem](packages/conquerorAndProtectorSystem.md)
- [craft](packages/craft.md)
- [cron](packages/cron.md)
- [drop](packages/drop.md)
- [event](packages/event.md)
- [findgroup](packages/findgroup.md)
- [instance](packages/instance.md)
- [item](packages/item.md)
- [mail](packages/mail.md)
- [panesterra](packages/panesterra.md)
- [player](packages/player.md)
- [reward](packages/reward.md)
- [rift](packages/rift.md)
- [siege](packages/siege.md)
- [summons](packages/summons.md)
- [teleport](packages/teleport.md)
- [toypet](packages/toypet.md)
- [trade](packages/trade.md)
- [transfers](packages/transfers.md)
- [vortex](packages/vortex.md)
- [worldraid](packages/worldraid.md)

## Top-Level Service Areas

- [AccountService](top-level/AccountService.md)
- [AdminService](top-level/AdminService.md)
- [AnnouncementService](top-level/AnnouncementService.md)
- [ArmsfusionService](top-level/ArmsfusionService.md)
- [AtreianPassportService](top-level/AtreianPassportService.md)
- [AutoGroupService](top-level/AutoGroupService.md)
- [BaseService](top-level/BaseService.md)
- [BonusPackService](top-level/BonusPackService.md)
- [BrokerService](top-level/BrokerService.md)
- [ChallengeTaskService](top-level/ChallengeTaskService.md)
- [ClassChangeService](top-level/ClassChangeService.md)
- [CommandsAccessService](top-level/CommandsAccessService.md)
- [CronJobService](top-level/CronJobService.md)
- [CubeExpandService](top-level/CubeExpandService.md)
- [CuringZoneService](top-level/CuringZoneService.md)
- [DatabaseCleaningService](top-level/DatabaseCleaningService.md)
- [DebugService](top-level/DebugService.md)
- [DialogService](top-level/DialogService.md)
- [DuelService](top-level/DuelService.md)
- [EnchantService](top-level/EnchantService.md)
- [ExchangeService](top-level/ExchangeService.md)
- [FactionPackService](top-level/FactionPackService.md)
- [FlyRingService](top-level/FlyRingService.md)
- [GameTimeService](top-level/GameTimeService.md)
- [HousingBidService](top-level/HousingBidService.md)
- [HousingService](top-level/HousingService.md)
- [HTMLService](top-level/HTMLService.md)
- [KiskService](top-level/KiskService.md)
- [LegionDominionService](top-level/LegionDominionService.md)
- [LegionService](top-level/LegionService.md)
- [LifeStatsRestoreService](top-level/LifeStatsRestoreService.md)
- [LimitedItemTradeService](top-level/LimitedItemTradeService.md)
- [NameRestrictionService](top-level/NameRestrictionService.md)
- [NpcShoutsService](top-level/NpcShoutsService.md)
- [PeriodicSaveService](top-level/PeriodicSaveService.md)
- [PrivateStoreService](top-level/PrivateStoreService.md)
- [PunishmentService](top-level/PunishmentService.md)
- [PvpService](top-level/PvpService.md)
- [QuestService](top-level/QuestService.md)
- [RecipeService](top-level/RecipeService.md)
- [RepurchaseService](top-level/RepurchaseService.md)
- [RespawnService](top-level/RespawnService.md)
- [RiftService](top-level/RiftService.md)
- [RoadService](top-level/RoadService.md)
- [ShieldService](top-level/ShieldService.md)
- [SiegeService](top-level/SiegeService.md)
- [SkillLearnService](top-level/SkillLearnService.md)
- [SocialService](top-level/SocialService.md)
- [StaticDoorService](top-level/StaticDoorService.md)
- [StigmaService](top-level/StigmaService.md)
- [SurveyService](top-level/SurveyService.md)
- [TownService](top-level/TownService.md)
- [TradeService](top-level/TradeService.md)
- [TribeRelationService](top-level/TribeRelationService.md)
- [UpgradeArcadeService](top-level/UpgradeArcadeService.md)
- [VortexService](top-level/VortexService.md)
- [WarehouseService](top-level/WarehouseService.md)
- [WeatherService](top-level/WeatherService.md)
- [WorldRaidService](top-level/WorldRaidService.md)

## Working Notes

- Statuses are high-level and based on current checked-in names and structure.
- Per-area docs are individually still dated 2026-05-29; their status labels are directionally usable but predate the 2026-06-07 summary refresh.
- `Not obvious` means either missed or not yet ported; this index intentionally does not try to infer intent.
- The biggest leverage now is **not** in this index — it is in `skillengine`, `controllers`, and the content handlers. Within this index, the highest-value deep dives are areas that already have plan-services needing live promotion (vortex, summons, duel) and the still-absent large-area systems (siege, panesterra, worldraid, transfers, conqueror/protector).

## At-a-Glance Signals (refreshed 2026-06-07)

### Deepest Ported Zones (login → play axis)

- enter-world + post-enter packet sequence
- item actions (enchant, manastone, idian, decompose, assemble, extract, ap-extract, remodel, charge)
- `housing` (auctions/bids/rent/visibility)
- `kisk`
- `mail` / `broker`
- friends/blocks/chat, movement broadcast
- combat/reward **formula** services (modeled, mostly non-live)

### Highest-Signal Gaps (still ~zero)

- `siege`
- `panesterra`
- `worldraid`
- `transfers`
- `conqueror/protector`
- (and outside this index: `skillengine`, `controllers`, `questEngine`, content handlers)

### Modeled-But-Not-Live (file count overstates parity)

- `vortex` (34 files, almost all plan-services)
- `summons`
- `duel`
- most `*PlanService` formula clusters