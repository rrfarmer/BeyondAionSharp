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
							serviceProvider.GetService<IGameClientConnectionRegistry>(),
							serviceProvider.GetRequiredService<GameServerRuntimeContext>(),
							serviceProvider.GetRequiredService<GameWorld>(),
							cancellationToken,
							serviceProvider.GetService<CreaturePvpZoneCounterService>())));
			services.AddSingleton<WorldNpcLifeStatsService>();
			services.AddSingleton<WorldNpcResourceStatsService>();
			services.AddSingleton<PlayerVisualStatsUpdateService>();
			services.AddSingleton<CustomLevelRewardExecutionService>();
			services.AddSingleton<WorldNpcSoloDpRewardService>();
			services.AddSingleton<WorldNpcTeamApRewardService>();
			services.AddSingleton<PvpApRewardService>();
			services.AddSingleton<PvpDpRewardService>();
			services.AddSingleton<PvpInstanceApRewardService>();
			services.AddSingleton<PvpArenaApRewardService>();
			services.AddSingleton<AturamSkyFortressApRewardService>();
			services.AddSingleton<EternalBastionApRewardService>();
			services.AddSingleton<StonespearReachApRewardService>();
			services.AddSingleton<WorldNpcCombatStateService>();
			services.AddSingleton<WorldNpcCombatEventService>();
			services.AddSingleton<WorldNpcCastingInterruptService>();
			services.AddSingleton<WorldNpcSkillResultCalculationService>();
			services.AddSingleton<Action<WorldNpc>>(
				serviceProvider => npc =>
				{
					var lifeStats = serviceProvider.GetRequiredService<WorldNpcLifeStatsService>();
					if (npc.Template.MaxHp > 0)
						lifeStats.Initialize(npc, npc.Template.MaxHp);
					else
						lifeStats.Clear(npc.ObjectId);
				});
			services.AddSingleton<Action<int>>(
				serviceProvider => objectId =>
				{
					serviceProvider.GetRequiredService<WorldNpcLifeStatsService>().Clear(objectId);
					serviceProvider.GetRequiredService<WorldNpcCombatStateService>().Clear(objectId);
					serviceProvider.GetRequiredService<WorldNpcCombatEventService>().Clear(objectId);
					serviceProvider.GetRequiredService<WorldNpcCastingInterruptService>().Clear(objectId);
				});
			services.AddSingleton<WorldNpcDamageService>();
			// Reworked WorldNpcSkillDamageService/Fanout (depended on the deleted PlayerEnterWorldService god's
			// SaveIdianPolishBurn/SaveItemChargeBurn mutations) removed.
			services.AddSingleton<EquipmentObserverBurnFanoutService>();
			services.AddSingleton<PlayerIncomingDamageObserverFanoutService>();
			services.AddSingleton<WorldNpcLootService>();
			services.AddSingleton<WorldNpcLootBroadcastService>();
			services.AddSingleton<WorldNpcRandomWalkService>();
			services.AddSingleton<Func<int, bool>>(
				serviceProvider => objectId => serviceProvider.GetRequiredService<WorldNpcSpawnService>().CancelRespawn(objectId));
			services.AddSingleton<Func<int, WorldNpc, bool>>(
				serviceProvider => (oldObjectId, respawn) => serviceProvider.GetRequiredService<RiftService>().UpdateSpawned(oldObjectId, respawn));
			services.AddSingleton<RiftManagerService>();
			services.AddSingleton<RiftService>();
			services.AddSingleton<RiftInformerService>();
			services.AddSingleton<RiftScheduleService>();
			services.AddSingleton<InstanceDestroyWorkflowService>();
			services.AddSingleton<InstanceEmptyInstanceCheckerService>();
			services.AddSingleton<PeriodicInstanceRegistrationService>();
			// Reworked AutoGroup runtime/registration services (depended on the deleted Player*Runtime + PlayerEnterWorldService god) removed.
			services.AddSingleton<RiftPortalDialogService>();
			services.AddSingleton<RiftPortalUseService>();
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
			services.AddSingleton<Aion.GameServer.Model.GameEngine>(
				serviceProvider => serviceProvider.GetRequiredService<RiftScheduleService>());
			services.AddSingleton<HousingVisibilityService>();
			services.AddSingleton<NpcVisibilityService>();
			services.AddSingleton<CreaturePvpZoneCounterService>();
			services.AddSingleton<HouseAuctionTimingService>();
			services.AddSingleton<HouseMaintenanceTimingService>();
			services.AddSingleton<ShutdownHook>();
			services.AddSingleton<IStaticDataLoader, StaticDataService>();
			services.AddSingleton(
				serviceProvider => new GamePacketProcessor<string>(
					(packet, cancellationToken) =>
					{
						var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Aion.GameServer.Network.Aion.GamePacketProcessor");
						logger.LogDebug("Parsed game client packet 0x{Opcode:X3}", packet.OpCode);
						return Task.CompletedTask;
					}));
			services.AddSingleton<Aion.GameServer.Network.LoginServer.LoginServer>();
			services.AddSingleton<Aion.GameServer.Network.ChatServer.ChatServer>();
			services.AddSingleton<IUsedIdRepository, MySqlUsedIdRepository>();
			services.AddSingleton<IServerVariablesRepository, MySqlServerVariablesRepository>();
			services.AddSingleton<ICharacterSelectionRepository, MySqlCharacterSelectionRepository>();
			services.AddSingleton<ICharacterCreationRepository, MySqlCharacterCreationRepository>();
			services.AddSingleton<IPlayerEnterWorldRepository, MySqlPlayerEnterWorldRepository>();
			services.AddSingleton<IMailRepository, MySqlMailRepository>();
			services.AddSingleton<ICustomLevelRewardRepository, MySqlCustomLevelRewardRepository>();
			services.AddSingleton<IBrokerRepository, MySqlBrokerRepository>();
			services.AddSingleton<ISocialRepository, MySqlSocialRepository>();
			services.AddSingleton<IHouseAuctionRepository, MySqlHouseAuctionRepository>();
			services.AddSingleton<IHousingRepository, MySqlHousingRepository>();
			services.AddSingleton<IMotionRepository, MySqlMotionRepository>();
			services.AddSingleton<CharacterCreationService>();
			services.AddSingleton<PlayerEnterWorldService>();
			services.AddHostedService<GameServerBootstrapService>();
			// GameClientSocketServer (reworked async god-class stack) removed; GameServerHostedService now
			// boots the faithful NioServer + GameConnectionFactoryImpl directly.
			services.AddHostedService<GameServerHostedService>();
			services.AddHostedService<GameBridgeHostedService>();
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
