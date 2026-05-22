using Aion.Commons.Database;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
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
			services.AddSingleton<IWorldNpcWalkerSpawnPlanCacheService, WorldNpcWalkerSpawnPlanCacheService>();
			services.AddSingleton<WorldNpcWalkerPlacementApplicationService>();
			services.AddSingleton<WorldNpcWalkerRouteService>();
			services.AddSingleton<WorldNpcWalkerMovementStateService>();
			services.AddSingleton<WorldNpcWalkerMovementBroadcastService>();
			services.AddSingleton<WorldNpcAiStateService>();
			services.AddSingleton<WorldNpcDropRegistrationService>();
			services.AddSingleton<IWorldNpcDropRegistrationLookup>(
				serviceProvider => serviceProvider.GetRequiredService<WorldNpcDropRegistrationService>());
			services.AddSingleton<WorldNpcCustomDropService>();
			services.AddSingleton<WorldNpcLootService>();
			services.AddSingleton<WorldNpcLootBroadcastService>();
			services.AddSingleton<WorldNpcRandomWalkService>();
			services.AddSingleton<WorldNpcWalkerRouteWalkingService>();
			services.AddSingleton<Func<int, bool>>(
				serviceProvider => objectId => serviceProvider.GetRequiredService<WorldNpcSpawnService>().CancelRespawn(objectId));
			services.AddSingleton<Func<int, WorldNpc, bool>>(
				serviceProvider => (oldObjectId, respawn) => serviceProvider.GetRequiredService<RiftService>().UpdateSpawned(oldObjectId, respawn));
			services.AddSingleton<RiftManagerService>();
			services.AddSingleton<RiftService>();
			services.AddSingleton<RiftInformerService>();
			services.AddSingleton<RiftScheduleService>();
			services.AddSingleton<RiftPortalDialogService>();
			services.AddSingleton<RiftPortalUseService>();
			services.AddSingleton<VortexLocationService>();
			services.AddSingleton<PeriodicSaveService>();
			services.AddSingleton<HousingWorldService>();
			services.AddSingleton<WorldNpcSpawnService>();
			services.AddSingleton<Aion.GameServer.Model.GameEngine>(
				serviceProvider => serviceProvider.GetRequiredService<PeriodicSaveService>());
			services.AddSingleton<Aion.GameServer.Model.GameEngine>(
				serviceProvider => serviceProvider.GetRequiredService<HousingWorldService>());
			services.AddSingleton<Aion.GameServer.Model.GameEngine>(
				serviceProvider => serviceProvider.GetRequiredService<WorldNpcSpawnService>());
			services.AddSingleton<Aion.GameServer.Model.GameEngine>(
				serviceProvider => serviceProvider.GetRequiredService<RiftScheduleService>());
			services.AddSingleton<ExpirableTaskService>();
			services.AddSingleton<HousingVisibilityService>();
			services.AddSingleton<NpcVisibilityService>();
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
			services.AddSingleton<IBrokerRepository, MySqlBrokerRepository>();
			services.AddSingleton<ISocialRepository, MySqlSocialRepository>();
			services.AddSingleton<IHouseAuctionRepository, MySqlHouseAuctionRepository>();
			services.AddSingleton<IHousingRepository, MySqlHousingRepository>();
			services.AddSingleton<IMotionRepository, MySqlMotionRepository>();
			services.AddSingleton<CharacterCreationService>();
			services.AddSingleton<PlayerEnterWorldService>();
			services.AddHostedService<GameServerBootstrapService>();
			services.AddSingleton<GameClientSocketServer>();
			services.AddSingleton<IGameClientConnectionRegistry>(
				serviceProvider => serviceProvider.GetRequiredService<GameClientSocketServer>());
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
