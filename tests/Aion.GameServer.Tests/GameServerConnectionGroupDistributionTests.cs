using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionGroupDistributionTests
{
	private const int CubeStorageId = 0;
	private const int KinahItemId = 182400001;
	private const int TeamId = 700;

	[Fact]
	public async Task GroupDistribution_EvenSplit_MovesKinahAndSendsMessages()
	{
		var registry = new CapturingConnectionRegistry();
		var groupRuntime = new PlayerGroupRuntime();

		var distributor = CreateGroupedPlayer(5001, "Boss", kinah: 1000);
		var member = CreateGroupedPlayer(5002, "Mate", kinah: 50);
		groupRuntime.CreateOrUpdateGroup(TeamId, new[] { distributor, member });
		registry.OnlinePlayers.Add(distributor);
		registry.OnlinePlayers.Add(member);

		await using var pair = await TestConnectionPair.CreateAsync(registry, groupRuntime);

		// Distribute 90 among 2 -> 45 each. Distributor net: -90 + 45 = -45.
		await InvokeAsync(pair.Connection, distributor, amount: 90, partyType: 1);

		Assert.Equal(955, KinahOf(distributor)); // 1000 - 90 + 45
		Assert.Equal(95, KinahOf(member)); // 50 + 45

		// Distributor gets ME_TO_B; the other member gets B_TO_ME.
		Assert.Contains(registry.SentPackets, p => p.RecipientObjectId == 5001
			&& p.Packet is SmSystemMessage { MessageId: 1390247 });
		Assert.Contains(registry.SentPackets, p => p.RecipientObjectId == 5002
			&& p.Packet is SmSystemMessage { MessageId: 1390248 });
	}

	[Fact]
	public async Task GroupDistribution_UnevenSplit_TruncatesPerJava()
	{
		var registry = new CapturingConnectionRegistry();
		var groupRuntime = new PlayerGroupRuntime();
		var distributor = CreateGroupedPlayer(5001, "Boss", kinah: 1000);
		var m2 = CreateGroupedPlayer(5002, "A", kinah: 0);
		var m3 = CreateGroupedPlayer(5003, "B", kinah: 0);
		groupRuntime.CreateOrUpdateGroup(TeamId, new[] { distributor, m2, m3 });
		registry.OnlinePlayers.AddRange(new[] { distributor, m2, m3 });

		await using var pair = await TestConnectionPair.CreateAsync(registry, groupRuntime);

		// 100 / 3 = 33 (truncated). Distributor: 1000 - 100 + 33 = 933. Others: 33.
		await InvokeAsync(pair.Connection, distributor, amount: 100, partyType: 1);

		Assert.Equal(933, KinahOf(distributor));
		Assert.Equal(33, KinahOf(m2));
		Assert.Equal(33, KinahOf(m3));
	}

	[Fact]
	public async Task GroupDistribution_InsufficientKinah_SendsNotEnoughMoneyOnly()
	{
		var registry = new CapturingConnectionRegistry();
		var groupRuntime = new PlayerGroupRuntime();
		var distributor = CreateGroupedPlayer(5001, "Boss", kinah: 80);
		var member = CreateGroupedPlayer(5002, "Mate", kinah: 0);
		groupRuntime.CreateOrUpdateGroup(TeamId, new[] { distributor, member });
		registry.OnlinePlayers.AddRange(new[] { distributor, member });

		await using var pair = await TestConnectionPair.CreateAsync(registry, groupRuntime);

		await InvokeAsync(pair.Connection, distributor, amount: 90, partyType: 1);

		// No kinah moved.
		Assert.Equal(80, KinahOf(distributor));
		Assert.Equal(0, KinahOf(member));
		// STR_NOT_ENOUGH_MONEY (1300388) to distributor; no split messages.
		Assert.Contains(registry.SentPackets, p => p.Packet is SmSystemMessage { MessageId: 1300388 });
		Assert.DoesNotContain(registry.SentPackets, p => p.Packet is SmSystemMessage { MessageId: 1390247 });
		Assert.DoesNotContain(registry.SentPackets, p => p.Packet is SmSystemMessage { MessageId: 1390248 });
	}

	[Fact]
	public async Task GroupDistribution_AmountBelowTwo_DoesNothing()
	{
		var registry = new CapturingConnectionRegistry();
		var groupRuntime = new PlayerGroupRuntime();
		var distributor = CreateGroupedPlayer(5001, "Boss", kinah: 1000);
		var member = CreateGroupedPlayer(5002, "Mate", kinah: 0);
		groupRuntime.CreateOrUpdateGroup(TeamId, new[] { distributor, member });
		registry.OnlinePlayers.AddRange(new[] { distributor, member });

		await using var pair = await TestConnectionPair.CreateAsync(registry, groupRuntime);

		await InvokeAsync(pair.Connection, distributor, amount: 1, partyType: 1);

		Assert.Equal(1000, KinahOf(distributor));
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task GroupDistribution_AllianceVariant_Deferred_NoEffect()
	{
		var registry = new CapturingConnectionRegistry();
		var groupRuntime = new PlayerGroupRuntime();
		var distributor = CreateGroupedPlayer(5001, "Boss", kinah: 1000);
		var member = CreateGroupedPlayer(5002, "Mate", kinah: 0);
		groupRuntime.CreateOrUpdateGroup(TeamId, new[] { distributor, member });
		registry.OnlinePlayers.AddRange(new[] { distributor, member });

		await using var pair = await TestConnectionPair.CreateAsync(registry, groupRuntime);

		// partyType 2 (alliance) is deferred -> no effect.
		await InvokeAsync(pair.Connection, distributor, amount: 90, partyType: 2);

		Assert.Equal(1000, KinahOf(distributor));
		Assert.Empty(registry.SentPackets);
	}

	private static long KinahOf(Player player)
		=> player.InventoryItems.First(i => i.ItemId == KinahItemId && i.Location == CubeStorageId).Count;

	private static Player CreateGroupedPlayer(int objectId, string name, long kinah)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 50,
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
			IsOnline = true,
			TeamMembership = PlayerTeamMembership.Group,
			CurrentTeamId = TeamId,
			InventoryItems = new[]
			{
				new InventoryItem
				{
					ObjectId = objectId * 10,
					ItemId = KinahItemId,
					Count = kinah,
					OwnerId = objectId,
					Location = CubeStorageId,
					Slot = 0,
				},
			},
		};
	}

	private static async Task InvokeAsync(GameServerConnection connection, Player player, long amount, byte partyType)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"HandleGroupDistributionAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [player, amount, partyType, CancellationToken.None]));
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

		public static async Task<TestConnectionPair> CreateAsync(IGameClientConnectionRegistry registry, PlayerGroupRuntime groupRuntime)
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
					"group-distribution-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					connectionRegistry: registry,
					playerGroupRuntime: groupRuntime,
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
