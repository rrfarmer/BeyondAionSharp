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

public sealed class GameServerConnectionAllianceDistributionTests
{
	private const int CubeStorageId = 0;
	private const int KinahItemId = 182400001;
	private const int AllianceId = 900;

	[Fact]
	public async Task AllianceDistribution_PartyType2_DistributesAcrossWholeAlliance()
	{
		var registry = new CapturingConnectionRegistry();
		var allianceRuntime = new PlayerAllianceRuntime();

		var distributor = CreateAllianceePlayer(6001, "Cap", kinah: 1000);
		var m2 = CreateAllianceePlayer(6002, "A", kinah: 0);
		var m3 = CreateAllianceePlayer(6003, "B", kinah: 0);
		allianceRuntime.CreateAlliance(AllianceId, distributor);
		allianceRuntime.AddMember(AllianceId, m2);
		allianceRuntime.AddMember(AllianceId, m3);
		registry.OnlinePlayers.AddRange(new[] { distributor, m2, m3 });

		await using var pair = await TestConnectionPair.CreateAsync(registry, allianceRuntime);

		// 90 / 3 = 30 each. Distributor net: -90 + 30 = -60.
		await InvokeAsync(pair.Connection, distributor, amount: 90, partyType: 2);

		Assert.Equal(940, KinahOf(distributor)); // 1000 - 90 + 30
		Assert.Equal(30, KinahOf(m2));
		Assert.Equal(30, KinahOf(m3));
		Assert.Contains(registry.SentPackets, p => p.RecipientObjectId == 6001 && p.Packet is SmSystemMessage { MessageId: 1390247 });
		Assert.Contains(registry.SentPackets, p => p.RecipientObjectId == 6002 && p.Packet is SmSystemMessage { MessageId: 1390248 });
		Assert.Contains(registry.SentPackets, p => p.RecipientObjectId == 6003 && p.Packet is SmSystemMessage { MessageId: 1390248 });
	}

	[Fact]
	public async Task AllianceDistribution_PartyType1InAlliance_DistributesToSubGroup()
	{
		var registry = new CapturingConnectionRegistry();
		var allianceRuntime = new PlayerAllianceRuntime();

		var distributor = CreateAllianceePlayer(6001, "Cap", kinah: 600);
		var m2 = CreateAllianceePlayer(6002, "A", kinah: 0);
		allianceRuntime.CreateAlliance(AllianceId, distributor);
		allianceRuntime.AddMember(AllianceId, m2);
		registry.OnlinePlayers.AddRange(new[] { distributor, m2 });

		await using var pair = await TestConnectionPair.CreateAsync(registry, allianceRuntime);

		// partyType 1 while in an alliance -> distributeKinahInGroup (the distributor's sub-group). Both default to group 1000.
		// 60 / 2 = 30 each. Distributor net: -60 + 30 = -30.
		await InvokeAsync(pair.Connection, distributor, amount: 60, partyType: 1);

		Assert.Equal(570, KinahOf(distributor)); // 600 - 60 + 30
		Assert.Equal(30, KinahOf(m2));
		Assert.Contains(registry.SentPackets, p => p.RecipientObjectId == 6001 && p.Packet is SmSystemMessage { MessageId: 1390247 });
		Assert.Contains(registry.SentPackets, p => p.RecipientObjectId == 6002 && p.Packet is SmSystemMessage { MessageId: 1390248 });
	}

	[Fact]
	public async Task AllianceDistribution_ExcludesOfflineMembers()
	{
		var registry = new CapturingConnectionRegistry();
		var allianceRuntime = new PlayerAllianceRuntime();

		var distributor = CreateAllianceePlayer(6001, "Cap", kinah: 1000);
		var online = CreateAllianceePlayer(6002, "On", kinah: 0);
		var offline = CreateAllianceePlayer(6003, "Off", kinah: 0);
		offline.IsOnline = false;
		allianceRuntime.CreateAlliance(AllianceId, distributor);
		allianceRuntime.AddMember(AllianceId, online);
		allianceRuntime.AddMember(AllianceId, offline);
		registry.OnlinePlayers.AddRange(new[] { distributor, online, offline });

		await using var pair = await TestConnectionPair.CreateAsync(registry, allianceRuntime);

		// Only 2 online members -> 90 / 2 = 45. Distributor net: -90 + 45 = -45.
		await InvokeAsync(pair.Connection, distributor, amount: 90, partyType: 2);

		Assert.Equal(955, KinahOf(distributor)); // 1000 - 90 + 45
		Assert.Equal(45, KinahOf(online));
		Assert.Equal(0, KinahOf(offline)); // offline member excluded
		Assert.DoesNotContain(registry.SentPackets, p => p.RecipientObjectId == 6003);
	}

	[Fact]
	public async Task AllianceDistribution_NotInAlliance_PartyType2_DoesNothing()
	{
		var registry = new CapturingConnectionRegistry();
		var allianceRuntime = new PlayerAllianceRuntime();

		// A player whose membership is None should not distribute via the alliance path.
		var distributor = CreateAllianceePlayer(6001, "Cap", kinah: 1000);
		distributor.TeamMembership = PlayerTeamMembership.None;
		distributor.CurrentTeamId = 0;
		registry.OnlinePlayers.Add(distributor);

		await using var pair = await TestConnectionPair.CreateAsync(registry, allianceRuntime);

		await InvokeAsync(pair.Connection, distributor, amount: 90, partyType: 2);

		Assert.Equal(1000, KinahOf(distributor));
		Assert.Empty(registry.SentPackets);
	}

	private static long KinahOf(Player player)
		=> player.InventoryItems.First(i => i.ItemId == KinahItemId && i.Location == CubeStorageId).Count;

	private static Player CreateAllianceePlayer(int objectId, string name, long kinah)
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
			TeamMembership = PlayerTeamMembership.Alliance,
			CurrentTeamId = AllianceId,
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

		public static async Task<TestConnectionPair> CreateAsync(IGameClientConnectionRegistry registry, PlayerAllianceRuntime allianceRuntime)
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
					"alliance-distribution-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					connectionRegistry: registry,
					playerAllianceRuntime: allianceRuntime,
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
