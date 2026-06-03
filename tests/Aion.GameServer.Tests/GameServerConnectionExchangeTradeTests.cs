using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionExchangeTradeTests
{
	private const int CubeStorageId = 0;

	[Fact]
	public async Task ExecuteExchangeTrade_FullStackItems_MovesItemsBetweenPlayers()
	{
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry);

		var playerSword = CreateItem(objectId: 9001, itemId: 100000, count: 1, ownerId: 5001);
		var player = CreateTradingPlayer(5001, "Giver", partnerObjectId: 5002, items: [playerSword]);
		player.ExchangeItems[9001] = 1;

		var partnerShield = CreateItem(objectId: 9011, itemId: 110000, count: 1, ownerId: 5002);
		var partner = CreateTradingPlayer(5002, "Receiver", partnerObjectId: 5001, items: [partnerShield]);
		partner.ExchangeItems[9011] = 1;

		registry.OnlinePlayers.Add(player);
		registry.OnlinePlayers.Add(partner);

		await InvokeExecuteExchangeTradeAsync(pair.Connection, player);

		// Item moved from giver to receiver in memory.
		Assert.DoesNotContain(player.InventoryItems, i => i.ObjectId == 9001);
		Assert.Contains(partner.InventoryItems, i => i.ObjectId == 9001 && i.OwnerId == 5002 && i.Location == CubeStorageId);
		Assert.DoesNotContain(partner.InventoryItems, i => i.ObjectId == 9011);
		Assert.Contains(player.InventoryItems, i => i.ObjectId == 9011 && i.OwnerId == 5001);

		// Persistence-safety: traded-away items must NOT be tracked for deletion (the row now belongs to the partner).
		Assert.Empty(player.DeletedInventoryItems);
		Assert.Empty(partner.DeletedInventoryItems);

		// Both players received the success confirmation.
		Assert.Equal(2, registry.SentPackets.Count(p => p.Packet is SmExchangeConfirmation { Action: SmExchangeConfirmation.Success }));

		// Each giver received a delete packet for the item they handed over.
		Assert.Contains(registry.SentPackets, p => p.RecipientObjectId == 5001 && p.Packet is SmDeleteItem);
		Assert.Contains(registry.SentPackets, p => p.RecipientObjectId == 5002 && p.Packet is SmDeleteItem);

		// Exchange state cleared on both sides.
		Assert.False(player.IsTrading);
		Assert.False(partner.IsTrading);
		Assert.Empty(player.ExchangeItems);
		Assert.Empty(partner.ExchangeItems);
	}

	[Fact]
	public async Task ExecuteExchangeTrade_PartialStack_NoIdFactory_AbortsWithCancelAndKeepsItems()
	{
		var registry = new CapturingConnectionRegistry();
		// No IDFactory injected: a partial-stack split cannot allocate the receiver's new id, so the trade aborts cleanly.
		await using var pair = await TestConnectionPair.CreateAsync(registry);

		// Player commits 20 of a 50-count stack — a partial-stack trade.
		var stack = CreateItem(objectId: 9002, itemId: 100100, count: 50, ownerId: 5001);
		var player = CreateTradingPlayer(5001, "Giver", partnerObjectId: 5002, items: [stack]);
		player.ExchangeItems[9002] = 20;

		var partner = CreateTradingPlayer(5002, "Receiver", partnerObjectId: 5001, items: []);

		registry.OnlinePlayers.Add(player);
		registry.OnlinePlayers.Add(partner);

		await InvokeExecuteExchangeTradeAsync(pair.Connection, player);

		// Item not moved; both players still hold their inventories.
		Assert.Contains(player.InventoryItems, i => i.ObjectId == 9002 && i.Count == 50);
		Assert.DoesNotContain(partner.InventoryItems, i => i.ObjectId == 9002);

		// Trade aborted with a cancel to both, not a success.
		Assert.Equal(2, registry.SentPackets.Count(p => p.Packet is SmExchangeConfirmation { Action: SmExchangeConfirmation.Canceled }));
		Assert.DoesNotContain(registry.SentPackets, p => p.Packet is SmExchangeConfirmation { Action: SmExchangeConfirmation.Success });

		// State cleared.
		Assert.False(player.IsTrading);
		Assert.False(partner.IsTrading);
	}

	[Fact]
	public async Task ExecuteExchangeTrade_PartialStack_SplitsStackToReceiverWithNewId()
	{
		var registry = new CapturingConnectionRegistry();
		// Pre-lock the test object ids so the allocated split id cannot collide with them.
		var idFactory = new IDFactory([5001, 5002, 9002]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, idFactory);

		// Giver commits 20 of a 50-count stack; receiver brings nothing.
		var stack = CreateItem(objectId: 9002, itemId: 100100, count: 50, ownerId: 5001);
		var player = CreateTradingPlayer(5001, "Giver", partnerObjectId: 5002, items: [stack]);
		player.ExchangeItems[9002] = 20;

		var partner = CreateTradingPlayer(5002, "Receiver", partnerObjectId: 5001, items: []);

		registry.OnlinePlayers.Add(player);
		registry.OnlinePlayers.Add(partner);

		await InvokeExecuteExchangeTradeAsync(pair.Connection, player);

		// Giver keeps the original row with the reduced remainder (50 - 20 = 30).
		var giverRemainder = Assert.Single(player.InventoryItems, i => i.ObjectId == 9002);
		Assert.Equal(30, giverRemainder.Count);

		// Receiver gets a brand-new row (fresh id, not 9002) holding the committed 20 with correct ownership/location.
		var received = Assert.Single(partner.InventoryItems, i => i.ItemId == 100100);
		Assert.NotEqual(9002, received.ObjectId);
		Assert.Equal(20, received.Count);
		Assert.Equal(5002, received.OwnerId);
		Assert.Equal(CubeStorageId, received.Location);
		Assert.False(received.IsEquipped);

		// Persistence-safety: nothing tracked for deletion (the giver row survives; the receiver row is a fresh INSERT).
		Assert.Empty(player.DeletedInventoryItems);
		Assert.Empty(partner.DeletedInventoryItems);

		// Trade succeeded (success confirmation to both), not cancelled.
		Assert.Equal(2, registry.SentPackets.Count(p => p.Packet is SmExchangeConfirmation { Action: SmExchangeConfirmation.Success }));
		Assert.DoesNotContain(registry.SentPackets, p => p.Packet is SmExchangeConfirmation { Action: SmExchangeConfirmation.Canceled });

		// Java parity: the partial-stack branch decreases the source (DEC_ITEM_USE update) and never deletes the giver's
		// row, unlike the full-stack branch which sends SM_DELETE_ITEM. The absence of a giver delete confirms the split
		// branch was taken. (The DEC_ITEM_USE/PLAYER_EXCHANGE_GET packets themselves require an ItemTemplate, which this
		// focused harness does not load; their type constants are unit-verified elsewhere.)
		Assert.DoesNotContain(registry.SentPackets, p => p.RecipientObjectId == 5001 && p.Packet is SmDeleteItem);

		// Exchange state cleared.
		Assert.Empty(player.ExchangeItems);
		Assert.Empty(partner.ExchangeItems);
	}

	private static InventoryItem CreateItem(int objectId, int itemId, long count, int ownerId)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = count,
			OwnerId = ownerId,
			Location = CubeStorageId,
			Slot = 0,
		};
	}

	private static Player CreateTradingPlayer(int objectId, string name, int partnerObjectId, InventoryItem[] items)
	{
		var player = new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 50,
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
			InventoryItems = items,
			IsTrading = true,
			CurrentExchangePartnerObjectId = partnerObjectId,
		};
		return player;
	}

	private static async Task InvokeExecuteExchangeTradeAsync(GameServerConnection connection, Player player)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"ExecuteExchangeTradeAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [player, CancellationToken.None]));
		await task;
	}

	private sealed record SentPacketRecord(int RecipientObjectId, GameServerPacket Packet);

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<Player> OnlinePlayers { get; } = [];
		public List<SentPacketRecord> SentPackets { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection) { }
		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection) { }

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = OnlinePlayers.FirstOrDefault(candidate => string.Equals(candidate.Name, playerName, StringComparison.OrdinalIgnoreCase));
			return player != null;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
			foreach (var player in OnlinePlayers)
				action(player);
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			SentPackets.Add(new SentPacketRecord(playerObjectId, packet));
			return Task.FromResult(true);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null) => Task.FromResult(0);

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null) => Task.FromResult(0);

		public Task<int> RefreshHousingVisibilityAsync(IReadOnlyList<WorldHouse> houses, HousingTemplateTable? housingTemplates, int? playerObjectId = null) => Task.FromResult(0);

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null) => Task.FromResult(0);

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates) => Task.FromResult(0);

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail) => Task.FromResult(false);

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah) => Task.FromResult(false);
	}

	private sealed class TestConnectionPair : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private TestConnectionPair(TcpClient client, GameServerConnection connection)
		{
			_client = client;
			Connection = connection;
		}

		public GameServerConnection Connection { get; }

		public static async Task<TestConnectionPair> CreateAsync(IGameClientConnectionRegistry registry, IDFactory? idFactory = null)
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			try
			{
				var endpoint = (IPEndPoint)listener.LocalEndpoint;
				var client = new TcpClient();
				var acceptTask = listener.AcceptTcpClientAsync();
				await client.ConnectAsync(endpoint.Address, endpoint.Port);
				var serverClient = await acceptTask;
				var crypt = new GameCrypt(() => 0x01020304);
				crypt.EnableKey();
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"exchange-trade-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					connectionRegistry: registry,
					idFactory: idFactory,
					crypt: crypt);
				return new TestConnectionPair(client, connection);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			await Connection.DisposeAsync();
			_client.Dispose();
		}
	}
}
