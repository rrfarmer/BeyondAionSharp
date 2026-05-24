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
	public async Task HandleDeathAsync_MarksAiDiedBeforeDecayCleanup()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var dropRegistration = new WorldNpcDropRegistrationService();
		var staticPlaceables = new StaticPlaceableStateService();
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var spawnService = CreateSpawnService(world, staticPlaceables, threadPoolManager, dropRegistration);
			var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203006, respawnSeconds: 30)]);
			var templates = new NpcTemplateTable([CreateTemplate(203006)]);
			spawnService.SpawnWorldNpcs(spawns, templates, [210010000]);
			var npc = Assert.Single(world.GetNpcs());
			var aiStates = new WorldNpcAiStateService();
			aiStates.StartRandomWalking(npc.ObjectId);
			var workflow = CreateDeathWorkflow(
				spawnService,
				dropRegistration,
				threadPoolManager,
				new CapturingConnectionRegistry(),
				CreateCustomDropService(203006),
				aiStates);

			var result = await workflow.HandleDeathAsync(
				npc,
				new Player { ObjectId = 1001, Race = "ELYOS", Level = 10 },
				options: WorldNpcDeathDropOptions.Default with { RewardLoot = false });

			Assert.True(result.AiMarkedDied);
			Assert.True(aiStates.TryGetState(npc.ObjectId, out var state));
			Assert.Equal(WorldNpcAiState.Died, state!.State);
			Assert.Equal(WorldNpcAiSubState.None, state.SubState);
			Assert.True(result.DecayScheduled);
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

	[Fact]
	public async Task HandleDeathAsync_RemovesRuntimeKiskAndRunsMemberCleanup()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var dropRegistration = new WorldNpcDropRegistrationService();
		var staticPlaceables = new StaticPlaceableStateService();
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			const int kiskObjectId = 1;
			var spawnService = CreateSpawnService(world, staticPlaceables, threadPoolManager, dropRegistration);
			var runtimeContext = new GameServerRuntimeContext();
			var registry = runtimeContext.Kisks;
			var idFactory = new IDFactory([kiskObjectId]);
			var kiskState = new PlayerKiskRuntimeState(
				objectId: kiskObjectId,
				ownerObjectId: 1001,
				npcId: 700273);
			Assert.True(kiskState.AddMember(1001));
			Assert.True(kiskState.AddMember(1002));
			registry.RegisterKisk(kiskState);
			Assert.True(world.TryAddObject(kiskObjectId, CreateKiskNpc(kiskObjectId, 700273)));

			var creator = CreateOnlinePlayer(1001, boundKiskObjectId: kiskObjectId);
			var deadMember = CreateOnlinePlayer(1002, boundKiskObjectId: kiskObjectId);
			deadMember.CreatureState = PlayerCreatureState.Dead;
			var pendingResponder = CreateOnlinePlayer(1003, boundKiskObjectId: 0);
			var pendingKiskBindRequest = new PendingKiskBindRequest(kiskObjectId, SmQuestionWindow.RegisterBindstone);
			pendingResponder.PendingKiskBindRequest = pendingKiskBindRequest;
			Assert.True(pendingResponder.ResponseRequester.PutRequest(
				SmQuestionWindow.RegisterBindstone,
				new QuestionResponseRequest(kiskObjectId, QuestionResponseRequestKind.KiskBind, pendingKiskBindRequest)));
			var connectionRegistry = new CapturingConnectionRegistry();
			connectionRegistry.OnlinePlayers.AddRange([creator, deadMember, pendingResponder]);
			connectionRegistry.OnlinePlayerObjectIds.UnionWith([1001, 1002, 1003]);
			var zoneCounterService = new CreaturePvpZoneCounterService();
			zoneCounterService.EnterZone(kiskObjectId, CreaturePvpZoneCounterType.Pvp);
			zoneCounterService.EnterZone(kiskObjectId, CreaturePvpZoneCounterType.Siege);

			var workflow = CreateDeathWorkflow(
				spawnService,
				dropRegistration,
				threadPoolManager,
				connectionRegistry,
				CreateCustomDropService(700273),
				kiskDeathCleanup: (npc, _) =>
					ValueTask.FromResult(PlayerKiskDeathCleanupService.TryRemoveDiedKisk(npc, world, registry, idFactory)),
				kiskRemovalCleanup: (despawn, cancellationToken) =>
					PlayerKiskRemovalRuntimeCleanupService.ApplyAsync(
						despawn,
						connectionRegistry,
						runtimeContext,
						world,
						cancellationToken,
						zoneCounterService));

			Assert.True(world.TryGetObject(kiskObjectId, out var kiskObject));
			var result = await workflow.HandleDeathAsync(
				Assert.IsAssignableFrom<IWorldNpcObject>(kiskObject),
				creator);

			Assert.Equal(WorldNpcDeathDropWorkflowStatus.KiskRemoved, result.Status);
			Assert.Equal(WorldNpcDropRegistrationWorkflowStatus.KiskRemoved, result.DropRegistration.Status);
			Assert.True(result.DeletedImmediately);
			Assert.False(result.RespawnScheduled);
			Assert.False(result.DecayScheduled);
			Assert.False(spawnService.HasRespawnTask(kiskObjectId));
			Assert.False(spawnService.HasDecayTask(kiskObjectId));
			Assert.NotNull(result.KiskDespawn);
			Assert.True(result.KiskDespawn.RemovedRegistry);
			Assert.True(result.KiskDespawn.RemovedWorldObject);
			Assert.True(result.KiskDespawn.ReleasedObjectId);
			Assert.Equal(210010000, result.KiskDespawn.WorldId);
			Assert.NotNull(result.KiskRemovalCleanup);
			Assert.True(result.KiskRemovalCleanup.Applied);
			Assert.Equal(1, result.KiskRemovalCleanup.CreatorUpdatesSent);
			Assert.Equal(2, result.KiskRemovalCleanup.BindPointResetsSent);
			Assert.Equal(1, result.KiskRemovalCleanup.DeathOptionRefreshesSent);
			Assert.Equal(2, result.KiskRemovalCleanup.ClearedBoundMembers);
			Assert.Equal(1, result.KiskRemovalCleanup.ClearedPendingRequests);
			Assert.Equal(1, result.KiskRemovalCleanup.NpcVisibilityRefreshes);
			Assert.Equal(1, result.KiskRemovalCleanup.ClearedZoneCounters);
			Assert.Equal(CreaturePvpZoneCounters.Empty, zoneCounterService.GetCounters(kiskObjectId));
			Assert.False(registry.HaveKisk(1001));
			Assert.False(world.TryGetObject(kiskObjectId, out _));
			Assert.Equal(kiskObjectId, idFactory.NextId());
			Assert.Equal(0, creator.BoundKiskObjectId);
			Assert.Equal(0, deadMember.BoundKiskObjectId);
			Assert.Null(pendingResponder.PendingKiskBindRequest);
			Assert.Equal(0, pendingResponder.ResponseRequester.Count);
			Assert.Contains(connectionRegistry.SentPackets, delivery => delivery.PlayerObjectId == 1001 && delivery.Packet is SmKiskUpdate);
			Assert.Equal(2, connectionRegistry.SentPackets.Count(delivery => delivery.Packet is SmBindPointInfo));
			Assert.Single(connectionRegistry.SentPackets, delivery => delivery.PlayerObjectId == 1002 && delivery.Packet is SmDie);
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
		WorldNpcCustomDropService customDropService,
		WorldNpcAiStateService? aiStates = null,
		Func<IWorldNpcObject, CancellationToken, ValueTask<PlayerKiskDespawnResult?>>? kiskDeathCleanup = null,
		Func<PlayerKiskDespawnResult, CancellationToken, ValueTask<PlayerKiskRemovalRuntimeCleanupResult>>? kiskRemovalCleanup = null)
	{
		var lootService = new WorldNpcLootService(dropRegistration, spawnService, threadPoolManager);
		var broadcastService = new WorldNpcLootBroadcastService(lootService, registry);
		var dropWorkflow = new WorldNpcDropRegistrationWorkflowService(customDropService, dropRegistration, broadcastService);
		return new WorldNpcDeathDropWorkflowService(
			spawnService,
			dropWorkflow,
			aiStates,
			kiskDeathCleanup,
			kiskRemovalCleanup);
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

	private static WorldNpc CreateKiskNpc(int objectId, int npcId)
	{
		var template = new NpcTemplateSummary(
			npcId,
			"test_kisk",
			NameId: npcId + 100,
			Level: 10,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "PC_LIGHT_CASTLE_DOOR",
			Tribe: "KISK",
			Type: "NPC",
			MaxHp: 1000,
			Height: 2.5f,
			BoundRadius: 1.2f,
			State: WorldNpcState.DefaultSpawnState);
		return new WorldNpc(
			objectId,
			npcId,
			template,
			new WorldPosition(210010000, 1, 2, 3, 4));
	}

	private static Player CreateOnlinePlayer(int objectId, int boundKiskObjectId)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = $"player-{objectId}",
			Race = "ELYOS",
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
			BoundKiskObjectId = boundKiskObjectId,
			LifeStats = new PlayerLifeStats(CurrentHp: 100, CurrentMp: 100, CurrentFp: 100),
		};
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public HashSet<int> OnlinePlayerObjectIds { get; } = [];

		public List<Player> OnlinePlayers { get; } = [];

		public List<PacketDelivery> SentPackets { get; } = [];

		public GameServerPacket? BroadcastPacket { get; private set; }

		public int NpcVisibilityRefreshes { get; private set; }

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
			foreach (var player in OnlinePlayers)
				action(player);
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
			NpcVisibilityRefreshes++;
			return Task.FromResult(1);
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
