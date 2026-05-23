using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcDeathDropWorkflowServiceTests
{
	[Fact]
	public async Task HandleCustomDropDeathAsync_SchedulesRespawnRegistersDropsThenSchedulesDropAwareDecay()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var dropRegistration = new WorldNpcDropRegistrationService();
		var staticPlaceables = new StaticPlaceableStateService();
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var spawnService = CreateSpawnService(world, staticPlaceables, threadPoolManager, dropRegistration);
			var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203001, staticId: 107, respawnSeconds: 30)]);
			var templates = new NpcTemplateTable([CreateTemplate(203001)]);
			spawnService.SpawnWorldNpcs(spawns, templates, [210010000]);
			var npc = Assert.Single(world.GetNpcs());
			Assert.Equal(1, staticPlaceables.GetSpawnCount(210010000, 107));

			var registry = new CapturingConnectionRegistry();
			registry.OnlinePlayerObjectIds.Add(1001);
			var workflow = CreateDeathWorkflow(
				spawnService,
				dropRegistration,
				threadPoolManager,
				registry,
				CreateCustomDropService(203001));
			var looter = new Player { ObjectId = 1001, Race = "ELYOS", Level = 10 };

			var result = await workflow.HandleCustomDropDeathAsync(
				npc,
				looter,
				freeForAllDelay: TimeSpan.FromMilliseconds(10));

			Assert.Equal(WorldNpcDeathDropWorkflowStatus.Scheduled, result.Status);
			Assert.True(result.RespawnScheduled);
			Assert.True(spawnService.HasRespawnTask(npc.ObjectId));
			Assert.Equal(WorldNpcDropRegistrationWorkflowStatus.Registered, result.DropRegistration.Status);
			Assert.True(dropRegistration.HasRegisteredDrops(npc.ObjectId));
			Assert.True(result.DecayScheduled);
			Assert.True(spawnService.HasDecayTask(npc.ObjectId));
			Assert.True(result.StaticPlaceableDespawned);
			Assert.False(result.DeletedImmediately);
			Assert.Equal(0, staticPlaceables.GetSpawnCount(210010000, 107));
			Assert.Single(registry.SentPackets);
			var initialPacket = Assert.IsType<SmLootStatus>(registry.SentPackets[0].Packet);
			Assert.Equal(SmLootStatusType.LootEnable, initialPacket.Status);

			var remainingDecay = spawnService.CancelDecay(npc.ObjectId);
			Assert.NotNull(remainingDecay);
			Assert.True(remainingDecay.Value > TimeSpan.FromMinutes(4));

			var completed = await Task.WhenAny(
				result.DropRegistration.Fanout!.FreeForAllTask!.Completion,
				Task.Delay(TimeSpan.FromSeconds(1)));
			Assert.Same(result.DropRegistration.Fanout.FreeForAllTask.Completion, completed);
			var broadcastPacket = Assert.IsType<SmLootStatus>(registry.BroadcastPacket);
			Assert.Equal(SmLootStatusType.LootEnable, broadcastPacket.Status);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task HandleCustomDropDeathAsync_UsesImmediateDecayWhenNoDropsAreGenerated()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var dropRegistration = new WorldNpcDropRegistrationService();
		var staticPlaceables = new StaticPlaceableStateService();
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var spawnService = CreateSpawnService(world, staticPlaceables, threadPoolManager, dropRegistration);
			var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203002, respawnSeconds: 30)]);
			var templates = new NpcTemplateTable([CreateTemplate(203002)]);
			spawnService.SpawnWorldNpcs(spawns, templates, [210010000]);
			var npc = Assert.Single(world.GetNpcs());
			var registry = new CapturingConnectionRegistry();
			var workflow = CreateDeathWorkflow(
				spawnService,
				dropRegistration,
				threadPoolManager,
				registry,
				new WorldNpcCustomDropService(new CustomNpcDropTable([])));

			var result = await workflow.HandleCustomDropDeathAsync(
				npc,
				new Player { ObjectId = 1001, Race = "ELYOS", Level = 10 });

			Assert.Equal(WorldNpcDeathDropWorkflowStatus.Scheduled, result.Status);
			Assert.True(result.RespawnScheduled);
			Assert.Equal(WorldNpcDropRegistrationWorkflowStatus.NoGeneratedDrops, result.DropRegistration.Status);
			Assert.False(dropRegistration.HasRegisteredDrops(npc.ObjectId));
			Assert.True(result.DecayScheduled);
			Assert.True(spawnService.HasDecayTask(npc.ObjectId));
			Assert.False(result.StaticPlaceableDespawned);
			Assert.False(result.DeletedImmediately);
			Assert.Empty(registry.SentPackets);
			Assert.Null(registry.BroadcastPacket);

			var remainingDecay = spawnService.CancelDecay(npc.ObjectId);
			Assert.NotNull(remainingDecay);
			Assert.True(remainingDecay.Value <= TimeSpan.FromSeconds(2));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task HandleCustomDropDeathAsync_SkipsMissingNpc()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var dropRegistration = new WorldNpcDropRegistrationService();
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var spawnService = CreateSpawnService(world, new StaticPlaceableStateService(), threadPoolManager, dropRegistration);
			var workflow = CreateDeathWorkflow(
				spawnService,
				dropRegistration,
				threadPoolManager,
				new CapturingConnectionRegistry(),
				CreateCustomDropService(203001));

			var result = await workflow.HandleCustomDropDeathAsync(
				null,
				new Player { ObjectId = 1001, Race = "ELYOS", Level = 10 });

			Assert.Equal(WorldNpcDeathDropWorkflowStatus.MissingNpc, result.Status);
			Assert.Equal(WorldNpcDropRegistrationWorkflowStatus.MissingNpc, result.DropRegistration.Status);
			Assert.False(result.RespawnScheduled);
			Assert.False(result.DecayScheduled);
			Assert.False(result.StaticPlaceableDespawned);
			Assert.False(result.DeletedImmediately);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task HandleDeathAsync_SkipsRespawnWhenAiDisallowsRespawn()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var dropRegistration = new WorldNpcDropRegistrationService();
		var staticPlaceables = new StaticPlaceableStateService();
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var spawnService = CreateSpawnService(world, staticPlaceables, threadPoolManager, dropRegistration);
			var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203003, respawnSeconds: 30)]);
			var templates = new NpcTemplateTable([CreateTemplate(203003)]);
			spawnService.SpawnWorldNpcs(spawns, templates, [210010000]);
			var npc = Assert.Single(world.GetNpcs());
			var workflow = CreateDeathWorkflow(
				spawnService,
				dropRegistration,
				threadPoolManager,
				new CapturingConnectionRegistry(),
				CreateCustomDropService(203003));

			var result = await workflow.HandleDeathAsync(
				npc,
				new Player { ObjectId = 1001, Race = "ELYOS", Level = 10 },
				options: WorldNpcDeathDropOptions.Default with { AllowRespawn = false });

			Assert.Equal(WorldNpcDeathDropWorkflowStatus.Scheduled, result.Status);
			Assert.False(result.RespawnScheduled);
			Assert.False(spawnService.HasRespawnTask(npc.ObjectId));
			Assert.Equal(WorldNpcDropRegistrationWorkflowStatus.Registered, result.DropRegistration.Status);
			Assert.True(result.DecayScheduled);
			Assert.True(spawnService.HasDecayTask(npc.ObjectId));
			Assert.False(result.DeletedImmediately);
			Assert.NotNull(spawnService.CancelDecay(npc.ObjectId));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task HandleDeathAsync_SkipsDropRegistrationWhenRewardLootDisabled()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var dropRegistration = new WorldNpcDropRegistrationService();
		var staticPlaceables = new StaticPlaceableStateService();
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var spawnService = CreateSpawnService(world, staticPlaceables, threadPoolManager, dropRegistration);
			var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203004, respawnSeconds: 30)]);
			var templates = new NpcTemplateTable([CreateTemplate(203004)]);
			spawnService.SpawnWorldNpcs(spawns, templates, [210010000]);
			var npc = Assert.Single(world.GetNpcs());
			var registry = new CapturingConnectionRegistry();
			registry.OnlinePlayerObjectIds.Add(1001);
			var workflow = CreateDeathWorkflow(
				spawnService,
				dropRegistration,
				threadPoolManager,
				registry,
				CreateCustomDropService(203004));

			var result = await workflow.HandleDeathAsync(
				npc,
				new Player { ObjectId = 1001, Race = "ELYOS", Level = 10 },
				options: WorldNpcDeathDropOptions.Default with { RewardLoot = false });

			Assert.Equal(WorldNpcDeathDropWorkflowStatus.Scheduled, result.Status);
			Assert.True(result.RespawnScheduled);
			Assert.Equal(WorldNpcDropRegistrationWorkflowStatus.LootRewardDisabled, result.DropRegistration.Status);
			Assert.False(dropRegistration.HasRegisteredDrops(npc.ObjectId));
			Assert.True(result.DecayScheduled);
			Assert.True(spawnService.HasDecayTask(npc.ObjectId));
			Assert.Empty(registry.SentPackets);
			Assert.Null(registry.BroadcastPacket);

			var remainingDecay = spawnService.CancelDecay(npc.ObjectId);
			Assert.NotNull(remainingDecay);
			Assert.True(remainingDecay.Value <= TimeSpan.FromSeconds(2));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task HandleDeathAsync_DeletesImmediatelyWhenAiDisallowsDecay()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var dropRegistration = new WorldNpcDropRegistrationService();
		var staticPlaceables = new StaticPlaceableStateService();
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var spawnService = CreateSpawnService(world, staticPlaceables, threadPoolManager, dropRegistration);
			var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203005, staticId: 107, respawnSeconds: 30)]);
			var templates = new NpcTemplateTable([CreateTemplate(203005)]);
			spawnService.SpawnWorldNpcs(spawns, templates, [210010000]);
			var npc = Assert.Single(world.GetNpcs());
			Assert.Equal(1, staticPlaceables.GetSpawnCount(210010000, 107));
			var workflow = CreateDeathWorkflow(
				spawnService,
				dropRegistration,
				threadPoolManager,
				new CapturingConnectionRegistry(),
				CreateCustomDropService(203005));

			var result = await workflow.HandleDeathAsync(
				npc,
				new Player { ObjectId = 1001, Race = "ELYOS", Level = 10 },
				options: WorldNpcDeathDropOptions.Default with { RewardLoot = false, AllowDecay = false });

			Assert.Equal(WorldNpcDeathDropWorkflowStatus.Scheduled, result.Status);
			Assert.True(result.RespawnScheduled);
			Assert.True(spawnService.HasRespawnTask(npc.ObjectId));
			Assert.Equal(WorldNpcDropRegistrationWorkflowStatus.LootRewardDisabled, result.DropRegistration.Status);
			Assert.False(result.DecayScheduled);
			Assert.False(spawnService.HasDecayTask(npc.ObjectId));
			Assert.False(result.StaticPlaceableDespawned);
			Assert.True(result.DeletedImmediately);
			Assert.False(world.TryGetObject(npc.ObjectId, out _));
			Assert.Equal(0, staticPlaceables.GetSpawnCount(210010000, 107));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	private static WorldNpcDeathDropWorkflowService CreateDeathWorkflow(
		WorldNpcSpawnService spawnService,
		WorldNpcDropRegistrationService dropRegistration,
		ThreadPoolManager threadPoolManager,
		CapturingConnectionRegistry registry,
		WorldNpcCustomDropService customDropService)
	{
		var lootService = new WorldNpcLootService(dropRegistration, spawnService, threadPoolManager);
		var broadcastService = new WorldNpcLootBroadcastService(lootService, registry);
		var dropWorkflow = new WorldNpcDropRegistrationWorkflowService(customDropService, dropRegistration, broadcastService);
		return new WorldNpcDeathDropWorkflowService(spawnService, dropWorkflow);
	}

	private static WorldNpcSpawnService CreateSpawnService(
		GameWorld world,
		IStaticPlaceableStateService staticPlaceables,
		ThreadPoolManager threadPoolManager,
		WorldNpcDropRegistrationService dropRegistration)
	{
		return new WorldNpcSpawnService(
			new GameServerRuntimeContext(),
			world,
			new IDFactory(),
			gameTimeService: null,
			threadPoolManager: threadPoolManager,
			connectionRegistry: null,
			staticPlaceables: staticPlaceables,
			walkerSpawnPlans: null,
			walkerPlacementApplication: null,
			logger: NullLogger<WorldNpcSpawnService>.Instance,
			dropRegistrationLookup: dropRegistration);
	}

	private static WorldNpcCustomDropService CreateCustomDropService(int npcTemplateId)
	{
		return new WorldNpcCustomDropService(
			new CustomNpcDropTable(
			[
				new CustomNpcDropSummary(
					npcTemplateId,
					[
						new CustomDropGroupSummary(
							"custom",
							"PC_ALL",
							UseLevelBasedChanceReduction: false,
							MaxItems: 1,
							[new CustomDropSummary(166020000, 1, 1, 100f, false)]),
					]),
			]),
			chanceRoll: () => 0f);
	}

	private static NpcSpawnSummary CreateSpawn(
		int mapId,
		int npcId,
		float x = 1,
		float y = 2,
		float z = 3,
		byte heading = 0,
		int respawnSeconds = 295,
		int staticId = 0)
	{
		return new NpcSpawnSummary(
			mapId,
			npcId,
			x,
			y,
			z,
			heading,
			respawnSeconds,
			PoolSize: 0,
			DifficultId: 0,
			Handler: string.Empty,
			staticId,
			RandomWalkRange: 0,
			WalkerId: string.Empty,
			WalkerIndex: 0,
			Anchor: string.Empty,
			State: 0,
			AiName: string.Empty,
			Custom: false,
			GroupTemporarySchedule: null,
			SpotTemporarySchedule: null);
	}

	private static NpcTemplateSummary CreateTemplate(int templateId)
	{
		return new NpcTemplateSummary(
			templateId,
			$"npc-{templateId}",
			NameId: templateId,
			Level: 10,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "ELYOS",
			Tribe: "GENERAL",
			Type: "GENERAL");
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public HashSet<int> OnlinePlayerObjectIds { get; } = [];

		public List<PacketDelivery> SentPackets { get; } = [];

		public GameServerPacket? BroadcastPacket { get; private set; }

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = null;
			return false;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			if (!OnlinePlayerObjectIds.Contains(playerObjectId))
				return Task.FromResult(false);

			SentPackets.Add(new PacketDelivery(playerObjectId, packet));
			return Task.FromResult(true);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			BroadcastPacket = packet;
			return Task.FromResult(1);
		}

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
		{
			return Task.FromResult(0);
		}

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
		{
			return Task.FromResult(false);
		}

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
		{
			return Task.FromResult(false);
		}
	}

	private sealed record PacketDelivery(int PlayerObjectId, GameServerPacket Packet);
}
