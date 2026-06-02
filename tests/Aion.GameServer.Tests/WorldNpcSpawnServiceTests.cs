using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
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
	public async Task SpawnAndDespawnWorldNpc_RevalidatesCreaturePvpZoneCounters()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var context = new GameServerRuntimeContext();
		context.SetDataManager(dataManager);
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var zoneCounterService = new CreaturePvpZoneCounterService();
		var service = new WorldNpcSpawnService(
			context,
			world,
			new IDFactory(),
			gameTimeService: null,
			threadPoolManager: null,
			connectionRegistry: null,
			staticPlaceables: null,
			walkerSpawnPlans: null,
			walkerPlacementApplication: null,
			NullLogger<WorldNpcSpawnService>.Instance,
			creaturePvpZoneCounterService: zoneCounterService);
		var pvpZonePosition = new WorldPosition(210040000, 2700, 620, 150, 0);
		var spawns = new NpcSpawnTable([CreateSpawn(pvpZonePosition.WorldId, 203090, pvpZonePosition.X, pvpZonePosition.Y, pvpZonePosition.Z)]);
		var templates = new NpcTemplateTable([CreateTemplate(203090)]);
		Assert.Contains(
			dataManager.StaticData.CreaturePvpZones.GetZonesByMapId(pvpZonePosition.WorldId),
			zone => zone.Name == "PVP_87_210040000" && zone.Contains(pvpZonePosition));

		var result = service.SpawnWorldNpcs(spawns, templates, [pvpZonePosition.WorldId]);
		var enteredCounters = zoneCounterService.GetCounters(1);
		var despawned = service.TryDespawnWorldNpc(1);
		var staleLeave = zoneCounterService.ApplyZoneLeave(1, "PVP_87_210040000", CreaturePvpZoneCounterType.Pvp);

		Assert.Equal(new WorldNpcSpawnResult(1, 0), result);
		Assert.Equal(1, enteredCounters.PvpZoneCount);
		Assert.Equal(0, enteredCounters.SiegeZoneCount);
		Assert.True(despawned);
		Assert.Equal(CreaturePvpZoneCounters.Empty, zoneCounterService.GetCounters(1));
		Assert.Equal(CreaturePvpZoneMembershipTransitionStatus.NotInside, staleLeave.Status);
	}

	[Fact]
	public async Task InitAsync_StartsRouteWalkingForSpawnedWalkerPlans()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walker-startup-route-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextWithWalkerSpawnDataAsync(tempPath);
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var walkerPlans = new WorldNpcWalkerSpawnPlanCacheService();
			var registry = new CapturingConnectionRegistry();
			var routeWalking = CreateRouteWalkingService(context, world, walkerPlans, registry);
			var service = new WorldNpcSpawnService(
				context,
				world,
				new IDFactory(),
				gameTimeService: null,
				threadPoolManager: null,
				connectionRegistry: registry,
				staticPlaceables: null,
				walkerSpawnPlans: walkerPlans,
				walkerPlacementApplication: new WorldNpcWalkerPlacementApplicationService(),
				NullLogger<WorldNpcSpawnService>.Instance,
				routeWalking);

			await service.InitAsync();

			Assert.Equal(1, service.LoadedCount);
			Assert.Equal(0, service.SkippedCount);
			Assert.Equal(1, routeWalking.ActiveStateCount);
			Assert.True(routeWalking.TryGetActiveState(1, out var state));
			Assert.NotNull(state);
			Assert.Equal(0, state.TargetStepIndex);
			Assert.Single(registry.Broadcasts);
			Assert.Equal(1, registry.Broadcasts[0].SourceObjectId);
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
	public async Task InitAsync_StartsRandomWalkingBeforeRouteWalkingForDualMetadataNpc()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-random-before-route-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var context = await CreateRuntimeContextWithRandomWalkerSpawnDataAsync(tempPath);
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var walkerPlans = new WorldNpcWalkerSpawnPlanCacheService();
			var registry = new CapturingConnectionRegistry();
			var aiStates = new WorldNpcAiStateService();
			var randomValues = new Queue<float>([6, 4]);
			var randomWalking = new WorldNpcRandomWalkService(
				world,
				registry,
				new GameServerOptions
				{
					Ai = new GameServerAiOptions
					{
						NpcMovementMinimumDelaySeconds = 0,
						NpcMovementMaximumDelaySeconds = 0,
					},
				},
				threadPoolManager,
				aiStates,
				maxExclusive =>
				{
					Assert.Equal(10, maxExclusive);
					return randomValues.Dequeue();
				},
				(_, _) => 0);
			var routeWalking = CreateRouteWalkingService(context, world, walkerPlans, registry, aiStates);
			var service = new WorldNpcSpawnService(
				context,
				world,
				new IDFactory(),
				gameTimeService: null,
				threadPoolManager: null,
				connectionRegistry: registry,
				staticPlaceables: null,
				walkerSpawnPlans: walkerPlans,
				walkerPlacementApplication: new WorldNpcWalkerPlacementApplicationService(),
				NullLogger<WorldNpcSpawnService>.Instance,
				routeWalking,
				randomWalking);

			await service.InitAsync();

			await WaitUntilAsync(() => registry.Broadcasts.Count == 1
				&& randomWalking.TryGetActiveState(1, out var state)
				&& state?.Target != null);
			Assert.Equal(1, randomWalking.ActiveStateCount);
			Assert.Equal(0, routeWalking.ActiveStateCount);
			Assert.True(aiStates.TryGetState(1, out var aiState));
			Assert.NotNull(aiState);
			Assert.Equal(WorldNpcAiState.Walking, aiState.State);
			Assert.Equal(WorldNpcAiSubState.WalkRandom, aiState.SubState);
			Assert.True(randomWalking.TryGetActiveState(1, out var randomState));
			Assert.NotNull(randomState);
			Assert.NotNull(randomState.Target);
			Assert.Equal(1, randomState.Target.X);
			Assert.Equal(-1, randomState.Target.Y);
			Assert.Equal(0, randomState.Target.Z);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
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
	public async Task ProcessTemporarySpawnHourChangeAsync_StartsRouteWalkingForNewTemporaryWalkers()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walker-hour-route-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextWithSingleWalkerDataAsync(tempPath);
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var walkerPlans = new WorldNpcWalkerSpawnPlanCacheService();
			var registry = new CapturingConnectionRegistry();
			var routeWalking = CreateRouteWalkingService(context, world, walkerPlans, registry);
			var service = new WorldNpcSpawnService(
				context,
				world,
				new IDFactory(),
				gameTimeService: null,
				threadPoolManager: null,
				connectionRegistry: registry,
				staticPlaceables: null,
				walkerSpawnPlans: walkerPlans,
				walkerPlacementApplication: new WorldNpcWalkerPlacementApplicationService(),
				NullLogger<WorldNpcSpawnService>.Instance,
				routeWalking);
			var spawns = new NpcSpawnTable(
			[
				CreateSpawn(
					210010000,
					203080,
					x: 0,
					y: 0,
					walkerId: "route-a",
					walkerIndex: 0,
					groupTemporarySchedule: TemporarySpawnSchedule.FromAttributes(null, "4.*.*", "5.*.*")),
			]);
			var templates = new NpcTemplateTable([CreateTemplate(203080)]);

			var result = await service.ProcessTemporarySpawnHourChangeAsync(
				spawns,
				templates,
				[210010000],
				gameMinutes: 4 * 60,
				serverDayOfWeek: DayOfWeek.Friday);

			Assert.Equal(new TemporarySpawnHourChangeResult(1, 0, 0), result);
			Assert.Equal(1, routeWalking.ActiveStateCount);
			Assert.True(routeWalking.TryGetActiveState(1, out _));
			Assert.Single(registry.Broadcasts);
			Assert.Equal(1, registry.Broadcasts[0].SourceObjectId);
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
	public async Task TrySwapInactiveWalkerVariant_RevalidatesCreaturePvpZoneCountersForActivatedAndParkedVariants()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walker-swap-pvp-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextWithVersionedWalkerDataAsync(tempPath, includePvpZone: true);
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var zoneCounterService = new CreaturePvpZoneCounterService();
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
				NullLogger<WorldNpcSpawnService>.Instance,
				creaturePvpZoneCounterService: zoneCounterService);
			var spawns = new NpcSpawnTable(
			[
				CreateSpawn(210010000, 203081, x: 1, y: 10, walkerId: "route-v1", walkerIndex: 0),
				CreateSpawn(210010000, 203082, x: 2, y: 20, walkerId: "route-v2", walkerIndex: 0),
			]);
			var templates = new NpcTemplateTable([CreateTemplate(203081), CreateTemplate(203082)]);
			var zones = context.DataManager!.StaticData.CreaturePvpZones.GetZonesByMapId(210010000);
			Assert.Contains(zones, zone => zone.ZoneId == "PVP_WALKER_VARIANTS_210010000"
				&& zone.Contains(new global::Aion.GameServer.World.WorldPosition(210010000, 1, 10, 0, 0))
				&& zone.Contains(new global::Aion.GameServer.World.WorldPosition(210010000, 2, 20, 0, 0)));

			service.SpawnWorldNpcs(spawns, templates, [210010000]);

			Assert.False(world.TryGetObject(1, out _));
			Assert.True(world.TryGetObject(2, out _));
			Assert.Equal(CreaturePvpZoneCounters.Empty, zoneCounterService.GetCounters(1));
			Assert.Equal(1, zoneCounterService.GetCounters(2).PvpZoneCount);

			var swapped = service.TrySwapInactiveWalkerVariant(activeObjectId: 2, inactiveObjectId: 1);

			Assert.True(swapped);
			Assert.True(world.TryGetObject(1, out _));
			Assert.False(world.TryGetObject(2, out _));
			Assert.Equal(1, zoneCounterService.GetCounters(1).PvpZoneCount);
			Assert.Equal(CreaturePvpZoneCounters.Empty, zoneCounterService.GetCounters(2));
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
	public async Task TrySwapInactiveWalkerFormationVariant_SpawnsParkedFormationAndParksCurrentFormation()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walker-formation-swap-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextWithVersionedFormationDataAsync(tempPath);
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
				CreateSpawn(210010000, 203083, x: 1, y: 10, walkerId: "formation-v1", walkerIndex: 1),
				CreateSpawn(210010000, 203083, x: 1, y: 10, walkerId: "formation-v1", walkerIndex: 2),
				CreateSpawn(210010000, 203084, x: 20, y: 30, walkerId: "formation-v2", walkerIndex: 1),
				CreateSpawn(210010000, 203084, x: 20, y: 30, walkerId: "formation-v2", walkerIndex: 2),
			]);
			var templates = new NpcTemplateTable([CreateTemplate(203083), CreateTemplate(203084)]);

			service.SpawnWorldNpcs(spawns, templates, [210010000]);
			var activeIds = world.GetNpcs().OfType<WorldNpc>().Select(npc => npc.ObjectId).OrderBy(id => id).ToArray();
			var inactiveIds = Enumerable.Range(1, 4)
				.Where(objectId => service.TryGetInactiveWalkerVariant(objectId, out _))
				.OrderBy(id => id)
				.ToArray();

			var swapped = service.TrySwapInactiveWalkerFormationVariant(activeIds, inactiveIds);

			Assert.True(swapped);
			Assert.Equal(2, activeIds.Length);
			Assert.Equal(2, inactiveIds.Length);
			Assert.Equal(inactiveIds, world.GetNpcs().OfType<WorldNpc>().Select(npc => npc.ObjectId).OrderBy(id => id).ToArray());
			foreach (var objectId in activeIds)
			{
				Assert.False(world.TryGetObject(objectId, out _));
				Assert.True(service.TryGetInactiveWalkerVariant(objectId, out var parkedNpc));
				Assert.NotNull(parkedNpc);
			}

			foreach (var objectId in inactiveIds)
			{
				Assert.True(world.TryGetObject(objectId, out var activatedObject));
				var activatedNpc = Assert.IsType<WorldNpc>(activatedObject);
				Assert.False(service.TryGetInactiveWalkerVariant(objectId, out _));
				var expectedY = activatedNpc.SpawnLocation.Y + (activatedNpc.WalkerIndex == 2 ? 1 : -1);
				Assert.Equal(activatedNpc.SpawnLocation.X, activatedNpc.Position.X, precision: 4);
				Assert.Equal(expectedY, activatedNpc.Position.Y, precision: 4);
			}

			Assert.Equal(2, service.InactiveWalkerVariantCount);
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
	public async Task TrySwapInactiveWalkerFormationVariant_RevalidatesCreaturePvpZoneCountersForActivatedAndParkedFormations()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walker-formation-swap-pvp-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextWithVersionedFormationDataAsync(tempPath, includePvpZone: true);
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var zoneCounterService = new CreaturePvpZoneCounterService();
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
				NullLogger<WorldNpcSpawnService>.Instance,
				creaturePvpZoneCounterService: zoneCounterService);
			var spawns = new NpcSpawnTable(
			[
				CreateSpawn(210010000, 203083, x: 1, y: 10, walkerId: "formation-v1", walkerIndex: 1),
				CreateSpawn(210010000, 203083, x: 1, y: 10, walkerId: "formation-v1", walkerIndex: 2),
				CreateSpawn(210010000, 203084, x: 20, y: 30, walkerId: "formation-v2", walkerIndex: 1),
				CreateSpawn(210010000, 203084, x: 20, y: 30, walkerId: "formation-v2", walkerIndex: 2),
			]);
			var templates = new NpcTemplateTable([CreateTemplate(203083), CreateTemplate(203084)]);
			var zones = context.DataManager!.StaticData.CreaturePvpZones.GetZonesByMapId(210010000);
			Assert.Contains(zones, zone => zone.ZoneId == "PVP_FORMATION_VARIANTS_210010000"
				&& zone.Contains(new global::Aion.GameServer.World.WorldPosition(210010000, 1, 9, 0, 0))
				&& zone.Contains(new global::Aion.GameServer.World.WorldPosition(210010000, 20, 31, 0, 0)));

			service.SpawnWorldNpcs(spawns, templates, [210010000]);
			var activeIds = world.GetNpcs().OfType<WorldNpc>().Select(npc => npc.ObjectId).OrderBy(id => id).ToArray();
			var inactiveIds = Enumerable.Range(1, 4)
				.Where(objectId => service.TryGetInactiveWalkerVariant(objectId, out _))
				.OrderBy(id => id)
				.ToArray();
			Assert.Equal([3, 4], activeIds);
			Assert.Equal([1, 2], inactiveIds);
			Assert.All(activeIds, objectId => Assert.Equal(1, zoneCounterService.GetCounters(objectId).PvpZoneCount));
			Assert.All(inactiveIds, objectId => Assert.Equal(CreaturePvpZoneCounters.Empty, zoneCounterService.GetCounters(objectId)));

			var swapped = service.TrySwapInactiveWalkerFormationVariant(activeIds, inactiveIds);

			Assert.True(swapped);
			Assert.All(activeIds, objectId =>
			{
				Assert.False(world.TryGetObject(objectId, out _));
				Assert.Equal(CreaturePvpZoneCounters.Empty, zoneCounterService.GetCounters(objectId));
			});
			Assert.All(inactiveIds, objectId =>
			{
				Assert.True(world.TryGetObject(objectId, out _));
				Assert.Equal(1, zoneCounterService.GetCounters(objectId).PvpZoneCount);
			});
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
	public async Task TrySwapInactiveWalkerFormationVariant_RevalidatesCreatureSiegeZoneCountersForActivatedAndParkedFormations()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-walker-formation-swap-siege-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		try
		{
			var context = await CreateRuntimeContextWithVersionedFormationDataAsync(tempPath, includeSiegeZone: true);
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			var zoneCounterService = new CreaturePvpZoneCounterService();
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
				NullLogger<WorldNpcSpawnService>.Instance,
				creaturePvpZoneCounterService: zoneCounterService);
			var spawns = new NpcSpawnTable(
			[
				CreateSpawn(210010000, 203083, x: 1, y: 10, walkerId: "formation-v1", walkerIndex: 1),
				CreateSpawn(210010000, 203083, x: 1, y: 10, walkerId: "formation-v1", walkerIndex: 2),
				CreateSpawn(210010000, 203084, x: 20, y: 30, walkerId: "formation-v2", walkerIndex: 1),
				CreateSpawn(210010000, 203084, x: 20, y: 30, walkerId: "formation-v2", walkerIndex: 2),
			]);
			var templates = new NpcTemplateTable([CreateTemplate(203083), CreateTemplate(203084)]);
			var zones = context.DataManager!.StaticData.CreaturePvpZones.GetZonesByMapId(210010000);
			Assert.Contains(zones, zone => zone.ZoneId == "FORT_FORMATION_VARIANTS_210010000"
				&& zone.ZoneType == CreaturePvpZoneType.Siege
				&& zone.Contains(new global::Aion.GameServer.World.WorldPosition(210010000, 1, 9, 0, 0))
				&& zone.Contains(new global::Aion.GameServer.World.WorldPosition(210010000, 20, 31, 0, 0)));

			service.SpawnWorldNpcs(spawns, templates, [210010000]);
			var activeIds = world.GetNpcs().OfType<WorldNpc>().Select(npc => npc.ObjectId).OrderBy(id => id).ToArray();
			var inactiveIds = Enumerable.Range(1, 4)
				.Where(objectId => service.TryGetInactiveWalkerVariant(objectId, out _))
				.OrderBy(id => id)
				.ToArray();
			Assert.Equal([3, 4], activeIds);
			Assert.Equal([1, 2], inactiveIds);
			Assert.All(activeIds, objectId =>
			{
				var counters = zoneCounterService.GetCounters(objectId);
				Assert.Equal(1, counters.SiegeZoneCount);
				Assert.Equal(0, counters.PvpZoneCount);
			});
			Assert.All(inactiveIds, objectId => Assert.Equal(CreaturePvpZoneCounters.Empty, zoneCounterService.GetCounters(objectId)));

			var swapped = service.TrySwapInactiveWalkerFormationVariant(activeIds, inactiveIds);

			Assert.True(swapped);
			Assert.All(activeIds, objectId =>
			{
				Assert.False(world.TryGetObject(objectId, out _));
				Assert.Equal(CreaturePvpZoneCounters.Empty, zoneCounterService.GetCounters(objectId));
			});
			Assert.All(inactiveIds, objectId =>
			{
				Assert.True(world.TryGetObject(objectId, out _));
				var counters = zoneCounterService.GetCounters(objectId);
				Assert.Equal(1, counters.SiegeZoneCount);
				Assert.Equal(0, counters.PvpZoneCount);
			});
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
	public void TryDespawnWorldNpc_ClearsAiRuntimeState()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var staticPlaceables = new StaticPlaceableStateService();
		var aiStates = new WorldNpcAiStateService();
		var service = CreateService(world, staticPlaceables, aiStates);
		var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203073)]);
		var templates = new NpcTemplateTable([CreateTemplate(203073)]);

		service.SpawnWorldNpcs(spawns, templates, [210010000]);
		aiStates.StartRandomWalking(1);
		var despawned = service.TryDespawnWorldNpc(1);

		Assert.True(despawned);
		Assert.False(aiStates.TryGetState(1, out _));
	}

	[Fact]
	public void SpawnWorldNpcs_ClearsStaleAiRuntimeStateForReusedObjectId()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var staticPlaceables = new StaticPlaceableStateService();
		var aiStates = new WorldNpcAiStateService();
		var service = CreateService(world, staticPlaceables, aiStates);
		var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203079)]);
		var templates = new NpcTemplateTable([CreateTemplate(203079)]);

		aiStates.MarkDied(1);
		service.SpawnWorldNpcs(spawns, templates, [210010000]);

		Assert.True(world.TryGetObject(1, out _));
		Assert.False(aiStates.TryGetState(1, out _));
	}

	[Fact]
	public void SpawnWorldNpcs_InitializesNpcLifeStatsFromTemplateMaxHp()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var staticPlaceables = new StaticPlaceableStateService();
		var initialized = new List<(int ObjectId, int MaxHp)>();
		var service = CreateService(
			world,
			staticPlaceables,
			npcLifeStatsInitialize: npc => initialized.Add((npc.ObjectId, npc.Template.MaxHp)));
		var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203083)]);
		var templates = new NpcTemplateTable([CreateTemplate(203083, maxHp: 321)]);

		service.SpawnWorldNpcs(spawns, templates, [210010000]);

		Assert.Equal([(1, 321)], initialized);
	}

	[Fact]
	public void TryDespawnWorldNpc_ClearsNpcLifeStatsRuntimeState()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var staticPlaceables = new StaticPlaceableStateService();
		var cleared = new List<int>();
		var service = CreateService(
			world,
			staticPlaceables,
			npcLifeStatsClear: objectId => cleared.Add(objectId));
		var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203084)]);
		var templates = new NpcTemplateTable([CreateTemplate(203084, maxHp: 321)]);

		service.SpawnWorldNpcs(spawns, templates, [210010000]);
		cleared.Clear();
		var despawned = service.TryDespawnWorldNpc(1);

		Assert.True(despawned);
		Assert.Equal([1], cleared);
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
	public async Task TryScheduleWorldNpcDeath_NotifiesRespawnedNpcCallback()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var staticPlaceables = new StaticPlaceableStateService();
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var respawnNotifications = new List<(int OldObjectId, WorldNpc Respawn)>();
		try
		{
			var service = CreateService(
				world,
				staticPlaceables,
				threadPoolManager,
				(oldObjectId, respawn) =>
				{
					respawnNotifications.Add((oldObjectId, respawn));
					return true;
				});
			var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203076, x: 7, respawnSeconds: 1)]);
			var templates = new NpcTemplateTable([CreateTemplate(203076)]);

			service.SpawnWorldNpcs(spawns, templates, [210010000]);
			var scheduledDeath = service.TryScheduleWorldNpcDeath(1, hasRegisteredDrops: false, decayDelay: TimeSpan.FromMilliseconds(50));

			Assert.True(scheduledDeath);

			await WaitUntilAsync(() => respawnNotifications.Count == 1);

			var (oldObjectId, respawn) = Assert.Single(respawnNotifications);
			Assert.Equal(1, oldObjectId);
			Assert.Equal(203076, respawn.TemplateId);
			Assert.Equal(7, respawn.Position.X);
			Assert.True(world.TryGetObject(respawn.ObjectId, out var stored));
			Assert.Same(respawn, stored);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public void TryScheduleWorldNpcDeath_UsesRegisteredDropLookupForDefaultDecaySelection()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var dropLookup = new FakeWorldNpcDropRegistrationLookup([1]);
		var service = CreateService(world, dropLookup);
		var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203075)]);
		var templates = new NpcTemplateTable([CreateTemplate(203075)]);

		service.SpawnWorldNpcs(spawns, templates, [210010000]);

		Assert.Equal(TimeSpan.FromMinutes(5), service.SelectWorldNpcDecayDelay(1));
		Assert.Equal([1], dropLookup.QueriedObjectIds);

		var scheduledDeath = service.TryScheduleWorldNpcDeath(1);

		Assert.True(scheduledDeath);
		Assert.Equal([1, 1], dropLookup.QueriedObjectIds);
		Assert.Empty(world.GetNpcs());
	}

	[Fact]
	public void SelectWorldNpcDecayDelay_MatchesJavaDropIntervals()
	{
		Assert.Equal(TimeSpan.FromSeconds(2), WorldNpcSpawnService.SelectWorldNpcDecayDelay(hasRegisteredDrops: false));
		Assert.Equal(TimeSpan.FromMinutes(5), WorldNpcSpawnService.SelectWorldNpcDecayDelay(hasRegisteredDrops: true));
	}

	[Fact]
	public async Task CancelDecay_CancelsTrackedDecayAndReturnsRemainingDelay()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var service = CreateService(world, new StaticPlaceableStateService(), threadPoolManager);
			var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203077)]);
			var templates = new NpcTemplateTable([CreateTemplate(203077)]);

			service.SpawnWorldNpcs(spawns, templates, [210010000]);
			var scheduled = service.TryScheduleWorldNpcDecayTask(1, hasRegisteredDrops: true, TimeSpan.FromMilliseconds(500));

			Assert.True(scheduled);
			Assert.True(service.HasDecayTask(1));
			Assert.Equal(1, service.PendingDecayCount);

			var remainingDelay = service.CancelDecay(1);

			Assert.NotNull(remainingDelay);
			Assert.True(remainingDelay.Value > TimeSpan.Zero);
			Assert.False(service.HasDecayTask(1));
			Assert.Equal(0, service.PendingDecayCount);
			await Task.Delay(100);
			Assert.Single(world.GetNpcs());
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
	public void SpawnWorldNpcsForInstance_FiltersByDifficultyAndUsesInstanceIdLikeJavaSpawnInstance()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = CreateService(world);
		var instance = new WorldMapInstanceRuntimeState(instanceId: 7, difficultyId: 2);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 203040, x: 1, difficultId: 1),
			CreateSpawn(210010000, 203040, x: 2, difficultId: 2),
			CreateSpawn(210010000, 203040, x: 3),
		]);
		var templates = new NpcTemplateTable([CreateTemplate(203040)]);

		var result = service.SpawnWorldNpcsForInstance(instance, 210010000, spawns, templates);

		Assert.Equal(new WorldNpcSpawnResult(2, 1), result);
		var npcs = world.GetNpcs().OrderBy(npc => npc.Position.X).ToArray();
		Assert.Equal([2, 3], npcs.Select(npc => (int)npc.Position.X).ToArray());
		Assert.All(npcs, npc => Assert.Equal(7, npc.Position.InstanceId));
	}

	[Fact]
	public void SpawnWorldNpcsForInstance_SetsStaticDoorStateLikeJavaStaticDoorSpawnManager()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var staticPlaceables = new StaticPlaceableStateService();
		var service = CreateService(world, staticPlaceables);
		var instance = new WorldMapInstanceRuntimeState(instanceId: 7, difficultyId: 2);
		var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203040, x: 2, difficultId: 2)]);
		var templates = new NpcTemplateTable([CreateTemplate(203040)]);
		var staticDoors = new StaticDoorTable(
		[
			new StaticDoorSummary(210010000, StaticId: 33, KeyId: 0, X: 1, Y: 2, Z: 3, State: 1),
			new StaticDoorSummary(210010000, StaticId: 34, KeyId: 185000044, X: 4, Y: 5, Z: 6, State: 10),
			new StaticDoorSummary(220010000, StaticId: 35, KeyId: 0, X: 7, Y: 8, Z: 9, State: 1),
		]);

		var result = service.SpawnWorldNpcsForInstance(instance, 210010000, spawns, templates, staticDoors);

		Assert.Equal(new WorldNpcSpawnResult(1, 0), result);
		Assert.Equal(true, staticPlaceables.GetDoorState(210010000, 7, 33));
		Assert.Equal(false, staticPlaceables.GetDoorState(210010000, 7, 34));
		Assert.Null(staticPlaceables.GetDoorState(220010000, 7, 35));
	}

	[Fact]
	public void SpawnWorldNpcsForInstance_MaterializesStaticHandlerObjectsLikeJavaStaticObjectSpawnManager()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var staticPlaceables = new StaticPlaceableStateService();
		var service = CreateService(world, staticPlaceables);
		var instance = new WorldMapInstanceRuntimeState(instanceId: 7, difficultyId: 2);
		var spawns = new NpcSpawnTable(
		[
			CreateSpawn(210010000, 400001, x: 4, y: 5, z: 6, heading: 7, difficultId: 2, handler: "STATIC", staticId: 107),
		]);
		var itemTemplates = new ItemTemplateTable(
		[
			new ItemTemplateSummary(400001, "static_object", 0, 0, 1, "STATIC_OBJECT", "ITEM", "COMMON", "PC_ALL", 1, 0, 0),
		]);

		var result = service.SpawnWorldNpcsForInstance(
			instance,
			210010000,
			spawns,
			new NpcTemplateTable([]),
			itemTemplates: itemTemplates);

		Assert.Equal(new WorldNpcSpawnResult(0, 0), result);
		Assert.Empty(world.GetNpcs());
		var staticObject = Assert.Single(world.GetStaticObjects());
		Assert.Equal(400001, staticObject.TemplateId);
		Assert.Equal("static_object", staticObject.Template.Name);
		Assert.Equal(new WorldPosition(210010000, 4, 5, 6, 7, InstanceId: 7), staticObject.Position);
		Assert.Equal(107, staticObject.StaticId);
		Assert.Equal(1, staticPlaceables.GetSpawnCount(210010000, 107));
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

	[Fact]
	public async Task UnregisterTemporarySpawnsForInstance_RemovesOnlyDestroyedInstanceTrackingLikeJavaTemporarySpawnEngine()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var service = CreateService(world);
		var groupSchedule = TemporarySpawnSchedule.FromAttributes(null, "0.*.*", "1.*.*");
		var spawns = new NpcSpawnTable([CreateSpawn(210010000, 203030, groupTemporarySchedule: groupSchedule)]);
		var templates = new NpcTemplateTable([CreateTemplate(203030)]);
		var firstInstance = new WorldMapInstanceRuntimeState(instanceId: 7, difficultyId: 0);
		var secondInstance = new WorldMapInstanceRuntimeState(instanceId: 8, difficultyId: 0);

		var firstSpawn = service.SpawnWorldNpcsForInstance(firstInstance, 210010000, spawns, templates);
		var secondSpawn = service.SpawnWorldNpcsForInstance(secondInstance, 210010000, spawns, templates);
		var unregistered = service.UnregisterTemporarySpawnsForInstance(210010000, firstInstance.InstanceId);
		var hourChange = await service.ProcessTemporarySpawnHourChangeAsync(
			spawns,
			templates,
			[210010000],
			gameMinutes: 60,
			serverDayOfWeek: DayOfWeek.Friday);

		Assert.Equal(new WorldNpcSpawnResult(1, 0), firstSpawn);
		Assert.Equal(new WorldNpcSpawnResult(1, 0), secondSpawn);
		Assert.Equal(1, unregistered);
		Assert.Equal(new TemporarySpawnHourChangeResult(0, 1, 1), hourChange);
		Assert.Equal([7], world.GetNpcs().Select(npc => npc.Position.InstanceId).ToArray());
	}

	private static WorldNpcSpawnService CreateService(GameWorld world)
	{
		return new WorldNpcSpawnService(
			new GameServerRuntimeContext(),
			world,
			new IDFactory(),
			NullLogger<WorldNpcSpawnService>.Instance);
	}

	private static WorldNpcSpawnService CreateService(
		GameWorld world,
		IStaticPlaceableStateService staticPlaceables,
		WorldNpcAiStateService? aiStates = null,
		Action<WorldNpc>? npcLifeStatsInitialize = null,
		Action<int>? npcLifeStatsClear = null)
	{
		return new WorldNpcSpawnService(
			new GameServerRuntimeContext(),
			world,
			new IDFactory(),
			gameTimeService: null,
			threadPoolManager: null,
			staticPlaceables,
			NullLogger<WorldNpcSpawnService>.Instance,
			npcAiStates: aiStates,
			npcLifeStatsInitialize: npcLifeStatsInitialize,
			npcLifeStatsClear: npcLifeStatsClear);
	}

	private static WorldNpcSpawnService CreateService(
		GameWorld world,
		IStaticPlaceableStateService staticPlaceables,
		ThreadPoolManager threadPoolManager,
		Func<int, WorldNpc, bool>? respawnedNpcCallback = null)
	{
		return new WorldNpcSpawnService(
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
			respawnedNpcCallback: respawnedNpcCallback);
	}

	private static WorldNpcSpawnService CreateService(GameWorld world, IWorldNpcDropRegistrationLookup dropRegistrationLookup)
	{
		return new WorldNpcSpawnService(
			new GameServerRuntimeContext(),
			world,
			new IDFactory(),
			gameTimeService: null,
			threadPoolManager: null,
			connectionRegistry: null,
			staticPlaceables: null,
			walkerSpawnPlans: null,
			walkerPlacementApplication: null,
			logger: NullLogger<WorldNpcSpawnService>.Instance,
			dropRegistrationLookup: dropRegistrationLookup);
	}

	private static WorldNpcWalkerRouteWalkingService CreateRouteWalkingService(
		GameServerRuntimeContext context,
		GameWorld world,
		IWorldNpcWalkerSpawnPlanCacheService walkerPlans,
		IGameClientConnectionRegistry registry,
		WorldNpcAiStateService? aiStates = null)
	{
		return new WorldNpcWalkerRouteWalkingService(
			context,
			world,
			walkerPlans,
			new WorldNpcWalkerRouteService(),
			new WorldNpcWalkerMovementStateService(),
			new WorldNpcWalkerMovementBroadcastService(world, registry),
			npcAiStates: aiStates);
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

	private static string FindRepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "game-server", "data", "static_data", "static_data.xml")))
				return directory.FullName;
			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
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

	private static async Task<GameServerRuntimeContext> CreateRuntimeContextWithSingleWalkerDataAsync(string tempPath)
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
					<walker_template route_id="route-a" pool="1" formation="POINT">
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

	private static async Task<GameServerRuntimeContext> CreateRuntimeContextWithWalkerSpawnDataAsync(string tempPath)
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
				<world_maps>
					<map id="210010000" instance="false" twin_count="1" />
				</world_maps>
				<npc_templates>
					<npc_template npc_id="203080" name="walker-npc" name_id="203080" level="1" rank="NORMAL" rating="NORMAL" race="ELYOS" tribe="GENERAL" type="GENERAL" />
				</npc_templates>
				<spawns>
					<spawn_map map_id="210010000">
						<spawn npc_id="203080" respawn_time="295">
							<spot x="0" y="0" z="0" walker_id="route-a" walker_index="0" />
						</spawn>
					</spawn_map>
				</spawns>
				<npc_walker>
					<walker_template route_id="route-a" pool="1" formation="POINT">
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

	private static async Task<GameServerRuntimeContext> CreateRuntimeContextWithRandomWalkerSpawnDataAsync(string tempPath)
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
				<world_maps>
					<map id="210010000" instance="false" twin_count="1" />
				</world_maps>
				<npc_templates>
					<npc_template npc_id="203080" name="random-walker-npc" name_id="203080" level="1" rank="NORMAL" rating="NORMAL" race="ELYOS" tribe="GENERAL" type="GENERAL" />
				</npc_templates>
				<spawns>
					<spawn_map map_id="210010000">
						<spawn npc_id="203080" respawn_time="295">
							<spot x="0" y="0" z="0" random_walk="5" walker_id="route-a" walker_index="0" />
						</spawn>
					</spawn_map>
				</spawns>
				<npc_walker>
					<walker_template route_id="route-a" pool="1" formation="POINT">
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

	private static async Task<GameServerRuntimeContext> CreateRuntimeContextWithVersionedWalkerDataAsync(
		string tempPath,
		bool includePvpZone = false)
	{
		var staticDataFile = Path.Combine(tempPath, "static_data.xml");
		var cacheFile = Path.Combine(tempPath, "cache", "static_data.xml");
		var schemaFile = Path.Combine(tempPath, "static_data.xsd");
		Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
		var pvpZoneXml = includePvpZone
			? """
				<zones>
					<zone name="PVP_WALKER_VARIANTS_210010000" zone_type="PVP" area_type="POLYGON" mapid="210010000">
						<points bottom="-10" top="10">
							<point x="0" y="9" />
							<point x="3" y="9" />
							<point x="3" y="21" />
							<point x="0" y="21" />
						</points>
					</zone>
				</zones>
			"""
			: string.Empty;
		File.WriteAllText(
			staticDataFile,
			$$"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
			{{pvpZoneXml}}
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

	private static async Task<GameServerRuntimeContext> CreateRuntimeContextWithVersionedFormationDataAsync(
		string tempPath,
		bool includePvpZone = false,
		bool includeSiegeZone = false)
	{
		var staticDataFile = Path.Combine(tempPath, "static_data.xml");
		var cacheFile = Path.Combine(tempPath, "cache", "static_data.xml");
		var schemaFile = Path.Combine(tempPath, "static_data.xsd");
		Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
		var pvpZoneXml = includePvpZone
			? """
				<zones>
					<zone name="PVP_FORMATION_VARIANTS_210010000" zone_type="PVP" area_type="POLYGON" mapid="210010000">
						<points bottom="-10" top="10">
							<point x="0" y="8" />
							<point x="22" y="8" />
							<point x="22" y="32" />
							<point x="0" y="32" />
						</points>
					</zone>
				</zones>
			"""
			: string.Empty;
		var siegeZoneXml = includeSiegeZone
			? """
				<zones>
					<zone name="FORT_FORMATION_VARIANTS_210010000" zone_type="FORT" area_type="POLYGON" mapid="210010000">
						<points bottom="-10" top="10">
							<point x="0" y="8" />
							<point x="22" y="8" />
							<point x="22" y="32" />
							<point x="0" y="32" />
						</points>
					</zone>
				</zones>
			"""
			: string.Empty;
		File.WriteAllText(
			staticDataFile,
			$$"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
			{{pvpZoneXml}}
			{{siegeZoneXml}}
				<npc_walker>
					<walker_template route_id="formation-v1" pool="2" formation="SQUARE" rows="2">
						<routestep x="1" y="10" z="0" />
						<routestep x="11" y="10" z="0" />
					</walker_template>
					<walker_template route_id="formation-v2" pool="2" formation="SQUARE" rows="2">
						<routestep x="20" y="30" z="0" />
						<routestep x="30" y="30" z="0" />
					</walker_template>
				</npc_walker>
				<walker_versions>
					<walk_parent id="formation-parent">
						<version id="formation-v1" />
						<version id="formation-v2" />
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

	private static NpcTemplateSummary CreateTemplate(int templateId, int state = 0, string aiName = "", int maxHp = 0)
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
			MaxHp: maxHp,
			State: state,
			AiName: aiName);
	}

	private sealed class FakeWorldNpcDropRegistrationLookup : IWorldNpcDropRegistrationLookup
	{
		private readonly HashSet<int> _objectIdsWithDrops;

		public FakeWorldNpcDropRegistrationLookup(IEnumerable<int> objectIdsWithDrops)
		{
			_objectIdsWithDrops = objectIdsWithDrops.ToHashSet();
		}

		public List<int> QueriedObjectIds { get; } = [];

		public bool HasRegisteredDrops(int npcObjectId)
		{
			QueriedObjectIds.Add(npcObjectId);
			return _objectIdsWithDrops.Contains(npcObjectId);
		}
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		private readonly object _gate = new();
		private readonly List<BroadcastRecord> _broadcasts = [];

		public IReadOnlyList<BroadcastRecord> Broadcasts
		{
			get
			{
				lock (_gate)
					return _broadcasts.ToArray();
			}
		}

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
			lock (_gate)
				_broadcasts.Add(new BroadcastRecord(sourcePosition, sourceObjectId, packet));
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
		GameServerPacket Packet);
}
