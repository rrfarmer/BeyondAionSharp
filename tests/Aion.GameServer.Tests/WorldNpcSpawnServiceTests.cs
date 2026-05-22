using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcSpawnServiceTests
{
	[Fact]
	public void SpawnWorldNpcs_MaterializesOnlySupportedRegularSpawns()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = CreateService(world);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203000, x: 10, y: 20, z: 30, heading: 40, staticId: 107, randomWalkRange: 7, walkerId: "path-a", walkerIndex: 3, anchor: "anchor-a"),
			CreateSpawn(210010000, 400001),
			CreateSpawn(210010000, 203001, handler: "STATIC"),
			CreateSpawn(210010000, 203002, poolSize: 2),
			CreateSpawn(300030000, 203003),
			CreateSpawn(210010000, 299999),
			CreateSpawn(210010000, 203004, groupTemporarySchedule: TemporarySpawnSchedule.FromAttributes(null, "9.*.*", "10.*.*")),
		]);
		var templates = new NpcTemplateTable(
		[
			CreateTemplate(203000),
			CreateTemplate(203001),
			CreateTemplate(203002),
			CreateTemplate(203003),
			CreateTemplate(203004),
		]);

		var result = service.SpawnWorldNpcs(spawns, templates, [210010000]);

		Assert.Equal(new WorldNpcSpawnResult(2, 5), result);
		Assert.True(world.TryGetObject(1, out var gameObject));
		var npc = Assert.IsType<WorldNpc>(gameObject);
		Assert.Equal(203000, npc.TemplateId);
		Assert.Equal(new global::Aion.GameServer.World.WorldPosition(210010000, 10, 20, 30, 40), npc.Position);
		Assert.Equal(WorldNpcState.DefaultSpawnState, npc.State);
		Assert.Equal(295, npc.RespawnSeconds);
		Assert.Equal(107, npc.StaticId);
		Assert.Equal(7, npc.RandomWalkRange);
		Assert.Equal("path-a", npc.WalkerId);
		Assert.Equal(3, npc.WalkerIndex);
		Assert.Equal("anchor-a", npc.Anchor);
		Assert.Equal(npc.Position, npc.SpawnLocation);
		Assert.Equal(2, world.GetNpcs().Count);
		Assert.Contains(world.GetNpcs(), worldNpc => worldNpc.ObjectId == npc.ObjectId);
		Assert.Equal(2, world.GetNpcs(210010000).Count);
		Assert.Empty(world.GetNpcs(220010000));
	}

	[Fact]
	public async Task SpawnWorldNpcs_RefreshesWalkerSpawnPlanCacheFromLiveWorldNpcs()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walker-cache-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextWithWalkerDataAsync(tempPath);
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var walkerPlans = new WorldNpcWalkerSpawnPlanCacheService();
			var service = new WorldNpcSpawnService(
				context,
				world,
				new IDFactory(),
				gameTimeService: null,
				threadPoolManager: null,
				connectionRegistry: null,
				staticPlaceables: null,
				walkerSpawnPlans: walkerPlans,
				walkerPlacementApplication: new WorldNpcWalkerPlacementApplicationService(),
				NullLogger<WorldNpcSpawnService>.Instance);
			var spawns = new NpcSpawnTable(
			[
				CreateSpawn(210010000, 203080, x: 0, y: 0, walkerId: "route-a", walkerIndex: 1),
				CreateSpawn(210010000, 203080, x: 0, y: 0, walkerId: "route-a", walkerIndex: 2),
			]);
			var templates = new NpcTemplateTable([CreateTemplate(203080)]);

			var result = service.SpawnWorldNpcs(spawns, templates, [210010000]);

			Assert.Equal(new WorldNpcSpawnResult(2, 0), result);
			var worldPlan = walkerPlans.GetWorldPlan(210010000);
			Assert.NotNull(worldPlan);
			var formation = Assert.Single(worldPlan.SpawnPlan.Formations);
			Assert.Equal("route-a", formation.RouteId);
			Assert.Equal([2, 1], formation.Members.Select(member => member.ObjectId).ToArray());
			Assert.Equal([2, 1], worldPlan.PlacementPlan.ActivePlacements.Select(placement => placement.ObjectId).ToArray());
			Assert.Empty(worldPlan.PlacementPlan.InactiveVariantObjectIds);
			var formedNpcs = world.GetNpcs()
				.OfType<WorldNpc>()
				.OrderBy(npc => npc.ObjectId)
				.ToArray();
			Assert.Equal(-1, formedNpcs[0].Position.Y, precision: 4);
			Assert.Equal(1, formedNpcs[1].Position.Y, precision: 4);
			Assert.Equal(0, formedNpcs[0].SpawnLocation.Y, precision: 4);
			Assert.Equal(0, formedNpcs[1].SpawnLocation.Y, precision: 4);
		}
		finally
		{
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task SpawnWorldNpcs_HidesInactiveWalkerVersionVariants()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walker-variants-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextWithVersionedWalkerDataAsync(tempPath);
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var walkerPlans = new WorldNpcWalkerSpawnPlanCacheService(
				new WorldNpcWalkerFormationOrganizerService(),
				new WorldNpcWalkerVariantSelectionService(count => count - 1));
			var service = new WorldNpcSpawnService(
				context,
				world,
				new IDFactory(),
				gameTimeService: null,
				threadPoolManager: null,
				connectionRegistry: null,
				staticPlaceables: null,
				walkerSpawnPlans: walkerPlans,
				walkerPlacementApplication: new WorldNpcWalkerPlacementApplicationService(),
				NullLogger<WorldNpcSpawnService>.Instance);
			var spawns = new NpcSpawnTable(
			[
				CreateSpawn(210010000, 203081, x: 1, walkerId: "route-v1", walkerIndex: 0),
				CreateSpawn(210010000, 203082, x: 2, walkerId: "route-v2", walkerIndex: 0),
			]);
			var templates = new NpcTemplateTable([CreateTemplate(203081), CreateTemplate(203082)]);

			var result = service.SpawnWorldNpcs(spawns, templates, [210010000]);

			Assert.Equal(new WorldNpcSpawnResult(2, 0), result);
			var liveNpc = Assert.Single(world.GetNpcs().OfType<WorldNpc>());
			Assert.Equal(2, liveNpc.ObjectId);
			Assert.False(world.TryGetObject(1, out _));
			Assert.True(service.TryGetInactiveWalkerVariant(1, out var inactiveNpc));
			Assert.NotNull(inactiveNpc);
			Assert.Equal("route-v1", inactiveNpc.WalkerId);
			Assert.Equal(1, service.InactiveWalkerVariantCount);
			var worldPlan = walkerPlans.GetWorldPlan(210010000);
			Assert.NotNull(worldPlan);
			Assert.Equal([1], worldPlan.PlacementPlan.InactiveVariantObjectIds);
		}
		finally
		{
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task TrySwapInactiveWalkerVariant_SpawnsParkedVariantAndParksCurrentVariant()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walker-swap-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextWithVersionedWalkerDataAsync(tempPath);
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var walkerPlans = new WorldNpcWalkerSpawnPlanCacheService(
				new WorldNpcWalkerFormationOrganizerService(),
				new WorldNpcWalkerVariantSelectionService(count => count - 1));
			var service = new WorldNpcSpawnService(
				context,
				world,
				new IDFactory(),
				gameTimeService: null,
				threadPoolManager: null,
				connectionRegistry: null,
				staticPlaceables: null,
				walkerSpawnPlans: walkerPlans,
				walkerPlacementApplication: new WorldNpcWalkerPlacementApplicationService(),
				NullLogger<WorldNpcSpawnService>.Instance);
			var spawns = new NpcSpawnTable(
			[
				CreateSpawn(210010000, 203081, x: 1, y: 10, walkerId: "route-v1", walkerIndex: 0),
				CreateSpawn(210010000, 203082, x: 2, y: 20, walkerId: "route-v2", walkerIndex: 0),
			]);
			var templates = new NpcTemplateTable([CreateTemplate(203081), CreateTemplate(203082)]);

			service.SpawnWorldNpcs(spawns, templates, [210010000]);
			var swapped = service.TrySwapInactiveWalkerVariant(activeObjectId: 2, inactiveObjectId: 1);

			Assert.True(swapped);
			Assert.True(world.TryGetObject(1, out var activatedObject));
			var activatedNpc = Assert.IsType<WorldNpc>(activatedObject);
			Assert.Equal("route-v1", activatedNpc.WalkerId);
			Assert.Equal(1, activatedNpc.Position.X);
			Assert.Equal(10, activatedNpc.Position.Y);
			Assert.False(world.TryGetObject(2, out _));
			Assert.True(service.TryGetInactiveWalkerVariant(2, out var parkedNpc));
			Assert.NotNull(parkedNpc);
			Assert.Equal("route-v2", parkedNpc.WalkerId);
			Assert.Equal(1, service.InactiveWalkerVariantCount);
		}
		finally
		{
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task SpawnWorldNpcs_UpdatesStaticPlaceableState()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var staticPlaceables = new StaticPlaceableStateService();
		var service = CreateService(world, staticPlaceables);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203070, x: 1, staticId: 107, groupTemporarySchedule: TemporarySpawnSchedule.FromAttributes(null, "2.*.*", "3.*.*")),
		]);
		var templates = new NpcTemplateTable([CreateTemplate(203070)]);

		var startup = service.SpawnWorldNpcs(spawns, templates, [210010000], gameMinutes: 2 * 60, serverDayOfWeek: DayOfWeek.Friday);
		Assert.Equal(new WorldNpcSpawnResult(1, 0), startup);
		Assert.Equal(1, staticPlaceables.GetSpawnCount(210010000, 107));

		var despawn = await service.ProcessTemporarySpawnHourChangeAsync(spawns, templates, [210010000], gameMinutes: 3 * 60, serverDayOfWeek: DayOfWeek.Friday);

		Assert.Equal(new TemporarySpawnHourChangeResult(0, 1, 1), despawn);
		Assert.Equal(0, staticPlaceables.GetSpawnCount(210010000, 107));
	}

	[Fact]
	public void TryDespawnWorldNpc_ClearsWorldStateStaticPlaceableAndObjectId()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var staticPlaceables = new StaticPlaceableStateService();
		var service = CreateService(world, staticPlaceables);
		var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203071, staticId: 107)]);
		var templates = new NpcTemplateTable([CreateTemplate(203071)]);

		var result = service.SpawnWorldNpcs(spawns, templates, [210010000]);
		var despawned = service.TryDespawnWorldNpc(1);

		Assert.Equal(new WorldNpcSpawnResult(1, 0), result);
		Assert.True(despawned);
		Assert.False(world.TryGetObject(1, out _));
		Assert.Equal(0, staticPlaceables.GetSpawnCount(210010000, 107));
		service.SpawnWorldNpcs(spawns, templates, [210010000]);
		Assert.True(world.TryGetObject(1, out _));
	}

	[Fact]
	public async Task TryDeleteAndScheduleRespawn_RestoresNpcAfterRespawnDelay()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var staticPlaceables = new StaticPlaceableStateService();
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var service = CreateService(world, staticPlaceables, threadPoolManager);
			var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203072, staticId: 107, respawnSeconds: 1)]);
			var templates = new NpcTemplateTable([CreateTemplate(203072)]);

			var result = service.SpawnWorldNpcs(spawns, templates, [210010000]);
			var deleted = service.TryDeleteAndScheduleRespawn(1);

			Assert.Equal(new WorldNpcSpawnResult(1, 0), result);
			Assert.True(deleted);
			Assert.True(service.HasRespawnTask(1));
			Assert.Equal(1, service.PendingRespawnCount);
			Assert.Empty(world.GetNpcs());
			Assert.Equal(0, staticPlaceables.GetSpawnCount(210010000, 107));

			await WaitUntilAsync(() => service.PendingRespawnCount == 0 && world.GetNpcs().Count == 1);

			var respawnedNpc = Assert.Single(world.GetNpcs());
			Assert.Equal(1, respawnedNpc.ObjectId);
			Assert.Equal(203072, respawnedNpc.TemplateId);
			Assert.Equal(1, staticPlaceables.GetSpawnCount(210010000, 107));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task TryScheduleWorldNpcDeath_DecaysCorpseAndRespawnsFromOriginalSpawn()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var staticPlaceables = new StaticPlaceableStateService();
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var service = CreateService(world, staticPlaceables, threadPoolManager);
			var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203074, x: 5, staticId: 107, respawnSeconds: 1)]);
			var templates = new NpcTemplateTable([CreateTemplate(203074)]);

			service.SpawnWorldNpcs(spawns, templates, [210010000]);
			var scheduledDeath = service.TryScheduleWorldNpcDeath(1, hasRegisteredDrops: false, decayDelay: TimeSpan.FromMilliseconds(50));

			Assert.True(scheduledDeath);
			Assert.True(service.HasRespawnTask(1));
			Assert.Equal(1, service.PendingRespawnCount);
			Assert.Single(world.GetNpcs());
			Assert.Equal(0, staticPlaceables.GetSpawnCount(210010000, 107));

			await WaitUntilAsync(() => world.GetNpcs().Count == 0 && service.HasRespawnTask(1));
			await WaitUntilAsync(() => service.PendingRespawnCount == 0 && world.GetNpcs().Count == 1);

			var respawnedNpc = Assert.Single(world.GetNpcs());
			Assert.Equal(1, respawnedNpc.ObjectId);
			Assert.Equal(203074, respawnedNpc.TemplateId);
			Assert.Equal(5, respawnedNpc.Position.X);
			Assert.Equal(1, staticPlaceables.GetSpawnCount(210010000, 107));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task TryDeleteAndScheduleRespawn_SkipsNoRespawnSpawns()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var service = CreateService(world, new StaticPlaceableStateService(), threadPoolManager);
			var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203073, respawnSeconds: 0)]);
			var templates = new NpcTemplateTable([CreateTemplate(203073)]);

			service.SpawnWorldNpcs(spawns, templates, [210010000]);
			var deleted = service.TryDeleteAndScheduleRespawn(1);

			Assert.True(deleted);
			Assert.False(service.HasRespawnTask(1));
			Assert.Equal(0, service.PendingRespawnCount);
			Assert.Empty(world.GetNpcs());
			service.SpawnWorldNpcs(spawns, templates, [210010000]);
			Assert.True(world.TryGetObject(1, out _));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public void SpawnWorldNpcs_ActivatesOnlyPoolSizeSpotsForValidPool()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = CreateService(world);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203010, x: 1, poolSize: 2),
			CreateSpawn(210010000, 203010, x: 2, poolSize: 2),
			CreateSpawn(210010000, 203010, x: 3, poolSize: 2),
		]);
		var templates = new NpcTemplateTable([CreateTemplate(203010)]);

		var result = service.SpawnWorldNpcs(spawns, templates, [210010000]);

		Assert.Equal(new WorldNpcSpawnResult(2, 0), result);
		Assert.Equal(2, world.GetNpcs().Count);
		Assert.Equal(2, world.GetNpcs().Select(npc => npc.Position.X).Distinct().Count());
	}

	[Fact]
	public void SpawnWorldNpcs_SpawnsOnlyTemporarySchedulesInCurrentWindow()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = CreateService(world);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203020, x: 1, groupTemporarySchedule: TemporarySpawnSchedule.FromAttributes(null, "2.*.*", "3.*.*")),
			CreateSpawn(210010000, 203020, x: 2),
			CreateSpawn(210010000, 203021, x: 3, spotTemporarySchedule: TemporarySpawnSchedule.FromAttributes(null, "2.*.*", "3.*.*")),
			CreateSpawn(210010000, 203021, x: 4, spotTemporarySchedule: TemporarySpawnSchedule.FromAttributes(null, "4.*.*", "5.*.*")),
		]);
		var templates = new NpcTemplateTable([CreateTemplate(203020), CreateTemplate(203021)]);

		var result = service.SpawnWorldNpcs(spawns, templates, [210010000], gameMinutes: 2 * 60, serverDayOfWeek: DayOfWeek.Friday);

		Assert.Equal(new WorldNpcSpawnResult(3, 1), result);
		Assert.Equal([1, 2, 3], world.GetNpcs().Select(npc => npc.Position.X).OrderBy(x => x).ToArray());
	}

	[Fact]
	public void SpawnWorldNpcsForMap_UsesOnlyRequestedMap()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = CreateService(world);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203000),
			CreateSpawn(220010000, 203001),
		]);
		var templates = new NpcTemplateTable([CreateTemplate(203000), CreateTemplate(203001)]);

		var result = service.SpawnWorldNpcsForMap(220010000, spawns, templates);

		Assert.Equal(new WorldNpcSpawnResult(1, 0), result);
		Assert.True(world.TryGetObject(1, out var gameObject));
		var npc = Assert.IsType<WorldNpc>(gameObject);
		Assert.Equal(203001, npc.TemplateId);
		Assert.Equal(220010000, npc.Position.WorldId);
	}

	[Fact]
	public void SpawnWorldNpcs_FiltersByDifficultId()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = CreateService(world);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203040, x: 1, difficultId: 1),
			CreateSpawn(210010000, 203040, x: 2, difficultId: 2),
		]);
		var templates = new NpcTemplateTable([CreateTemplate(203040)]);

		var result = service.SpawnWorldNpcs(spawns, templates, [210010000], gameMinutes: 0, serverDayOfWeek: DayOfWeek.Friday, difficultId: 1);

		Assert.Equal(new WorldNpcSpawnResult(1, 1), result);
		var npc = Assert.Single(world.GetNpcs());
		Assert.Equal(1, npc.Position.X);
	}

	[Fact]
	public void SpawnWorldNpcs_AppliesTemplateAndSpawnStatesLikeJava()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = CreateService(world);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203050, x: 1),
			CreateSpawn(210010000, 203051, x: 2),
			CreateSpawn(210010000, 203052, x: 3, state: 6),
		]);
		var templates = new NpcTemplateTable(
		[
			CreateTemplate(203050),
			CreateTemplate(203051, state: 32),
			CreateTemplate(203052, state: 32),
		]);

		var result = service.SpawnWorldNpcs(spawns, templates, [210010000]);

		Assert.Equal(new WorldNpcSpawnResult(3, 0), result);
		Assert.Equal(
			[WorldNpcState.DefaultSpawnState, 32, 6],
			world.GetNpcs().OrderBy(npc => npc.Position.X).Select(npc => npc.State).ToArray());
	}

	[Fact]
	public void SpawnWorldNpcs_AppliesTemplateAndSpawnAiNamesLikeJava()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = CreateService(world);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203060, x: 1),
			CreateSpawn(210010000, 203061, x: 2, aiName: "spot_ai"),
			CreateSpawn(210010000, 203062, x: 3, aiName: "__NO_AI__"),
		]);
		var templates = new NpcTemplateTable(
		[
			CreateTemplate(203060, aiName: "template_ai"),
			CreateTemplate(203061, aiName: "template_ai"),
			CreateTemplate(203062, aiName: "template_ai"),
		]);

		var result = service.SpawnWorldNpcs(spawns, templates, [210010000]);

		Assert.Equal(new WorldNpcSpawnResult(3, 0), result);
		Assert.Equal(
			["template_ai", "spot_ai", string.Empty],
			world.GetNpcs().OrderBy(npc => npc.Position.X).Select(npc => npc.AiName).ToArray());
	}

	[Fact]
	public async Task ProcessTemporarySpawnHourChange_DespawnsThenSpawnsEligibleTemporaryGroups()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = CreateService(world);
		var groupSchedule = TemporarySpawnSchedule.FromAttributes(null, null, null);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203030, x: 1, groupTemporarySchedule: groupSchedule, spotTemporarySchedule: TemporarySpawnSchedule.FromAttributes(null, "2.*.*", "3.*.*")),
			CreateSpawn(210010000, 203030, x: 2, groupTemporarySchedule: groupSchedule, spotTemporarySchedule: TemporarySpawnSchedule.FromAttributes(null, "4.*.*", "5.*.*")),
			CreateSpawn(210010000, 203031, x: 3),
		]);
		var templates = new NpcTemplateTable([CreateTemplate(203030), CreateTemplate(203031)]);

		var startup = service.SpawnWorldNpcs(spawns, templates, [210010000], gameMinutes: 2 * 60, serverDayOfWeek: DayOfWeek.Friday);
		var despawn = await service.ProcessTemporarySpawnHourChangeAsync(spawns, templates, [210010000], gameMinutes: 3 * 60, serverDayOfWeek: DayOfWeek.Friday);
		var respawn = await service.ProcessTemporarySpawnHourChangeAsync(spawns, templates, [210010000], gameMinutes: 4 * 60, serverDayOfWeek: DayOfWeek.Friday);

		Assert.Equal(new WorldNpcSpawnResult(2, 1), startup);
		Assert.Equal(new TemporarySpawnHourChangeResult(0, 1, 3), despawn);
		Assert.Equal(new TemporarySpawnHourChangeResult(1, 0, 2), respawn);
		Assert.Equal([2, 3], world.GetNpcs().Select(npc => npc.Position.X).OrderBy(x => x).ToArray());
	}

	private static WorldNpcSpawnService CreateService(GameWorld world)
	{
		return new WorldNpcSpawnService(
			new GameServerRuntimeContext(),
			world,
			new IDFactory(),
			NullLogger<WorldNpcSpawnService>.Instance);
	}

	private static WorldNpcSpawnService CreateService(GameWorld world, IStaticPlaceableStateService staticPlaceables)
	{
		return new WorldNpcSpawnService(
			new GameServerRuntimeContext(),
			world,
			new IDFactory(),
			gameTimeService: null,
			threadPoolManager: null,
			staticPlaceables,
			NullLogger<WorldNpcSpawnService>.Instance);
	}

	private static WorldNpcSpawnService CreateService(GameWorld world, IStaticPlaceableStateService staticPlaceables, ThreadPoolManager threadPoolManager)
	{
		return new WorldNpcSpawnService(
			new GameServerRuntimeContext(),
			world,
			new IDFactory(),
			gameTimeService: null,
			threadPoolManager,
			staticPlaceables,
			NullLogger<WorldNpcSpawnService>.Instance);
	}

	private static NpcSpawnSummary CreateSpawn(
		int mapId,
		int npcId,
		float x = 1,
		float y = 2,
		float z = 3,
		byte heading = 0,
		int poolSize = 0,
		byte difficultId = 0,
		string handler = "",
		int respawnSeconds = 295,
		int state = 0,
		string aiName = "",
		int staticId = 0,
		int randomWalkRange = 0,
		string walkerId = "",
		int walkerIndex = 0,
		string anchor = "",
		TemporarySpawnSchedule? groupTemporarySchedule = null,
		TemporarySpawnSchedule? spotTemporarySchedule = null)
	{
		return new NpcSpawnSummary(
			mapId,
			npcId,
			x,
			y,
			z,
			heading,
			respawnSeconds,
			poolSize,
			difficultId,
			handler,
			staticId,
			randomWalkRange,
			walkerId,
			walkerIndex,
			anchor,
			state,
			aiName,
			false,
			groupTemporarySchedule,
			spotTemporarySchedule);
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		var deadline = DateTimeOffset.UtcNow.AddSeconds(3);
		while (DateTimeOffset.UtcNow < deadline)
		{
			if (condition())
				return;

			await Task.Delay(25);
		}

		Assert.True(condition(), "Condition was not met before the timeout.");
	}

	private static async Task<GameServerRuntimeContext> CreateRuntimeContextWithWalkerDataAsync(string tempPath)
	{
		var staticDataFile = Path.Combine(tempPath, "static_data.xml");
		var cacheFile = Path.Combine(tempPath, "cache", "static_data.xml");
		var schemaFile = Path.Combine(tempPath, "static_data.xsd");
		Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
		File.WriteAllText(
			staticDataFile,
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<npc_walker>
					<walker_template route_id="route-a" pool="2" formation="SQUARE" rows="2">
						<routestep x="0" y="0" z="0" />
						<routestep x="10" y="0" z="0" />
					</walker_template>
				</npc_walker>
			</static_data>
			""");
		File.WriteAllText(schemaFile, """<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" />""");
		var dataManager = await DataManager.LoadAsync(
			new XmlDataLoaderOptions
			{
				MainXmlFilePath = staticDataFile,
				CacheXmlFilePath = cacheFile,
				SchemaFilePath = schemaFile,
				ValidateWhenCacheChanges = false,
			});
		var context = new GameServerRuntimeContext();
		context.SetDataManager(dataManager);
		return context;
	}

	private static async Task<GameServerRuntimeContext> CreateRuntimeContextWithVersionedWalkerDataAsync(string tempPath)
	{
		var staticDataFile = Path.Combine(tempPath, "static_data.xml");
		var cacheFile = Path.Combine(tempPath, "cache", "static_data.xml");
		var schemaFile = Path.Combine(tempPath, "static_data.xsd");
		Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
		File.WriteAllText(
			staticDataFile,
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<npc_walker>
					<walker_template route_id="route-v1" formation="POINT">
						<routestep x="1" y="0" z="0" />
						<routestep x="10" y="0" z="0" />
					</walker_template>
					<walker_template route_id="route-v2" formation="POINT">
						<routestep x="2" y="0" z="0" />
						<routestep x="20" y="0" z="0" />
					</walker_template>
				</npc_walker>
				<walker_versions>
					<walk_parent id="route-parent">
						<version id="route-v1" />
						<version id="route-v2" />
					</walk_parent>
				</walker_versions>
			</static_data>
			""");
		File.WriteAllText(schemaFile, """<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" />""");
		var dataManager = await DataManager.LoadAsync(
			new XmlDataLoaderOptions
			{
				MainXmlFilePath = staticDataFile,
				CacheXmlFilePath = cacheFile,
				SchemaFilePath = schemaFile,
				ValidateWhenCacheChanges = false,
			});
		var context = new GameServerRuntimeContext();
		context.SetDataManager(dataManager);
		return context;
	}

	private static NpcTemplateSummary CreateTemplate(int templateId, int state = 0, string aiName = "")
	{
		return new NpcTemplateSummary(
			templateId,
			$"npc-{templateId}",
			NameId: templateId,
			Level: 1,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "ELYOS",
			Tribe: "GENERAL",
			Type: "GENERAL",
			State: state,
			AiName: aiName);
	}
}
