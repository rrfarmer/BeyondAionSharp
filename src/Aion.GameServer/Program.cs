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
			services.AddSingleton<WorldNpcAiStateService>();
			services.AddSingleton<WorldNpcDropRegistrationService>();
			services.AddSingleton<IWorldNpcDropRegistrationLookup>(
				serviceProvider => serviceProvider.GetRequiredService<WorldNpcDropRegistrationService>());
			services.AddSingleton<WorldNpcCustomDropService>();
			services.AddSingleton<WorldNpcQuestDropService>();
			services.AddSingleton<WorldNpcGlobalDropService>();
			services.AddSingleton<WorldNpcEventDropRuleService>();
			services.AddSingleton<WorldNpcDropModifierService>();
			services.AddSingleton<WorldNpcDropRegistrationWorkflowService>();
			services.AddSingleton<WorldNpcDeathDropWorkflowService>(
				serviceProvider => new WorldNpcDeathDropWorkflowService(
					serviceProvider.GetRequiredService<WorldNpcSpawnService>(),
					serviceProvider.GetRequiredService<WorldNpcDropRegistrationWorkflowService>(),
					serviceProvider.GetService<WorldNpcAiStateService>(),
					(npc, _) =>
						ValueTask.FromResult(
							PlayerKiskDeathCleanupService.TryRemoveDiedKisk(
								npc,
								serviceProvider.GetRequiredService<GameWorld>(),
								serviceProvider.GetRequiredService<GameServerRuntimeContext>().Kisks,
								serviceProvider.GetService<IDFactory>())),
					(despawn, cancellationToken) =>
						PlayerKiskRemovalRuntimeCleanupService.ApplyAsync(
							despawn,
							serviceProvider.GetRequiredService<GameServerRuntimeContext>(),
							serviceProvider.GetRequiredService<GameWorld>(),
							cancellationToken,
							serviceProvider.GetService<CreaturePvpZoneCounterService>())));
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
			services.AddSingleton<WorldNpcLootService>();
			services.AddSingleton<WorldNpcLootBroadcastService>();
			services.AddSingleton<WorldNpcRandomWalkService>();
			services.AddSingleton<Func<int, bool>>(
				serviceProvider => objectId => serviceProvider.GetRequiredService<WorldNpcSpawnService>().CancelRespawn(objectId));
			// Faithful Rift surface (services/RiftService.java, RiftManager/RiftInformer/RVController) is a static
			// singleton (RiftService.getInstance()) wired at boot via GameServerBootstrapService (Java parity:
			// GameServer.main initRiftLocations/initRifts); the reworked DI Rift*Service slop cluster + the
			// Func<int,WorldNpc,bool> rift-respawn bridge (faithful RespawnService.respawn -> RiftService.updateSpawned
			// covers it) are retired.
			services.AddSingleton<InstanceDestroyWorkflowService>();
			services.AddSingleton<InstanceEmptyInstanceCheckerService>();
			services.AddSingleton<PeriodicInstanceRegistrationService>();
			// Reworked AutoGroup runtime/registration services (depended on the deleted Player*Runtime + PlayerEnterWorldService god) removed.
			services.AddSingleton<VortexLocationService>();
			services.AddSingleton<PeriodicSaveService>();
			services.AddSingleton<LimitedItemTradeSchedulerService>();
			services.AddSingleton<HousingWorldService>();
			services.AddSingleton<WorldNpcSpawnService>();
			services.AddSingleton<Aion.GameServer.Model.GameEngine>(
				serviceProvider => serviceProvider.GetRequiredService<PeriodicSaveService>());
			services.AddSingleton<Aion.GameServer.Model.GameEngine>(
				serviceProvider => serviceProvider.GetRequiredService<LimitedItemTradeSchedulerService>());
			services.AddSingleton<Aion.GameServer.Model.GameEngine>(
				serviceProvider => serviceProvider.GetRequiredService<HousingWorldService>());
			services.AddSingleton<Aion.GameServer.Model.GameEngine>(
				serviceProvider => serviceProvider.GetRequiredService<WorldNpcSpawnService>());
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
