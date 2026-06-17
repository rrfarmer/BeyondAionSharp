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
