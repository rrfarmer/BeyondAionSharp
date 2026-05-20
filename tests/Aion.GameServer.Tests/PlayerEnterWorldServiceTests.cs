using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerEnterWorldServiceTests
{
	[Fact]
	public async Task EnterWorld_MarksPlayerOnlineAndStoresInWorld()
	{
		var repository = new CapturingEnterWorldRepository
		{
			Player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-5)),
			Items = [new InventoryItem { ObjectId = 2001, ItemId = 100000094, IsEquipped = true, Location = 0, Slot = 1 }],
		};
		var world = CreateWorld();
		var service = CreateService(repository, world);

		var result = await service.EnterWorldAsync(accountId: 10, playerObjectId: 1001);

		Assert.Equal(EnterWorldCheckMessage.Ok, result.Message);
		Assert.Same(repository.Player, result.Player);
		Assert.True(repository.Player!.IsOnline);
		Assert.Single(repository.Player.InventoryItems);
		Assert.Equal(1, repository.LoadItemsCalls);
		Assert.Equal(1, repository.MarkOnlineCalls);
		Assert.True(world.TryGetObject(1001, out var stored));
		Assert.Same(repository.Player, stored);
	}

	[Fact]
	public async Task EnterWorld_ReturnsConnectionErrorWhenPlayerIsMissingOrDuplicate()
	{
		var repository = new CapturingEnterWorldRepository();
		var world = CreateWorld();
		var service = CreateService(repository, world);

		var missing = await service.EnterWorldAsync(accountId: 10, playerObjectId: 1001);

		Assert.Equal(EnterWorldCheckMessage.ConnectionError, missing.Message);

		repository.Player = CreatePlayer();
		world.TryAddObject(1001, new object());

		var duplicate = await service.EnterWorldAsync(accountId: 10, playerObjectId: 1001);

		Assert.Equal(EnterWorldCheckMessage.ConnectionError, duplicate.Message);
		Assert.Equal(0, repository.MarkOnlineCalls);
	}

	[Fact]
	public async Task EnterWorld_ReturnsReentryTimeForOnlineOrRecentPlayer()
	{
		var repository = new CapturingEnterWorldRepository { Player = CreatePlayer(isOnline: true) };
		var service = CreateService(repository, CreateWorld());

		var online = await service.EnterWorldAsync(accountId: 10, playerObjectId: 1001);

		Assert.Equal(EnterWorldCheckMessage.ReentryTime, online.Message);

		repository = new CapturingEnterWorldRepository { Player = CreatePlayer(lastOnline: DateTime.Now.AddSeconds(-5)) };
		service = CreateService(repository, CreateWorld());

		var recent = await service.EnterWorldAsync(accountId: 10, playerObjectId: 1001);

		Assert.Equal(EnterWorldCheckMessage.ReentryTime, recent.Message);
	}

	[Fact]
	public async Task EnterWorld_RemovesWorldObjectWhenOnlineUpdateFails()
	{
		var repository = new CapturingEnterWorldRepository
		{
			Player = CreatePlayer(lastOnline: DateTime.Now.AddMinutes(-5)),
			MarkOnlineResult = false,
		};
		var world = CreateWorld();
		var service = CreateService(repository, world);

		var result = await service.EnterWorldAsync(accountId: 10, playerObjectId: 1001);

		Assert.Equal(EnterWorldCheckMessage.ConnectionError, result.Message);
		Assert.False(world.TryGetObject(1001, out _));
	}

	private static PlayerEnterWorldService CreateService(CapturingEnterWorldRepository repository, GameWorld world)
	{
		return new PlayerEnterWorldService(
			new GameServerOptions(),
			repository,
			world,
			NullLogger<PlayerEnterWorldService>.Instance);
	}

	private static GameWorld CreateWorld()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		world.Initialize();
		return world;
	}

	private static Player CreatePlayer(bool isOnline = false, DateTime? lastOnline = null)
	{
		return new Player
		{
			ObjectId = 1001,
			AccountId = 10,
			Name = "Character",
			PlayerClass = "WARRIOR",
			Race = "ELYOS",
			Gender = "MALE",
			IsOnline = isOnline,
			LastOnline = lastOnline,
			Position = new WorldPosition(210010000, 1, 2, 3, 32),
		};
	}

	private sealed class CapturingEnterWorldRepository : IPlayerEnterWorldRepository
	{
		public Player? Player { get; set; }

		public bool MarkOnlineResult { get; init; } = true;

		public IReadOnlyList<InventoryItem> Items { get; init; } = Array.Empty<InventoryItem>();

		public int LoadItemsCalls { get; private set; }

		public int MarkOnlineCalls { get; private set; }

		public DateTime? LastOnline { get; private set; }

		public Task<Player?> LoadPlayerAsync(int accountId, int playerObjectId, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(Player);
		}

		public Task<IReadOnlyList<InventoryItem>> LoadPlayerItemsAsync(int playerObjectId, CancellationToken cancellationToken = default)
		{
			LoadItemsCalls++;
			return Task.FromResult(Items);
		}

		public Task<bool> MarkPlayerOnlineAsync(int playerObjectId, DateTime lastOnline, CancellationToken cancellationToken = default)
		{
			MarkOnlineCalls++;
			LastOnline = lastOnline;
			return Task.FromResult(MarkOnlineResult);
		}
	}
}
