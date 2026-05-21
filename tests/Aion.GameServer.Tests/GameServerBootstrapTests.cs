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
	public async Task PeriodicSaveService_StoresServerLastRunPeriodicallyAndOnShutdown()
	{
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var repository = new TrackingServerVariablesRepository();
		var service = new PeriodicSaveService(
			repository,
			threadPoolManager,
			NullLogger<PeriodicSaveService>.Instance,
			TimeSpan.FromMilliseconds(10),
			TimeSpan.FromMilliseconds(10));

		await service.InitAsync(CancellationToken.None);
		await WaitUntilAsync(() => repository.StoreCalls > 0);
		var storeCallsBeforeShutdown = repository.StoreCalls;
		await service.ShutdownAsync(CancellationToken.None);

		Assert.Equal("PeriodicSaveService", service.Name);
		Assert.True(repository.StoredValues.ContainsKey("serverLastRun"));
		Assert.True(repository.StoreCalls > storeCallsBeforeShutdown);
	}

	[Fact]
	public async Task GameServerBootstrap_PreloadsUsedObjectIds()
	{
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

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		while (!condition())
		{
			await Task.Delay(10, timeout.Token);
		}
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
			return new StaticDataFixture(path);
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
