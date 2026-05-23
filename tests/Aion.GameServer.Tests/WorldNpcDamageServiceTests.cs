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

public sealed class WorldNpcDamageServiceTests
{
	[Fact]
	public async Task ApplyDamageAsync_ReducesSpawnedNpcHpViaLifeStats()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out var lifeStats, out var threadPoolManager, out _, out var combatStates, out _, out _, out var registry);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203090, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await damageService.ApplyDamageAsync(npc, CreatePlayer(), damage: 25);

			Assert.Equal(WorldNpcDamageStatus.Damaged, result.Status);
			Assert.Equal(25, result.Damage);
			Assert.True(result.NotifyAttack);
			Assert.NotNull(result.LifeStats);
			Assert.NotNull(result.AttackStatusPacket);
			Assert.Equal(25, result.AttackStatusPacket.Value);
			Assert.Equal(75, result.AttackStatusPacket.HpOrMpPercentage);
			Assert.Equal(SmAttackStatusType.Regular, result.AttackStatusPacket.Type);
			Assert.Equal(1, result.AttackStatusBroadcastCount);
			var broadcast = Assert.Single(registry.Broadcasts);
			Assert.Same(result.AttackStatusPacket, broadcast.Packet);
			Assert.True(broadcast.IncludeSourcePlayer);
			Assert.NotNull(result.CombatState);
			Assert.Equal(1, result.CombatState.AttackedCount);
			var aggro = Assert.Single(result.CombatState.HateEntries);
			Assert.Equal(1001, aggro.AttackerObjectId);
			Assert.Equal(25, aggro.Damage);
			Assert.Equal(25, aggro.Hate);
			Assert.True(aggro.NotifyAttack);
			Assert.Equal(WorldNpcDamageHopType.Damage, aggro.HopType);
			Assert.NotNull(result.AttackedObserverNotification);
			Assert.Equal(npc.ObjectId, result.AttackedObserverNotification.NpcObjectId);
			Assert.Equal(1001, result.AttackedObserverNotification.AttackerObjectId);
			Assert.Equal(0, result.AttackedObserverNotification.SkillId);
			Assert.NotNull(result.CastingInterrupt);
			Assert.Equal(WorldNpcCastingInterruptStatus.NoCastingSkill, result.CastingInterrupt.Status);
			Assert.NotNull(result.SupportAiRequests);
			Assert.Empty(result.SupportAiRequests);
			Assert.Equal(WorldNpcLifeStatsDamageStatus.Reduced, result.LifeStats.Status);
			Assert.Equal(new WorldNpcLifeStats(100, 0, 75, 0), result.LifeStats.Current);
			Assert.True(lifeStats.TryGetStats(npc.ObjectId, out var stored));
			Assert.Equal(75, stored!.CurrentHp);
			Assert.True(combatStates.TryGetState(npc.ObjectId, out var storedCombat));
			Assert.Equal(result.CombatState.NpcObjectId, storedCombat!.NpcObjectId);
			Assert.Equal(result.CombatState.AttackedCount, storedCombat.AttackedCount);
			Assert.Equal(result.CombatState.HateEntries, storedCombat.HateEntries);
			Assert.False(spawnService.HasRespawnTask(npc.ObjectId));
			Assert.False(spawnService.HasDecayTask(npc.ObjectId));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageAsync_TriggersDeathWorkflowForLethalDamage()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out var aiStates, out _, out _, out _, out var registry);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203091, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await damageService.ApplyDamageAsync(
				npc,
				CreatePlayer(),
				damage: 150,
				new WorldNpcDamageOptions(
					NotifyAttack: true,
					DeathOptions: WorldNpcDeathDropOptions.Default with { RewardLoot = false }));

			Assert.Equal(WorldNpcDamageStatus.Died, result.Status);
			Assert.NotNull(result.LifeStats);
			Assert.NotNull(result.AttackStatusPacket);
			Assert.Equal(100, result.AttackStatusPacket.Value);
			Assert.Equal(0, result.AttackStatusPacket.HpOrMpPercentage);
			Assert.Equal(1, result.AttackStatusBroadcastCount);
			Assert.Single(registry.Broadcasts);
			Assert.Equal(WorldNpcLifeStatsDamageStatus.Died, result.LifeStats.Status);
			Assert.Equal(new WorldNpcLifeStats(100, 0, 0, 0), result.LifeStats.Current);
			Assert.NotNull(result.LifeStats.DeathResult);
			Assert.True(result.LifeStats.DeathResult.RespawnScheduled);
			Assert.True(result.LifeStats.DeathResult.DecayScheduled);
			Assert.True(result.LifeStats.DeathResult.AiMarkedDied);
			Assert.True(spawnService.HasRespawnTask(npc.ObjectId));
			Assert.True(spawnService.HasDecayTask(npc.ObjectId));
			Assert.True(aiStates.TryGetState(npc.ObjectId, out var state));
			Assert.Equal(WorldNpcAiState.Died, state!.State);

			var duplicate = await damageService.ApplyDamageAsync(
				npc,
				CreatePlayer(),
				damage: 1,
				new WorldNpcDamageOptions(
					NotifyAttack: true,
					DeathOptions: WorldNpcDeathDropOptions.Default with { RewardLoot = false }));

			Assert.Equal(WorldNpcDamageStatus.AlreadyDead, duplicate.Status);
			Assert.NotNull(duplicate.LifeStats);
			Assert.Equal(WorldNpcLifeStatsDamageStatus.AlreadyDead, duplicate.LifeStats.Status);
			Assert.Null(duplicate.LifeStats.DeathResult);
			Assert.Equal(1, spawnService.PendingDecayCount);
			Assert.NotNull(spawnService.CancelDecay(npc.ObjectId));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageAsync_CancelsCastingSkillBeforeDamageWorkflow()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out _, out var castingInterrupts, out _);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203099, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());
			castingInterrupts.SetCastingSkill(
				npc.ObjectId,
				new WorldNpcCastingSkill(
					3001,
					WorldNpcSkillMethod.Item,
					CancelRate: 0));

			var result = await damageService.ApplyDamageAsync(npc, CreatePlayer(), damage: 10);

			Assert.NotNull(result.CastingInterrupt);
			Assert.Equal(WorldNpcCastingInterruptStatus.ItemSkillCanceled, result.CastingInterrupt.Status);
			Assert.True(result.CastingInterrupt.Canceled);
			Assert.Equal(3001, result.CastingInterrupt.Skill?.SkillId);
			Assert.False(castingInterrupts.TryGetCastingSkill(npc.ObjectId, out _));
			Assert.NotNull(result.AttackedObserverNotification);
			Assert.Equal(WorldNpcDamageStatus.Damaged, result.Status);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageEffectAsync_MapsRegularDamageEffectToNpcDamageOptions()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203100, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await skillDamageService.ApplyDamageEffectAsync(new WorldNpcSkillDamageRequest(
				Target: npc,
				Effector: CreatePlayer(),
				Damage: 20,
				SkillId: 5001));

			Assert.Equal(WorldNpcSkillDamageKind.RegularDamageEffect, result.Kind);
			Assert.Equal(WorldNpcDamageStatus.Damaged, result.DamageResult.Status);
			Assert.NotNull(result.DamageResult.AttackStatusPacket);
			Assert.Equal(SmAttackStatusType.Regular, result.DamageResult.AttackStatusPacket.Type);
			Assert.Equal(SmAttackStatusLog.Regular, result.DamageResult.AttackStatusPacket.Log);
			Assert.Equal(5001, result.DamageResult.AttackStatusPacket.SkillId);
			Assert.NotNull(result.AttackObserverNotification);
			Assert.Equal(1001, result.AttackObserverNotification.EffectorObjectId);
			Assert.Equal(npc.ObjectId, result.AttackObserverNotification.TargetObjectId);
			Assert.Equal(5001, result.AttackObserverNotification.SkillId);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageEffectAsync_MapsProvokedDamageEffectToDamageTypeWithoutAttackObserver()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203101, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await skillDamageService.ApplyDamageEffectAsync(new WorldNpcSkillDamageRequest(
				Target: npc,
				Effector: CreatePlayer(),
				Damage: 20,
				SkillId: 5002,
				Kind: WorldNpcSkillDamageKind.ProvokedDamageEffect));

			Assert.Equal(WorldNpcSkillDamageKind.ProvokedDamageEffect, result.Kind);
			Assert.Equal(WorldNpcDamageStatus.Damaged, result.DamageResult.Status);
			Assert.NotNull(result.DamageResult.AttackStatusPacket);
			Assert.Equal(SmAttackStatusType.Damage, result.DamageResult.AttackStatusPacket.Type);
			Assert.Equal(SmAttackStatusLog.ProcAttackInstant, result.DamageResult.AttackStatusPacket.Log);
			Assert.Equal(5002, result.DamageResult.AttackStatusPacket.SkillId);
			Assert.Null(result.AttackObserverNotification);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageEffectAsync_MapsPeriodicSpellAttackToDotObserver()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203102, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await skillDamageService.ApplyDamageEffectAsync(new WorldNpcSkillDamageRequest(
				Target: npc,
				Effector: CreatePlayer(),
				Damage: 15,
				SkillId: 5003,
				Kind: WorldNpcSkillDamageKind.PeriodicSpellAttack));

			Assert.Equal(WorldNpcSkillDamageKind.PeriodicSpellAttack, result.Kind);
			Assert.Equal(WorldNpcDamageStatus.Damaged, result.DamageResult.Status);
			Assert.False(result.DamageResult.NotifyAttack);
			Assert.NotNull(result.DamageResult.AttackStatusPacket);
			Assert.Equal(SmAttackStatusType.Damage, result.DamageResult.AttackStatusPacket.Type);
			Assert.Equal(SmAttackStatusLog.SpellAttack, result.DamageResult.AttackStatusPacket.Log);
			Assert.Equal(5003, result.DamageResult.AttackStatusPacket.SkillId);
			Assert.Null(result.AttackObserverNotification);
			Assert.NotNull(result.DotAttackedObserverNotification);
			Assert.Equal(1001, result.DotAttackedObserverNotification.EffectorObjectId);
			Assert.Equal(npc.ObjectId, result.DotAttackedObserverNotification.TargetObjectId);
			Assert.Equal(5003, result.DotAttackedObserverNotification.SkillId);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageEffectAsync_MapsSpellAttackDrainToAttackObserverAndDrainAmounts()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203103, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await skillDamageService.ApplyDamageEffectAsync(new WorldNpcSkillDamageRequest(
				Target: npc,
				Effector: CreatePlayer(),
				Damage: 20,
				SkillId: 5004,
				Kind: WorldNpcSkillDamageKind.SpellAttackDrain,
				Options: new WorldNpcSkillDamageOptions(
					HpDrainPercent: 50,
					MpDrainPercent: 25)));

			Assert.Equal(WorldNpcSkillDamageKind.SpellAttackDrain, result.Kind);
			Assert.Equal(WorldNpcDamageStatus.Damaged, result.DamageResult.Status);
			Assert.True(result.DamageResult.NotifyAttack);
			Assert.NotNull(result.DamageResult.AttackStatusPacket);
			Assert.Equal(SmAttackStatusType.Damage, result.DamageResult.AttackStatusPacket.Type);
			Assert.Equal(SmAttackStatusLog.SpellAttackDrain, result.DamageResult.AttackStatusPacket.Log);
			Assert.Equal(5004, result.DamageResult.AttackStatusPacket.SkillId);
			Assert.NotNull(result.AttackObserverNotification);
			Assert.Null(result.DotAttackedObserverNotification);
			Assert.NotNull(result.DrainResult);
			Assert.Equal(10, result.DrainResult.HpAmount);
			Assert.Equal(5, result.DrainResult.MpAmount);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageAsync_RecordsAttackedObserverAndSupportAiEvents()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out var combatEvents, out _, out _);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203095, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());
			AddWorldNpc(world, objectId: 9001, templateId: 203096, position: npc.Position with { X = npc.Position.X + 20 });
			AddWorldNpc(world, objectId: 9002, templateId: 203097, position: npc.Position with { X = npc.Position.X + 200 });
			AddWorldNpc(world, objectId: 9003, templateId: 203098, position: npc.Position with { WorldId = 220010000 });

			var result = await damageService.ApplyDamageAsync(
				npc,
				CreatePlayer(),
				damage: 10,
				new WorldNpcDamageOptions(
					NotifyAttack: true,
					SkillId: 1234));

			Assert.Equal(WorldNpcDamageStatus.Damaged, result.Status);
			Assert.NotNull(result.AttackedObserverNotification);
			Assert.Equal(npc.ObjectId, result.AttackedObserverNotification.NpcObjectId);
			Assert.Equal(1001, result.AttackedObserverNotification.AttackerObjectId);
			Assert.Equal(1234, result.AttackedObserverNotification.SkillId);
			var supportRequest = Assert.Single(result.SupportAiRequests!);
			Assert.Equal(9001, supportRequest.SupportNpcObjectId);
			Assert.Equal(npc.ObjectId, supportRequest.AttackedNpcObjectId);
			Assert.Equal(WorldNpcAiEventType.CreatureNeedsSupport, supportRequest.EventType);
			Assert.True(result.AttackedObserverNotification.Sequence < supportRequest.Sequence);
			Assert.True(combatEvents.TryGetState(npc.ObjectId, out var eventState));
			Assert.Equal(result.AttackedObserverNotification, Assert.Single(eventState!.AttackedObserverNotifications));
			Assert.Equal(supportRequest, Assert.Single(eventState.SupportAiRequests));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageAsync_ReturnsNotSpawnedBeforeCreatingStats()
	{
		var damageService = CreateDamageService(out _, out _, out var lifeStats, out var threadPoolManager, out _, out var combatStates, out var combatEvents, out var castingInterrupts, out _);
		try
		{
			var npc = CreateWorldNpc(objectId: 77, maxHp: 100);

			var result = await damageService.ApplyDamageAsync(npc, CreatePlayer(), damage: 10);

			Assert.Equal(WorldNpcDamageStatus.NotSpawned, result.Status);
			Assert.Null(result.LifeStats);
			Assert.False(lifeStats.TryGetStats(npc.ObjectId, out _));
			Assert.False(combatStates.TryGetState(npc.ObjectId, out _));
			Assert.False(combatEvents.TryGetState(npc.ObjectId, out _));
			Assert.False(castingInterrupts.TryGetCastingSkill(npc.ObjectId, out _));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageAsync_ReturnsMissingAttackerWithoutReducingHp()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out var lifeStats, out var threadPoolManager, out _, out var combatStates, out var combatEvents, out var castingInterrupts, out var registry);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203092, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await damageService.ApplyDamageAsync(npc, attacker: null, damage: 10);

			Assert.Equal(WorldNpcDamageStatus.MissingAttacker, result.Status);
			Assert.Null(result.LifeStats);
			Assert.Null(result.AttackStatusPacket);
			Assert.Empty(registry.Broadcasts);
			Assert.True(lifeStats.TryGetStats(npc.ObjectId, out var stored));
			Assert.Equal(100, stored!.CurrentHp);
			Assert.False(combatStates.TryGetState(npc.ObjectId, out _));
			Assert.False(combatEvents.TryGetState(npc.ObjectId, out _));
			Assert.False(castingInterrupts.TryGetCastingSkill(npc.ObjectId, out _));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageAsync_ReturnsMissingLifeStatsWhenMaxHpUnavailable()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out var lifeStats, out var threadPoolManager, out _, out var combatStates, out var combatEvents, out var castingInterrupts, out _);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203093, maxHp: 0);
			var npc = Assert.Single(world.GetNpcs());

			var result = await damageService.ApplyDamageAsync(npc, CreatePlayer(), damage: 10);

			Assert.Equal(WorldNpcDamageStatus.MissingLifeStats, result.Status);
			Assert.Null(result.LifeStats);
			Assert.False(lifeStats.TryGetStats(npc.ObjectId, out _));
			Assert.False(combatStates.TryGetState(npc.ObjectId, out _));
			Assert.False(combatEvents.TryGetState(npc.ObjectId, out _));
			Assert.False(castingInterrupts.TryGetCastingSkill(npc.ObjectId, out _));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	private static WorldNpcDamageService CreateDamageService(
		out WorldNpcSpawnService spawnService,
		out GameWorld world,
		out WorldNpcLifeStatsService lifeStats,
		out ThreadPoolManager threadPoolManager,
		out WorldNpcAiStateService aiStates,
		out WorldNpcCombatStateService combatStates,
		out WorldNpcCombatEventService combatEvents,
		out WorldNpcCastingInterruptService castingInterrupts,
		out CapturingConnectionRegistry registry)
	{
		world = new GameWorld(NullLogger<GameWorld>.Instance);
		var dropRegistration = new WorldNpcDropRegistrationService();
		threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		aiStates = new WorldNpcAiStateService();
		var stagedCombatStates = new WorldNpcCombatStateService();
		var stagedCombatEvents = new WorldNpcCombatEventService();
		var stagedCastingInterrupts = new WorldNpcCastingInterruptService();
		combatStates = stagedCombatStates;
		combatEvents = stagedCombatEvents;
		castingInterrupts = stagedCastingInterrupts;
		var staticPlaceables = new StaticPlaceableStateService();
		registry = new CapturingConnectionRegistry();
		WorldNpcLifeStatsService? stagedLifeStats = null;
		spawnService = new WorldNpcSpawnService(
			new GameServerRuntimeContext(),
			world,
			new IDFactory(),
			gameTimeService: null,
			threadPoolManager,
			connectionRegistry: null,
			staticPlaceables,
			walkerSpawnPlans: null,
			walkerPlacementApplication: null,
			NullLogger<WorldNpcSpawnService>.Instance,
			dropRegistrationLookup: dropRegistration,
			npcAiStates: aiStates,
			npcLifeStatsInitialize: npc => stagedLifeStats!.Initialize(npc, npc.Template.MaxHp),
			npcLifeStatsClear: objectId =>
			{
				stagedLifeStats!.Clear(objectId);
				stagedCombatStates.Clear(objectId);
				stagedCombatEvents.Clear(objectId);
				stagedCastingInterrupts.Clear(objectId);
			});
		var lootService = new WorldNpcLootService(dropRegistration, spawnService, threadPoolManager);
		var broadcastService = new WorldNpcLootBroadcastService(lootService, registry);
		var dropWorkflow = new WorldNpcDropRegistrationWorkflowService(
			new WorldNpcCustomDropService(new CustomNpcDropTable([])),
			dropRegistration,
			broadcastService);
		var deathWorkflow = new WorldNpcDeathDropWorkflowService(spawnService, dropWorkflow, aiStates);
		stagedLifeStats = new WorldNpcLifeStatsService(deathWorkflow);
		lifeStats = stagedLifeStats;
		return new WorldNpcDamageService(world, lifeStats, registry, combatStates, combatEvents, castingInterrupts);
	}

	private static void SpawnNpc(WorldNpcSpawnService spawnService, GameWorld world, int npcTemplateId, int maxHp)
	{
		var spawns = new NpcSpawnTable([CreateSpawn(210010000, npcTemplateId, respawnSeconds: 30)]);
		var templates = new NpcTemplateTable([CreateTemplate(npcTemplateId, maxHp)]);
		spawnService.SpawnWorldNpcs(spawns, templates, [210010000]);
		Assert.Single(world.GetNpcs());
	}

	private static WorldNpc CreateWorldNpc(int objectId, int maxHp)
	{
		return new WorldNpc(
			objectId,
			203094,
			CreateTemplate(203094, maxHp),
				new WorldPosition(210010000, 1, 2, 3, 0));
	}

	private static WorldNpc AddWorldNpc(GameWorld world, int objectId, int templateId, WorldPosition position)
	{
		var npc = new WorldNpc(
			objectId,
			templateId,
			CreateTemplate(templateId, maxHp: 100),
			position);
		Assert.True(world.TryAddObject(objectId, npc));
		return npc;
	}

	private static Player CreatePlayer()
	{
		return new Player { ObjectId = 1001, Race = "ELYOS", Level = 10 };
	}

	private static NpcSpawnSummary CreateSpawn(
		int mapId,
		int npcId,
		int respawnSeconds)
	{
		return new NpcSpawnSummary(
			mapId,
			npcId,
			X: 1,
			Y: 2,
			Z: 3,
			Heading: 0,
			RespawnSeconds: respawnSeconds,
			PoolSize: 0,
			DifficultId: 0,
			Handler: string.Empty,
			StaticId: 0,
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

	private static NpcTemplateSummary CreateTemplate(int templateId, int maxHp)
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
			Type: "GENERAL",
			MaxHp: maxHp);
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<BroadcastRecord> Broadcasts { get; } = [];

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
			return Task.FromResult(false);
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
			Broadcasts.Add(new BroadcastRecord(sourcePosition, sourceObjectId, packet, includeSourcePlayer));
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

	private sealed record BroadcastRecord(
		WorldPosition SourcePosition,
		int SourceObjectId,
		GameServerPacket Packet,
		bool IncludeSourcePlayer);
}
