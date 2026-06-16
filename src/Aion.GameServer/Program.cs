using Aion.Commons.Database;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GameWorld = Aion.GameServer.World.World;

var builder = Host.CreateDefaultBuilder(args)
	.ConfigureAppConfiguration(
		(hostContext, config) =>
		{
			config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
			config.AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", optional: true);
			config.AddEnvironmentVariables();
		}
	)
	.ConfigureServices(
		(hostContext, services) =>
		{
			var options = GameServerOptions.LoadFromJavaConfig(AppContext.BaseDirectory);
			var databaseOptions = GameServerOptions.LoadDatabaseOptionsFromJavaConfig(AppContext.BaseDirectory);
			DatabaseFactory.Initialize(databaseOptions);
			services.AddSingleton(options);
			services.AddSingleton(databaseOptions);
			services.AddSingleton<ThreadPoolManager>();
			services.AddSingleton<IDFactory>();
			services.AddSingleton<GameServerRuntimeContext>();
			services.AddSingleton<GameWorld>();
			services.AddSingleton<GameTimeService>();
			services.AddSingleton<IHouseDoorStateService, HouseDoorStateService>();
			services.AddSingleton<IStaticPlaceableStateService, StaticPlaceableStateService>();
			// Reworked WorldNpc drop/loot mini-web (WorldNpcDropRegistrationService[+IWorldNpcDropRegistrationLookup]/
			// WorldNpcCustomDropService/WorldNpcQuestDropService/WorldNpcGlobalDropService/
			// WorldNpcEventDropRuleService/WorldNpcDropModifierService/WorldNpcDropRegistrationWorkflowService/
			// WorldNpcDeathDropWorkflowService/WorldNpcLootService/WorldNpcLootBroadcastService) retired: dead
			// closed graph referenced only by Program.cs DI + each other. Faithful home is DropService. The 3
			// reworked loot packets SmLootItemList/SmGroupLoot/SmLootStatus (faithful SM_LOOT_ITEMLIST/
			// SM_GROUP_LOOT/SM_LOOT_STATUS exist) were consumed only by WorldNpcLootService -> deleted as orphans.
			// The reworked WorldNpc spawn/walk web (WorldNpcSpawnService/WorldNpcRandomWalkService/
			// WorldNpcAiStateService + InstanceDestroyWorkflowService/InstanceEmptyInstanceCheckerService +
			// IWorldNpcDropRegistrationLookup) is likewise retired: boot NPC spawn is now the faithful
			// SpawnEngine.SpawnAll() (see GameServerBootstrapService), and faithful WalkManager/NpcAI cover
			// random/route walk + AI state. The PlayerKisk*Cleanup death/despawn Funcs survive as a separate kisk pillar.
			services.AddSingleton<PlayerVisualStatsUpdateService>();
			services.AddSingleton<PvpApRewardService>();
			services.AddSingleton<PvpInstanceApRewardService>();
			services.AddSingleton<PvpArenaApRewardService>();
			services.AddSingleton<AturamSkyFortressApRewardService>();
			services.AddSingleton<EternalBastionApRewardService>();
			services.AddSingleton<StonespearReachApRewardService>();
			// Reworked WorldNpc DP/HP web (WorldNpcResourceStatsService/WorldNpcLifeStatsService/
			// WorldNpcSoloDpRewardService/PvpDpRewardService/WorldNpcSkillResultCalculationService + the
			// shared WorldNpcEffectResourceType enum) retired: dead closed graph, no live faithful consumer.
			// Faithful DP is player.GetCommonData().AddDp; faithful HP/MP/FP is CreatureLifeStats. The two
			// Action<WorldNpc>/Action<int> npc-life-stats spawn/despawn delegates fed only WorldNpcSpawnService's
			// null-guarded optional ctor params (npcLifeStatsInitialize/npcLifeStatsClear default null) -> dropped.
			// Reworked WorldNpcSkillDamageService/Fanout/burn-island (depended on the deleted PlayerEnterWorldService god's
			// SaveIdianPolishBurn/SaveItemChargeBurn mutations; gameplay covered by faithful ItemChargeService/ChargeInfo
			// equipment-observer) removed.
			// Faithful Rift surface (services/RiftService.java, RiftManager/RiftInformer/RVController) is a static
			// singleton (RiftService.getInstance()) wired at boot via GameServerBootstrapService (Java parity:
			// GameServer.main initRiftLocations/initRifts); the reworked DI Rift*Service slop cluster + the
			// Func<int,WorldNpc,bool> rift-respawn bridge (faithful RespawnService.respawn -> RiftService.updateSpawned
			// covers it) are retired.
			services.AddSingleton<PeriodicInstanceRegistrationService>();
			// Reworked AutoGroup runtime/registration services (depended on the deleted Player*Runtime + PlayerEnterWorldService god) removed.
			// Reworked VortexLocationService removed: faithful services/VortexService.getLocationByRift/getLocationByWorld
			// (backed by DataManager.VORTEX_DATA) already covers the logic; the DI leaf had zero non-registration consumers.
			services.AddSingleton<PeriodicSaveService>();
			services.AddSingleton<LimitedItemTradeSchedulerService>();
			services.AddSingleton<HousingWorldService>();
			services.AddSingleton<Aion.GameServer.Model.GameEngine>(
				serviceProvider => serviceProvider.GetRequiredService<PeriodicSaveService>());
			services.AddSingleton<Aion.GameServer.Model.GameEngine>(
				serviceProvider => serviceProvider.GetRequiredService<LimitedItemTradeSchedulerService>());
			services.AddSingleton<Aion.GameServer.Model.GameEngine>(
				serviceProvider => serviceProvider.GetRequiredService<HousingWorldService>());
			// Boot NPC spawn is the faithful SpawnEngine.SpawnAll() (Java parity: GameServer.main), invoked
			// directly in GameServerBootstrapService after RiftService.initRiftLocations() and before
			// initRifts(). The reworked WorldNpcSpawnService GameEngine registration was removed so EXACTLY
			// ONE spawn path runs and _objects is no longer boot-populated.
			services.AddSingleton<HousingVisibilityService>();
			services.AddSingleton<NpcVisibilityService>();
			services.AddSingleton<CreaturePvpZoneCounterService>();
			services.AddSingleton<HouseAuctionTimingService>();
			services.AddSingleton<HouseMaintenanceTimingService>();
			services.AddSingleton<ShutdownHook>();
			services.AddSingleton<IStaticDataLoader, StaticDataService>();
			services.AddSingleton<Aion.GameServer.Network.LoginServer.LoginServer>();
			services.AddSingleton<Aion.GameServer.Network.ChatServer.ChatServer>();
			services.AddSingleton<IUsedIdRepository, MySqlUsedIdRepository>();
			services.AddSingleton<IServerVariablesRepository, MySqlServerVariablesRepository>();
			services.AddSingleton<ICharacterSelectionRepository, MySqlCharacterSelectionRepository>();
			services.AddSingleton<IMailRepository, MySqlMailRepository>();
			services.AddSingleton<ISocialRepository, MySqlSocialRepository>();
			services.AddSingleton<IHousingRepository, MySqlHousingRepository>();
			services.AddSingleton<IMotionRepository, MySqlMotionRepository>();
			services.AddSingleton<PlayerEnterWorldService>();
			services.AddHostedService<GameServerBootstrapService>();
			// GameClientSocketServer (reworked async god-class stack) removed; GameServerHostedService now
			// boots the faithful NioServer + GameConnectionFactoryImpl directly.
			services.AddHostedService<GameServerHostedService>();
			services.AddHostedService<OutboundLinkHostedService>();
		}
	)
	.ConfigureLogging(
		(hostContext, logging) =>
		{
			logging.ClearProviders();
			logging.AddConsole();
			if (hostContext.HostingEnvironment.IsDevelopment())
			{
				logging.AddDebug();
			}
		}
	);

var host = builder.Build();

// Singleton-bridge wiring (see docs/HANDOFF.md "SINGLETON-BRIDGE"): bind DI-created engine
// services to their Java-style static accessors so per-instance domain objects (Creature,
// AggroList, life/game stats, ...) can reach them exactly as Java's getInstance() does.
ThreadPoolManager.RegisterInstance(host.Services.GetRequiredService<ThreadPoolManager>());

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Aion Game Server starting...");

try
{
	await host.RunAsync();
}
finally
{
	DatabaseFactory.Dispose();
	logger.LogInformation("Aion Game Server stopped.");
}
