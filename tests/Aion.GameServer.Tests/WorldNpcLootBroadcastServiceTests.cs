using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcLootBroadcastServiceTests
{
	[Fact]
	public async Task StartRegisteredDropFanoutAsync_SendsInitialLootThenSchedulesFreeForAll()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		dropRegistration.RegisterDrop(
			5001,
			looterObjectId: 1001,
			drops: [new WorldNpcDropItem(1, 166020000, 1)]);
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var lootService = new WorldNpcLootService(dropRegistration, threadPoolManager: threadPoolManager);
			var registry = new CapturingConnectionRegistry();
			registry.OnlinePlayerObjectIds.Add(1001);
			var broadcastService = new WorldNpcLootBroadcastService(lootService, registry);
			var npc = CreateNpc(5001, race: "NONE");

			var result = await broadcastService.StartRegisteredDropFanoutAsync(
				npc,
				freeForAllDelay: TimeSpan.FromMilliseconds(10));

			Assert.Equal(new WorldNpcInitialLootBroadcastResult(
				Broadcasted: true,
				TargetCount: 1,
				SentCount: 1,
				WorldNpcInitialLootEnableStatus.Created), result.InitialLoot);
			Assert.True(result.FreeForAllScheduled);
			Assert.NotNull(result.FreeForAllTask);
			var completed = await Task.WhenAny(result.FreeForAllTask.Completion, Task.Delay(TimeSpan.FromSeconds(1)));
			Assert.Same(result.FreeForAllTask.Completion, completed);
			Assert.True(dropRegistration.TryGetRegistration(5001, out var registration));
			Assert.True(registration!.IsFreeForAll);
			Assert.Single(registry.SentPackets);
			var broadcastPacket = Assert.IsType<SmLootStatus>(registry.Packet);
			Assert.Equal(SmLootStatusType.LootEnable, broadcastPacket.Status);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task SendInitialLootEnableAsync_SendsLootStatusToAllowedLooters()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		dropRegistration.RegisterDrop(
			5001,
			looterObjectId: 1001,
			drops: [new WorldNpcDropItem(1, 166020000, 1)],
			allowedLooterObjectIds: [1002, 1003]);
		var lootService = new WorldNpcLootService(dropRegistration);
		var registry = new CapturingConnectionRegistry();
		registry.OnlinePlayerObjectIds.Add(1001);
		registry.OnlinePlayerObjectIds.Add(1002);
		var broadcastService = new WorldNpcLootBroadcastService(lootService, registry);

		var result = await broadcastService.SendInitialLootEnableAsync(5001);

		Assert.Equal(new WorldNpcInitialLootBroadcastResult(
			Broadcasted: true,
			TargetCount: 3,
			SentCount: 2,
			WorldNpcInitialLootEnableStatus.Created), result);
		Assert.Equal([1001, 1002], registry.SentPackets.Select(delivery => delivery.PlayerObjectId).Order());
		foreach (var delivery in registry.SentPackets)
		{
			var packet = Assert.IsType<SmLootStatus>(delivery.Packet);
			Assert.Equal(SmLootStatusType.LootEnable, packet.Status);
			Assert.Equal(1003, packet.LootEffectId);
		}
	}

	[Fact]
	public async Task SendInitialLootEnableAsync_SkipsMissingRegistration()
	{
		var lootService = new WorldNpcLootService(new WorldNpcDropRegistrationService());
		var registry = new CapturingConnectionRegistry();
		var broadcastService = new WorldNpcLootBroadcastService(lootService, registry);

		var result = await broadcastService.SendInitialLootEnableAsync(404);

		Assert.Equal(new WorldNpcInitialLootBroadcastResult(
			Broadcasted: false,
			TargetCount: 0,
			SentCount: 0,
			WorldNpcInitialLootEnableStatus.MissingRegistration), result);
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task BroadcastFreeForAllAsync_SendsLootEnableToVisiblePlayersWithRaceFilter()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		dropRegistration.RegisterDrop(
			5001,
			looterObjectId: 1001,
			drops: [new WorldNpcDropItem(1, 166020000, 1)]);
		var lootService = new WorldNpcLootService(dropRegistration);
		var registry = new CapturingConnectionRegistry { SentCount = 2 };
		var broadcastService = new WorldNpcLootBroadcastService(lootService, registry);
		var npc = CreateNpc(5001, race: "ASMODIANS");
		var freeForAll = lootService.StartFreeForAll(npc.ObjectId, npc);

		var result = await broadcastService.BroadcastFreeForAllAsync(freeForAll);

		Assert.Equal(new WorldNpcLootBroadcastResult(Broadcasted: true, SentCount: 2), result);
		Assert.Equal(npc.Position, registry.SourcePosition);
		Assert.Equal(npc.ObjectId, registry.SourceObjectId);
		var packet = Assert.IsType<SmLootStatus>(registry.Packet);
		Assert.Equal(SmLootStatusType.LootEnable, packet.Status);
		Assert.Equal(1003, packet.LootEffectId);
		Assert.NotNull(registry.Filter);
		Assert.False(registry.Filter!(CreatePlayer(1002, "ASMODIANS")));
		Assert.True(registry.Filter(CreatePlayer(1003, "ELYOS")));
	}

	[Fact]
	public async Task BroadcastFreeForAllAsync_SkipsMissingRegistration()
	{
		var lootService = new WorldNpcLootService(new WorldNpcDropRegistrationService());
		var registry = new CapturingConnectionRegistry();
		var broadcastService = new WorldNpcLootBroadcastService(lootService, registry);

		var result = await broadcastService.BroadcastFreeForAllAsync(WorldNpcFreeForAllResult.MissingRegistration());

		Assert.Equal(new WorldNpcLootBroadcastResult(Broadcasted: false, SentCount: 0), result);
		Assert.Null(registry.Packet);
	}

	[Fact]
	public async Task ScheduleFreeForAllBroadcast_StartsAndBroadcastsAfterDelay()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		dropRegistration.RegisterDrop(
			5001,
			looterObjectId: 1001,
			drops: [new WorldNpcDropItem(1, 166020000, 1)]);
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var lootService = new WorldNpcLootService(dropRegistration, threadPoolManager: threadPoolManager);
			var registry = new CapturingConnectionRegistry();
			var broadcastService = new WorldNpcLootBroadcastService(lootService, registry);
			var npc = CreateNpc(5001, race: "ELYOS");

			var scheduled = broadcastService.ScheduleFreeForAllBroadcast(npc, TimeSpan.FromMilliseconds(10));

			Assert.NotNull(scheduled);
			var completed = await Task.WhenAny(scheduled.Completion, Task.Delay(TimeSpan.FromSeconds(1)));
			Assert.Same(scheduled.Completion, completed);
			Assert.True(dropRegistration.TryGetRegistration(5001, out var registration));
			Assert.True(registration!.IsFreeForAll);
			var packet = Assert.IsType<SmLootStatus>(registry.Packet);
			Assert.Equal(SmLootStatusType.LootEnable, packet.Status);
			Assert.NotNull(registry.Filter);
			Assert.False(registry.Filter!(CreatePlayer(1002, "ELYOS")));
			Assert.True(registry.Filter(CreatePlayer(1003, "ASMODIANS")));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	private static Player CreatePlayer(int objectId, string race)
	{
		return new Player
		{
			ObjectId = objectId,
			Race = race,
		};
	}

	private static WorldNpc CreateNpc(int objectId, string race)
	{
		return new WorldNpc(
			objectId,
			203001,
			new NpcTemplateSummary(203001, "loot_npc", 0, 1, "NORMAL", "NORMAL", race, "NONE", "NON_ATTACKABLE"),
			new WorldPosition(210010000, 1, 2, 3, 0));
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public int SentCount { get; init; } = 1;

		public HashSet<int> OnlinePlayerObjectIds { get; } = [];

		public List<PacketDelivery> SentPackets { get; } = [];

		public WorldPosition? SourcePosition { get; private set; }

		public int SourceObjectId { get; private set; }

		public GameServerPacket? Packet { get; private set; }

		public Func<Player, bool>? Filter { get; private set; }

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
			SourcePosition = sourcePosition;
			SourceObjectId = sourceObjectId;
			Packet = packet;
			Filter = filter;
			return Task.FromResult(SentCount);
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
