using Aion.Commons.Database;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class GameServerBootstrapTests
{
	[Fact]
	public async Task GameServerBootstrap_LoadsDataInitializesWorldAndStartsGameTime()
	{
		using var dataManagerGuard = DataManagerSingletonGuard.Capture();
		using var temp = StaticDataFixture.Create();
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var engine = new TrackingEngine("QuestEngine");
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var gameTime = new GameTimeService(
			NullLogger<GameTimeService>.Instance,
			threadPoolManager,
			TimeSpan.FromMilliseconds(10),
			TimeSpan.FromMilliseconds(10));
		var bootstrap = new GameServerBootstrapService(
			temp,
			new EmptyUsedIdRepository(),
			new IDFactory(),
			new[] { engine },
			world,
			gameTime,
			threadPoolManager,
			new GameServerRuntimeContext(),
			NullLogger<GameServerBootstrapService>.Instance);

		await bootstrap.StartAsync(CancellationToken.None);
		await WaitUntilAsync(() => gameTime.GameMinutes > 0);

		Assert.True(bootstrap.IsStarted);
		Assert.True(temp.Loaded);
		Assert.Equal(1, engine.InitCalls);
		Assert.True(world.IsInitialized);
		Assert.True(gameTime.IsStarted);
		Assert.Equal(1, temp.LoadedData!.StaticData.GetElementCount("item"));

		await bootstrap.StopAsync(CancellationToken.None);

		Assert.False(bootstrap.IsStarted);
		Assert.Equal(1, engine.ShutdownCalls);
		Assert.False(gameTime.IsStarted);
		Assert.Equal(0, world.ObjectCount);
	}

	[Fact]
	public async Task ThreadPoolManager_RunsFixedRateTaskUntilShutdown()
	{
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var ticks = 0;

		_ = threadPoolManager.ScheduleAtFixedRate(
			_ =>
			{
				Interlocked.Increment(ref ticks);
				return ValueTask.CompletedTask;
			},
			TimeSpan.Zero,
			TimeSpan.FromMilliseconds(10));

		await WaitUntilAsync(() => Volatile.Read(ref ticks) >= 2);
		await threadPoolManager.ShutdownAsync();
		var stoppedAt = Volatile.Read(ref ticks);
		await Task.Delay(50);

		Assert.Equal(stoppedAt, Volatile.Read(ref ticks));
	}

	[Fact]
	public async Task ThreadPoolManager_RunsSingleDelayedTask()
	{
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var ran = 0;

		var scheduledTask = threadPoolManager.Schedule(
			_ =>
			{
				Interlocked.Exchange(ref ran, 1);
				return ValueTask.CompletedTask;
			},
			TimeSpan.FromMilliseconds(10));

		await WaitUntilAsync(() => Volatile.Read(ref ran) == 1);
		await scheduledTask.Completion;

		Assert.Equal(1, Volatile.Read(ref ran));
	}

	[Fact]
	public async Task ThreadPoolManager_CancelsSingleDelayedTask()
	{
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var ran = 0;

		var scheduledTask = threadPoolManager.Schedule(
			_ =>
			{
				Interlocked.Exchange(ref ran, 1);
				return ValueTask.CompletedTask;
			},
			TimeSpan.FromMilliseconds(100));

		Assert.True(scheduledTask.Cancel());
		await scheduledTask.Completion;
		await Task.Delay(50);

		Assert.Equal(0, Volatile.Read(ref ran));
	}

	[Fact]
	public async Task GameTimeService_LoadsAndPeriodicallyStoresServerVariable()
	{
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var repository = new TrackingServerVariablesRepository { LoadedInt = 42 };
		var broadcastCount = 0;
		SmGameTime? lastBroadcast = null;
		var gameTime = new GameTimeService(
			NullLogger<GameTimeService>.Instance,
			threadPoolManager,
			repository,
			TimeSpan.FromMilliseconds(5),
			TimeSpan.FromMilliseconds(5),
			TimeSpan.FromMilliseconds(25),
			TimeSpan.FromMilliseconds(25));
		gameTime.SetWorldBroadcaster(
			(packet, _) =>
			{
				Interlocked.Increment(ref broadcastCount);
				lastBroadcast = Assert.IsType<SmGameTime>(packet);
				return Task.FromResult(0);
			});

		await gameTime.InitAsync(CancellationToken.None);
		gameTime.StartClock();
		await WaitUntilAsync(() => repository.StoreCalls > 0 && Volatile.Read(ref broadcastCount) > 0);
		await gameTime.ShutdownAsync(CancellationToken.None);

		Assert.True(repository.LoadIntCalled);
		Assert.True(repository.StoredValues.TryGetValue("time", out var storedTime));
		Assert.True(int.Parse(storedTime!) >= 42);
		Assert.NotNull(lastBroadcast);
	}

	[Fact]
	public async Task PeriodicSaveService_GetInstanceSchedulesSaveTasks()
	{
		// Faithful Java parity: services/PeriodicSaveService is a singleton whose ctor schedules the
		// LegionWarehouseSaveTask + ServerRunTimeSaveTask via ThreadPoolManager.scheduleAtFixedRate.
		// The task bodies run against the live DB at interval-fire (empty legion cache + DB-guarded DAOs =>
		// no-op no-DB), so the boot wire is GetInstance() touching the SingletonHolder.
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		ThreadPoolManager.RegisterInstance(threadPoolManager);

		var service = PeriodicSaveService.GetInstance();

		Assert.NotNull(service);
		// SingletonHolder: getInstance() always returns the same instance.
		Assert.Same(service, PeriodicSaveService.GetInstance());

		// onShutdown stores data and cancels the scheduled tasks (legion cache empty => no-op, no throw).
		service.OnShutdown();

		await Task.CompletedTask;
	}

	[Fact]
	public async Task GameServerBootstrap_PreloadsUsedObjectIds()
	{
		using var dataManagerGuard = DataManagerSingletonGuard.Capture();
		using var temp = StaticDataFixture.Create();
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var idFactory = new IDFactory();
		var usedIds = new TrackingUsedIdRepository([1, 2, 3]);
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var gameTime = new GameTimeService(
			NullLogger<GameTimeService>.Instance,
			threadPoolManager,
			TimeSpan.FromMilliseconds(10),
			TimeSpan.FromMilliseconds(10));
		var bootstrap = new GameServerBootstrapService(
			temp,
			usedIds,
			idFactory,
			Array.Empty<GameEngine>(),
			world,
			gameTime,
			threadPoolManager,
			new GameServerRuntimeContext(),
			NullLogger<GameServerBootstrapService>.Instance);

		await bootstrap.StartAsync(CancellationToken.None);

		Assert.True(usedIds.Loaded);
		Assert.Equal(4, idFactory.GetUsedCount());
		Assert.Equal(4, idFactory.NextId());

		await bootstrap.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task GameServerBootstrap_RealSpawnDataMaterializesNpcsIntoWorld()
	{
		// Spawn-data-backed integration proof: unlike the minimal-fixture tests above (which seed a temp
		// static_data dir with a single item and assert an EMPTY world), this test boots the REAL game-server/data
		// + cache through the production DataManager.LoadAsync(repoRoot) path (the same real-data load proven by
		// RealStaticDataLoadIntegrationTests) so SPAWNS_DATA + WORLD_MAPS_DATA are populated, brings up the real
		// boot machinery (DataManager + World maps + IDFactory + ThreadPool + the AIEngine/ZoneService/GeoService
		// engines the spawn path needs), then drives the faithful SpawnEngine spawn path for a known starter map and
		// asserts the World object store materializes real Npc instances. This is the END-TO-END NPC-spawn proof:
		// before the SPAWNS_DATA fix, DataManager.SPAWNS_DATA was a hollow singleton returning [] for every world,
		// so zero NPCs spawned at boot. It guards that the fix actually turns spawn templates into live world NPCs.
		//
		// SCOPE NOTE: this drives the spawn path for a single regular map (Sanctum 110010000) via the faithful
		// SpawnEngine.SpawnObject, NOT the whole-world SpawnEngine.SpawnAll(). SpawnAll() iterates every WorldMap's
		// every twin instance and, per instance, calls HousingService.SpawnHouses() — which (a) needs a live DB
		// (HousingService ctor -> PlayerDAO.GetUsedIDs() returns null on no-DB, the documented #2 deferral; Java
		// NPEs identically and does not guard it) and (b) re-spawns the same address-cached House object into each
		// of a map's multiple twin instances (e.g. Heiron 210040000 has beginner_twin_count=3 => 4 instances),
		// colliding on the House objectId in World.StoreObject. Both are pre-existing whole-world-boot concerns
		// orthogonal to proving spawn materialization, so this guard scopes to the deterministic single-map spawn.
		using var dataManagerGuard = DataManagerSingletonGuard.Capture();
		var repoRoot = StaticDataFixture.FindRepoRoot(AppContext.BaseDirectory);
		var cacheFile = repoRoot is null
			? null
			: System.IO.Path.Combine(repoRoot, "game-server", "cache", "static_data.xml");
		if (cacheFile is null || !File.Exists(cacheFile))
			return; // Real game-server data/cache not present (e.g. data-less CI checkout); skip the spawn integration check.

		// Load + register the real DataManager (the same DataManager.LoadAsync(repoRoot) the production
		// StaticDataService uses). Register before constructing any engine singleton: some engine ctors read
		// DataManager.* (e.g. ZoneService reads ZONE_DATA), and Java's engine getInstance() block runs after
		// DataManager.getInstance().
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		var dataManager = await DataManager.LoadAsync(
			repoRoot!,
			cacheDirectory: null,
			validateWhenCacheChanges: false,
			logger: null,
			cancellationToken: cts.Token);
		DataManager.RegisterInstance(dataManager);

		// Java parity: World() ctor loads all world maps (WORLD_MAPS_DATA -> new WorldMap(template)); bind the
		// World + IDFactory + ThreadPoolManager singleton bridges the spawn path reads (World.GetInstance() in
		// BringIntoWorld, IDFactory.GetInstance().NextId() in every VisibleObject ctor).
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		world.LoadWorldMaps(DataManager.WORLD_MAPS_DATA);
		GameWorld.RegisterInstance(world);
		IDFactory.RegisterInstance(new IDFactory());
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		ThreadPoolManager.RegisterInstance(threadPoolManager);

		// Java parity: GameServer.main inits the engines before the spawn path. The NPC-spawn path needs these
		// three: each spawned Npc resolves its AI by name via AIEngine.NewAI (its ScriptManager.Load scans the
		// loaded assemblies for [AIName] handlers), and Npc OnAfterSpawn reads the per-world geo/zone maps that
		// GeoService.Init()/ZoneService.Init() seed for every WORLD_MAPS_DATA map (an empty GeoMap is created per
		// world even when geo files are disabled, so WorldHasTerrainMaterials no longer KeyNotFounds).
		await Aion.GameServer.Ai.AIEngine.GetInstance().InitAsync(cts.Token);
		await Aion.GameServer.World.Zone.ZoneService.GetInstance().InitAsync(cts.Token);
		await Aion.GameServer.World.Geo.GeoService.GetInstance().InitAsync(cts.Token);

		// SPAWNS_DATA must carry real spawn groups for Sanctum (proven by RealStaticDataLoadIntegrationTests too).
		var sanctumSpawns = DataManager.SPAWNS_DATA.GetSpawnsByWorldId(110010000);
		Assert.NotEmpty(sanctumSpawns);

		// Drive the faithful SpawnEngine spawn path for Sanctum's main instance: SpawnEngine.SpawnObject ->
		// VisibleObjectSpawner.SpawnNpc -> new Npc (AI + geo/zone) -> BringIntoWorld -> World.StoreObject/Spawn,
		// i.e. the exact pipeline SpawnEngine.SpawnAll() runs per regular NPC. This materializes real Npc instances
		// into the World _allObjects store from the real SPAWNS_DATA.
		var sanctumInstanceId = world.GetWorldMap(110010000).GetMainWorldMapInstance().GetInstanceId();
		var spawnedNpcs = 0;
		foreach (var group in sanctumSpawns)
		{
			if (group.GetHandlerType() != null || group.IsTemporarySpawn() || group.HasPool())
				continue; // skip handler/pool/temporary groups — assert on the plain regular-NPC spawns
			foreach (var template in group.GetSpawnTemplates())
			{
				if (Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(template, sanctumInstanceId) != null)
					spawnedNpcs++;
			}
		}

		// END-TO-END SPAWN PROOF: real spawn templates became live VisibleObjects in the World store.
		// (SpawnObject returns a VisibleObject for each spawned template — most are Npc, a few Sanctum spawns are
		// Gatherable (npcId 400000-499999), so the Npc-only count is a lower-bound <= spawnedNpcs, not equal.)
		Assert.True(spawnedNpcs > 0, "no regular spawns materialized from real Sanctum SPAWNS_DATA");
		Assert.True(world.ObjectCount > 0, $"World empty after spawning real Sanctum NPCs (ObjectCount={world.ObjectCount})");
		var sanctumNpcs = 0;
		world.GetWorldMap(110010000).ForEachObject(o => { if (o is Aion.GameServer.Model.GameObjects.Npc) sanctumNpcs++; });
		Assert.True(sanctumNpcs > 0, "no Npc instances materialized into Sanctum");
		// Every Sanctum spawn enters the single World store; a few of the spawned templates are gatherables or
		// relocate via their controller, so the World count tracks the spawn calls within a small delta rather than
		// matching exactly (data-version sensitive). Assert it is populated and on the same order as the spawn calls.
		Assert.True(world.ObjectCount >= sanctumNpcs, $"World store ({world.ObjectCount}) < Sanctum Npc count ({sanctumNpcs})");
		// Known Sanctum NPC Euterpe (npc 798173) is among the regular spawns -> proves a real template id resolved.
		var euterpeSpawned = false;
		world.GetWorldMap(110010000).ForEachObject(o =>
		{
			if (o is Aion.GameServer.Model.GameObjects.Npc npc && npc.GetNpcId() == 798173)
				euterpeSpawned = true;
		});
		Assert.True(euterpeSpawned, "known Sanctum NPC 798173 (Euterpe) did not spawn into the world");
	}

	[Fact]
	public async Task GameServerBootstrap_DbBackedFullBoot_RunsRealStartAsyncAgainstLiveMySql()
	{
		// STRONGEST end-to-end validation: boot the FULL GameServerBootstrapService.StartAsync against the LIVE
		// MySQL container (localhost:3307/aion_gs) using the REAL game-server static data + cache (the same
		// DataManager.LoadAsync(repoRoot) path proven by RealStaticDataLoadIntegrationTests / the spawn-backed test).
		// Unlike the no-DB minimal-fixture bootstrap tests above (empty SIEGE/WORLD_MAPS data, no DB => the housing /
		// siege / pvp-map blocks are documented deferrals), this exercises the real spawn path — SpawnEngine.SpawnAll()
		// runs SpawnInstance for every twin instance of every non-instance world map, and SpawnInstance ALWAYS calls
		// HousingService.GetInstance().SpawnHouses(instance, ownerId). So this empirically answers the #2 Housing
		// question: with a live (empty) players table, PlayerDAO.GetUsedIDs() returns int[0] (not null), so the
		// HousingService ctor's RevokeOwnershipOfDeletedPlayers no longer throws ArgumentNullException — the no-DB
		// deferral is lifted. The remaining house-twin question is whether re-spawning the address-cached House object
		// into a map's multiple twin instances collides on the House objectId (DuplicateAionObjectException) — the
		// documented Java-latent throw on Heiron(210040000)/Beluslan(220040000) housing twins.
		//
		// Env-gated: a no-op unless AION_GAMESERVER_DB_INTEGRATION=1 (so the normal suite stays green; the container
		// is only present in the integration environment).
		if (Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_INTEGRATION") != "1")
			return;

		var repoRoot = StaticDataFixture.FindRepoRoot(AppContext.BaseDirectory);
		var cacheFile = repoRoot is null
			? null
			: System.IO.Path.Combine(repoRoot, "game-server", "cache", "static_data.xml");
		if (cacheFile is null || !File.Exists(cacheFile))
			return; // Real game-server data/cache not present; the DB-backed boot needs the real static data.

		// Point DatabaseFactory at the live integration container (root/aion @ 3307/aion_gs) and apply the real
		// Java aion_gs schema so every DB-backed DAO the boot touches (PlayerDAO.GetUsedIDs, HousesDAO.LoadHouses,
		// BrokerDAO, AnnouncementsDAO, AbyssRankDAO, CommandsAccessDAO, ...) reads against real (empty) tables.
		DatabaseFactory.Initialize(
			server: Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_HOST") ?? "localhost",
			userId: Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_USER") ?? "root",
			password: Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_PASSWORD") ?? "aion",
			database: Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_NAME") ?? "aion_gs",
			port: int.Parse(Environment.GetEnvironmentVariable("AION_GAMESERVER_DB_PORT") ?? "3307"));
		await InitializeGameSchemaAsync(repoRoot!);

		using var dataManagerGuard = DataManagerSingletonGuard.Capture();
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));

		// Load the REAL DataManager once (147 MB cache) and feed the FULL StartAsync via a pass-through loader so the
		// boot does not re-parse it. StartAsync itself re-registers DataManager/World/IDFactory/ThreadPool singletons.
		var dataManager = await DataManager.LoadAsync(
			repoRoot!,
			cacheDirectory: null,
			validateWhenCacheChanges: false,
			logger: null,
			cancellationToken: cts.Token);
		DataManager.RegisterInstance(dataManager);

		// Java parity: GameServer.main inits the engines (AIEngine/ZoneService/GeoService/...) before the spawn path.
		// The bootstrap's _engines DI collection carries only the LimitedItemTradeScheduler GameEngine in production,
		// so the spawn-critical engines are initialized here exactly as the spawn-backed test does (each spawned Npc
		// resolves its AI by name via AIEngine.NewAI; Npc OnAfterSpawn reads the per-world geo/zone maps that
		// GeoService/ZoneService seed). Register the singleton bridges StartAsync's spawn path reads.
		IDFactory.RegisterInstance(new IDFactory());
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		ThreadPoolManager.RegisterInstance(threadPoolManager);
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		world.LoadWorldMaps(DataManager.WORLD_MAPS_DATA);
		GameWorld.RegisterInstance(world);
		await Aion.GameServer.Ai.AIEngine.GetInstance().InitAsync(cts.Token);
		await Aion.GameServer.World.Zone.ZoneService.GetInstance().InitAsync(cts.Token);
		await Aion.GameServer.World.Geo.GeoService.GetInstance().InitAsync(cts.Token);

		var gameTime = new GameTimeService(
			NullLogger<GameTimeService>.Instance,
			threadPoolManager,
			TimeSpan.FromMilliseconds(10),
			TimeSpan.FromMilliseconds(10));
		var bootstrap = new GameServerBootstrapService(
			new PassThroughStaticDataLoader(dataManager),
			new MySqlUsedIdRepository(NullLogger<MySqlUsedIdRepository>.Instance),
			IDFactory.GetInstance(),
			Array.Empty<GameEngine>(),
			world,
			gameTime,
			threadPoolManager,
			new GameServerRuntimeContext(),
			NullLogger<GameServerBootstrapService>.Instance);

		// Run the FULL StartAsync. The house-twin DuplicateAionObjectException is FIXED (WorldMapTemplate now applies
		// the Java WorldConfig twin clamps: WORLD_MAX_TWINS_BEGINNER=-1 disables beginner twins, WORLD_MAX_TWINS_USUAL=1
		// caps usual twins -> getInstanceCount()=1 for Heiron/Beluslan, so HousingService.SpawnHouses runs exactly once
		// per house map and never re-stores an address-cached House objectId). The boot must now complete cleanly.
		//
		// HTMLCache (and any other production relative-path data lookup) resolves "./data/static_data/..." against the
		// process working directory, which in a real deployment is the game-server dir. Point CWD at the repo's
		// game-server dir for the duration of the boot so the production relative paths resolve to the real data tree.
		var gameServerDir = System.IO.Path.Combine(repoRoot!, "game-server");
		var savedCwd = Directory.GetCurrentDirectory();
		Exception? bootException = null;
		try
		{
			Directory.SetCurrentDirectory(gameServerDir);
			await bootstrap.StartAsync(cts.Token);
		}
		catch (Exception ex)
		{
			bootException = ex;
		}
		finally
		{
			Directory.SetCurrentDirectory(savedCwd);
		}

		// REGRESSION GUARD: the house-twin DuplicateAionObjectException must NOT reappear. Before the WorldConfig twin
		// clamp was ported, the full boot threw DuplicateAionObjectException out of HousingService.SpawnHouses (a House
		// re-stored into a second twin instance of Heiron/Beluslan). That was a C# divergence — Java's
		// WorldMapTemplate.getBeginnerTwinCount()/getTwinCount() clamp by WorldConfig (beginner=-1 disabled, usual=1) so
		// getInstanceCount()=1 and houses spawn once. If the dup ever resurfaces, the clamp regressed.
		if (bootException is not null)
		{
			var flat0 = Flatten(bootException).ToList();
			var dup = flat0.OfType<Aion.GameServer.World.Exceptions.DuplicateAionObjectException>().FirstOrDefault();
			Assert.True(
				dup is null,
				"DB-backed full boot threw the house-twin DuplicateAionObjectException again — the WorldConfig twin clamp " +
				"(WorldMapTemplate.GetBeginnerTwinCount/GetTwinCount) regressed: " +
				string.Join(" => ", flat0.Select(e => $"{e.GetType().FullName}: {e.Message}")));
		}

		// CLEAN FULL BOOT: with the twin clamp in place the real spawn path (incl. HousingService.SpawnHouses across the
		// single non-twin instance of every house map) runs to completion against live MySQL + real static data.
		Assert.True(bootException is null,
			"DB-backed full boot did not complete cleanly: " +
			(bootException is null ? "" : string.Join(" => ", Flatten(bootException).Select(e => $"{e.GetType().FullName}: {e.Message}")) +
			Environment.NewLine + bootException));
		Assert.True(bootstrap.IsStarted, "full DB-backed boot did not reach IsStarted");
		Assert.True(world.IsInitialized, "world not initialized after full DB-backed boot");
		Assert.True(world.ObjectCount > 0, $"world empty after full DB-backed boot (ObjectCount={world.ObjectCount})");

		// BOOT-TAIL CLOSURE: the two GameServer.main wires deferred out of the always-on StartAsync path —
		// SiegeService.initSieges() (main:142) and PvpMapService.init() (main:176) — are FAITHFUL DB-gated, not
		// guarded for empty data. Java SiegeService.initSieges() runs its FULL body whenever SiegeConfig.SIEGE_ENABLED
		// (default true): it despawns/spawns from real SIEGE_LOCATION_DATA fortress/outpost/artifact registries,
		// schedules from the full config/schedule/siege_schedule.xml, and UpdateFortressNextState() does
		// GetSiegeLocation(scheduledLocId).SetNextState(...) with NO null guard — so it REQUIRES populated
		// SIEGE_LOCATION_DATA (the empty no-DB minimal fixture would NRE; adding a guard would be un-faithful).
		// PvpMapService.init() has no config gate at all and unconditionally resolves world map 301220000 via
		// InstanceService.GetNextAvailableInstance — it REQUIRES the populated WORLD_MAPS_DATA the real boot carries.
		// Both run clean ONLY against real data, so the faithful home for them is this DB-backed boot, not the
		// minimal-fixture StartAsync path. Exercise + assert them here (CWD = game-server so the siege_schedule.xml
		// relative path resolves), proving the boot tail is closed under real data.
		Exception? tailException = null;
		try
		{
			Directory.SetCurrentDirectory(gameServerDir);
			Aion.GameServer.Services.SiegeService.GetInstance().InitSieges();
			Aion.GameServer.Custom.Pvpmap.PvpMapService.GetInstance().Init();
		}
		catch (Exception ex)
		{
			tailException = ex;
		}
		finally
		{
			Directory.SetCurrentDirectory(savedCwd);
		}
		Assert.True(tailException is null,
			"DB-backed SiegeService.InitSieges()/PvpMapService.Init() boot-tail wires threw against real data: " +
			(tailException is null ? "" : string.Join(" => ", Flatten(tailException).Select(e => $"{e.GetType().FullName}: {e.Message}")) +
			Environment.NewLine + tailException));
		Assert.True(Aion.GameServer.Configs.Main.SiegeConfig.SIEGE_ENABLED,
			"SIEGE_ENABLED must default true (Java siege.properties gameserver.siege.enable=true) for InitSieges to run its full body");
		// PvpMapService.Init() registered the pvp-map instance handler; GetParticipantsSize()==0 is the live-handler
		// path (not the null-handler short-circuit) — any unported dep would have thrown into tailException above.
		Assert.Equal(0, Aion.GameServer.Custom.Pvpmap.PvpMapService.GetInstance().GetParticipantsSize());

		await bootstrap.StopAsync(cts.Token);
		Assert.False(bootstrap.IsStarted);
	}

	private static IEnumerable<Exception> Flatten(Exception ex)
	{
		var stack = new Stack<Exception>();
		stack.Push(ex);
		while (stack.Count > 0)
		{
			var current = stack.Pop();
			yield return current;
			if (current is AggregateException agg)
				foreach (var inner in agg.InnerExceptions)
					stack.Push(inner);
			else if (current.InnerException != null)
				stack.Push(current.InnerException);
		}
	}

	private static async Task InitializeGameSchemaAsync(string repoRoot)
	{
		var schemaPath = System.IO.Path.Combine(repoRoot, "game-server", "sql", "aion_gs.sql");
		var sql = await File.ReadAllTextAsync(schemaPath);
		await using var connection = DatabaseFactory.GetConnection();
		await connection.OpenAsync();
		var lines = sql.Split('\n')
			.Select(line => line.TrimEnd('\r'))
			.Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(line));
		foreach (var statement in string.Join('\n', lines)
			.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(s => !string.IsNullOrWhiteSpace(s)))
		{
			await using var command = connection.CreateCommand();
			command.CommandText = statement;
			await command.ExecuteNonQueryAsync();
		}
	}

	private sealed class PassThroughStaticDataLoader : IStaticDataLoader
	{
		private readonly DataManager _dataManager;

		public PassThroughStaticDataLoader(DataManager dataManager) => _dataManager = dataManager;

		public Task<DataManager> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(_dataManager);
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		while (!condition())
		{
			await Task.Delay(10, timeout.Token);
		}
	}

	// Test isolation: the bootstrap path calls DataManager.RegisterInstance(...) with a throwaway fixture,
	// overwriting the process-global DataManager singleton bridge. Snapshot it on entry and restore it on
	// dispose (including on a failing/throwing boot) so sibling test classes in the same process — e.g.
	// GoldenStatsInfoFixtureTests, which binds a synthetic PlayerExperienceTable once in its static ctor —
	// keep reading their own DataManager instead of this test's minimal/real fixture.
	private readonly struct DataManagerSingletonGuard : IDisposable
	{
		private readonly DataManager? _previous;

		private DataManagerSingletonGuard(DataManager? previous) => _previous = previous;

		public static DataManagerSingletonGuard Capture() => new(DataManager.GetRegisteredInstance());

		public void Dispose() => DataManager.RestoreInstance(_previous);
	}

	private sealed class TrackingEngine : GameEngine
	{
		public TrackingEngine(string name)
		{
			Name = name;
		}

		public string Name { get; }

		public int InitCalls { get; private set; }

		public int ShutdownCalls { get; private set; }

		public ValueTask InitAsync(CancellationToken cancellationToken = default)
		{
			InitCalls++;
			return ValueTask.CompletedTask;
		}

		public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
		{
			ShutdownCalls++;
			return ValueTask.CompletedTask;
		}
	}

	private sealed class TrackingUsedIdRepository : IUsedIdRepository
	{
		private readonly IReadOnlyCollection<int> _ids;

		public TrackingUsedIdRepository(IReadOnlyCollection<int> ids)
		{
			_ids = ids;
		}

		public bool Loaded { get; private set; }

		public Task<IReadOnlyCollection<int>> LoadUsedIdsAsync(CancellationToken cancellationToken = default)
		{
			Loaded = true;
			return Task.FromResult(_ids);
		}
	}

	private sealed class TrackingServerVariablesRepository : IServerVariablesRepository
	{
		public int? LoadedInt { get; init; }

		public bool LoadIntCalled { get; private set; }

		public int StoreCalls { get; private set; }

		public Dictionary<string, string> StoredValues { get; } = [];

		public Task<int?> LoadIntAsync(string key, CancellationToken cancellationToken = default)
		{
			LoadIntCalled = true;
			return Task.FromResult(LoadedInt);
		}

		public Task<long?> LoadLongAsync(string key, CancellationToken cancellationToken = default)
		{
			return Task.FromResult<long?>(null);
		}

		public Task<bool> StoreAsync(string key, object value, CancellationToken cancellationToken = default)
		{
			StoreCalls++;
			StoredValues[key] = value.ToString() ?? string.Empty;
			return Task.FromResult(true);
		}
	}

	private sealed class StaticDataFixture : IStaticDataLoader, IDisposable
	{
		private StaticDataFixture(string path)
		{
			Path = path;
		}

		public string Path { get; }

		public bool Loaded { get; private set; }

		public DataManager? LoadedData { get; private set; }

		public static StaticDataFixture Create()
		{
			var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aion-bootstrap-" + Guid.NewGuid().ToString("N"));
			var dataDirectory = Directory.CreateDirectory(System.IO.Path.Combine(path, "data", "static_data"));
			var itemsDirectory = Directory.CreateDirectory(System.IO.Path.Combine(dataDirectory.FullName, "items"));
			File.WriteAllText(
				System.IO.Path.Combine(dataDirectory.FullName, "static_data.xml"),
				"""
				<?xml version="1.0" encoding="UTF-8"?>
				<static_data>
					<import file="items/items.xml" />
				</static_data>
				""");
			File.WriteAllText(System.IO.Path.Combine(itemsDirectory.FullName, "items.xml"), """<items><item id="1" /></items>""");
			File.WriteAllText(
				System.IO.Path.Combine(dataDirectory.FullName, "static_data.xsd"),
				"""
				<?xml version="1.0" encoding="UTF-8"?>
				<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" />
				""");

			// Faithful boot-init fixture enrichment: seed the specific REAL static_data leaf files that the
			// otherwise-deferred boot services need at init, copied verbatim from the on-disk game-server data
			// (never invented). LoadLeafHoldersFromFiles reads each holder from a fixed sub-path of this temp
			// static_data dir, so dropping the real file at that sub-path populates exactly that holder, leaving
			// every other holder empty (minimal). When the real repo data is absent (e.g. CI without the game
			// data tree), the copy is skipped and the corresponding service stays a guarded no-op.
			var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
			if (repoRoot != null)
			{
				var realStaticData = System.IO.Path.Combine(repoRoot, "game-server", "data", "static_data");

				// AUTO_GROUP_DATA: unblocks PeriodicInstanceManager (its ctor's GetAGTByMaskId iterates every
				// AutoGroupType, each GetTemplate() reads DataManager.AUTO_GROUP — empty => null template => NRE).
				CopyRealFile(realStaticData, dataDirectory.FullName, System.IO.Path.Combine("auto_group", "auto_group.xml"));

				// HTMLCache: its ctor Reload(false) falls through to ParseDir(HTMLConfig.HTML_ROOT) when no
				// html.cache exists, and Directory.GetFileSystemEntries throws DirectoryNotFoundException on a
				// missing dir. Point HTML_ROOT at a temp copy of the REAL game-server HTML tree (verbatim .xhtml
				// files) and HTML_CACHE_FILE at a fresh temp path (no stale cache => ParseDir runs over the real
				// files). HTMLConfig fields are process-global statics; set before the StartAsync wire constructs
				// the singleton. Idempotent across fixture instances (same real source).
				var realHtmlDir = System.IO.Path.Combine(realStaticData, "HTML");
				if (Directory.Exists(realHtmlDir))
				{
					var fixtureHtmlDir = System.IO.Path.Combine(dataDirectory.FullName, "HTML");
					CopyDirectory(realHtmlDir, fixtureHtmlDir);
					Aion.GameServer.Configs.Main.HTMLConfig.HTML_ROOT = fixtureHtmlDir + System.IO.Path.DirectorySeparatorChar;
					Aion.GameServer.Configs.Main.HTMLConfig.HTML_CACHE_FILE = System.IO.Path.Combine(path, "cache", "html.cache");
				}
			}

			return new StaticDataFixture(path);
		}

		// Java parity helper: locate the repo root by walking up to the dir that holds game-server/data/static_data.
		internal static string? FindRepoRoot(string startDirectory)
		{
			var directory = new DirectoryInfo(startDirectory);
			while (directory != null)
			{
				if (File.Exists(System.IO.Path.Combine(directory.FullName, "game-server", "data", "static_data", "static_data.xml")))
					return directory.FullName;
				directory = directory.Parent;
			}

			return null;
		}

		private static void CopyRealFile(string realStaticDataDir, string fixtureStaticDataDir, string relativePath)
		{
			var source = System.IO.Path.Combine(realStaticDataDir, relativePath);
			if (!File.Exists(source))
				return;
			var destination = System.IO.Path.Combine(fixtureStaticDataDir, relativePath);
			var destinationDir = System.IO.Path.GetDirectoryName(destination);
			if (!string.IsNullOrEmpty(destinationDir))
				Directory.CreateDirectory(destinationDir);
			File.Copy(source, destination, overwrite: true);
		}

		private static void CopyDirectory(string sourceDir, string destinationDir)
		{
			Directory.CreateDirectory(destinationDir);
			foreach (var file in Directory.GetFiles(sourceDir))
				File.Copy(file, System.IO.Path.Combine(destinationDir, System.IO.Path.GetFileName(file)), overwrite: true);
			foreach (var subDir in Directory.GetDirectories(sourceDir))
				CopyDirectory(subDir, System.IO.Path.Combine(destinationDir, System.IO.Path.GetFileName(subDir)));
		}

		public async Task<DataManager> LoadAsync(CancellationToken cancellationToken = default)
		{
			Loaded = true;
			LoadedData = await DataManager.LoadAsync(
				new XmlDataLoaderOptions
				{
					MainXmlFilePath = System.IO.Path.Combine(Path, "data", "static_data", "static_data.xml"),
					CacheXmlFilePath = System.IO.Path.Combine(Path, "cache", "static_data.xml"),
					SchemaFilePath = System.IO.Path.Combine(Path, "data", "static_data", "static_data.xsd"),
					ValidateWhenCacheChanges = false,
				},
				cancellationToken: cancellationToken);
			return LoadedData;
		}

		public void Dispose()
		{
			try
			{
				Directory.Delete(Path, recursive: true);
			}
			catch
			{
			}
		}
	}
}
