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

// NOTE: the working directory is re-rooted to <repo>/game-server inside GameServerBootstrapService.StartAsync (the
// boot entry shared by the host and the integration tests) so relative-path lookups ("./config/schedule/*.xml",
// "./data/handlers/instance", HTMLCache "./data/static_data/HTML") resolve exactly as in Java regardless of launcher.

var builder = Host.CreateDefaultBuilder(args)
	.ConfigureAppConfiguration(
		(hostContext, config) =>
		{
			config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
			config.AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", optional: true);
			config.AddEnvironmentVariables();
		}
	)
	.ConfigureHostOptions(
		options =>
		{
			// Java parity: ShutdownHook.run() persists gameplay data during shutdown with no host-imposed deadline
			// (player save via NioServer.Shutdown, then PeriodicSaveService.onShutdown -> legion warehouses +
			// serverLastRun, then GameTimeService.saveGameTime — see GameServerBootstrapService.StopAsync). The .NET
			// host cancels graceful shutdown after HostOptions.ShutdownTimeout, which would truncate those saves: the
			// sequential StopAsync chain already spends up to ~5s in NioServer.Shutdown's player-disconnect wait alone,
			// before the legion-warehouse write (which scales with legion count) even begins. Raise the deadline well
			// past that worst case so the gameplay-data persistence steps always run to completion.
			options.ShutdownTimeout = TimeSpan.FromSeconds(60);
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
			// Reworked *ApRewardService stand-ins (PvpApRewardService/PvpInstanceApRewardService/
			// PvpArenaApRewardService/AturamSkyFortressApRewardService/EternalBastionApRewardService/
			// StonespearReachApRewardService) retired: invented Service+Result+Status blow-ups with NO Java
			// counterpart class, dead-island (referenced only by Program.cs DI + each own file, 0 production/test
			// consumers). The faithful AP-reward logic now lives 1:1 in the ported instance handlers
			// (AturamSkyFortressInstance/EternalBastionInstance/StonespearReachInstance onDie -> AbyssPointsService.AddAp);
			// the Pvp* ones had no faithful counterpart at all. Files deleted as orphans.
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
			// PeriodicSaveService is now the faithful Java singleton (services/PeriodicSaveService.getInstance()),
			// wired at boot in GameServerBootstrapService (GameServer.main:156); the reworked DI GameEngine
			// registration was retired.
			services.AddSingleton<LimitedItemTradeSchedulerService>();
			services.AddSingleton<Aion.GameServer.Model.GameEngine>(
				serviceProvider => serviceProvider.GetRequiredService<LimitedItemTradeSchedulerService>());
			// NOTE: the faithful engines (QuestEngine/AIEngine/InstanceEngine/ChatProcessor/ZoneService/GeoService)
			// are NOT registered as DI GameEngine singletons — DI would eagerly construct them when the bootstrap's
			// IEnumerable<GameEngine> is injected, i.e. BEFORE DataManager.RegisterInstance runs, and their
			// ctors/holders touch DataManager. They are instead initialized inline in GameServerBootstrapService
			// (Java parity: GameServer.main:100-101, after DataManager.getInstance()).
			// Boot NPC spawn is the faithful SpawnEngine.SpawnAll() (Java parity: GameServer.main), invoked
			// directly in GameServerBootstrapService after RiftService.initRiftLocations() and before
			// initRifts(). The reworked WorldNpcSpawnService GameEngine registration was removed so EXACTLY
			// ONE spawn path runs and _objects is no longer boot-populated. Houses enter the same _allObjects
			// store via the faithful SpawnEngine -> HousingService.SpawnHouses path (the reworked
			// HousingWorldService/HousingVisibilityService parallel object subsystem was retired).
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
			services.AddSingleton<IMotionRepository, MySqlMotionRepository>();
			services.AddSingleton<PlayerEnterWorldService>();
			services.AddHostedService<GameServerBootstrapService>();
			// GameClientSocketServer (reworked async god-class stack) removed; GameServerHostedService now
			// boots the faithful NioServer + GameConnectionFactoryImpl directly.
			services.AddHostedService<GameServerHostedService>();
			services.AddHostedService<OutboundLinkHostedService>();
			// Admin HTTP endpoint (opt-in via gameserver.admin.api.*) used by the external web portal to send
			// mail through the live SystemMailService instead of writing to the game DB directly.
			services.AddHostedService<Aion.GameServer.Services.Admin.AdminHttpService>();
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
