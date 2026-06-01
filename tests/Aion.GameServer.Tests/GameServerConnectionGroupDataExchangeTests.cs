using System.Net;
using System.Net.Sockets;
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

public sealed class GameServerConnectionGroupDataExchangeTests
{
	[Fact]
	public async Task ProcessPacketAsync_NearbyActionComposesDisabledBroadcastBoundaryWithoutSending()
	{
		var registry = new CapturingConnectionRegistry();
		await using var fixture = await GroupDataExchangeFixture.CreateAsync(registry);
		var player = CreatePlayer(1001);
		GameServerConnectionBuyItemTests.SetActivePlayerForPacketDispatchForAdapterTests(fixture.Connection, player);

		await GameServerConnectionBuyItemTests.InvokeProcessPacketAsyncForAdapterTests(
			fixture.Connection,
			CreateGroupDataExchangePayload(action: 1, groupType: 0, unknown2: 0, [1, 2, 3]));

		var plan = Assert.Single(fixture.CompositionPlans);
		Assert.Equal(GroupDataExchangeFanoutPlanStatus.NearbyBroadcastVisiblePlayersAndSelf, plan.FanoutPlan.Status);
		Assert.Equal(GroupDataExchangeFanoutSocketAdapterStatus.DisabledNoSend, plan.SocketAdapterResult.Status);
		Assert.True(plan.SocketAdapterResult.WouldCallBroadcastToVisiblePlayersAsync);
		Assert.False(plan.SocketAdapterResult.DidCallBroadcastToVisiblePlayersAsync);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.FanoutPlan.IsLive);
		Assert.False(plan.SocketAdapterResult.IsLive);
		Assert.Empty(registry.Broadcasts);
		Assert.Empty(registry.SentPackets);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_GroupActionComposesDisabledRecipientBoundaryWithoutSending()
	{
		var registry = new CapturingConnectionRegistry();
		var groupRuntime = new PlayerGroupRuntime();
		var source = CreatePlayer(1001);
		var member = CreatePlayer(1002);
		groupRuntime.CreateOrUpdateGroup(9001, [source, member]);
		await using var fixture = await GroupDataExchangeFixture.CreateAsync(registry, groupRuntime);
		GameServerConnectionBuyItemTests.SetActivePlayerForPacketDispatchForAdapterTests(fixture.Connection, source);

		await GameServerConnectionBuyItemTests.InvokeProcessPacketAsyncForAdapterTests(
			fixture.Connection,
			CreateGroupDataExchangePayload(action: 2, groupType: 0, unknown2: 7, [10, 11]));

		var plan = Assert.Single(fixture.CompositionPlans);
		Assert.Equal(GroupDataExchangeFanoutPlanStatus.GroupBroadcastMembersExceptSelf, plan.FanoutPlan.Status);
		Assert.Equal([1002], plan.FanoutPlan.RecipientObjectIds);
		Assert.Equal(GroupDataExchangeFanoutSocketAdapterStatus.DisabledNoSend, plan.SocketAdapterResult.Status);
		Assert.True(plan.SocketAdapterResult.WouldCallSendPacketToPlayerAsync);
		Assert.False(plan.SocketAdapterResult.DidCallSendPacketToPlayerAsync);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.Equal(GroupDataExchangeFanoutSocketRecipientStatus.NotAttemptedDisabled, Assert.Single(plan.SocketAdapterResult.RecipientResults).Status);
		Assert.Empty(registry.Broadcasts);
		Assert.Empty(registry.SentPackets);
		Assert.Empty(fixture.SentPackets);
	}

	private static Player CreatePlayer(int objectId) =>
		new()
		{
			ObjectId = objectId,
			Name = $"Player{objectId}",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 1,
			IsOnline = true,
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
		};

	private static byte[] CreateGroupDataExchangePayload(byte action, byte groupType, byte unknown2, byte[] data)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = EncodeClientPacketOpcode(79);
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		buffer.WriteC(action);
		if (action != 1)
		{
			buffer.WriteC(groupType);
			buffer.WriteC(unknown2);
		}

		buffer.WriteD(data.Length);
		buffer.WriteB(data);
		return buffer.ToArray();
	}

	private static int EncodeClientPacketOpcode(int opcode)
	{
		return ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
	}

	private sealed class GroupDataExchangeFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private GroupDataExchangeFixture(
			TcpClient client,
			GameServerConnection connection,
			List<GroupDataExchangeHandlerCompositionPlan> compositionPlans,
			List<GameServerPacket> sentPackets)
		{
			_client = client;
			Connection = connection;
			CompositionPlans = compositionPlans;
			SentPackets = sentPackets;
		}

		public GameServerConnection Connection { get; }

		public List<GroupDataExchangeHandlerCompositionPlan> CompositionPlans { get; }

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<GroupDataExchangeFixture> CreateAsync(
			IGameClientConnectionRegistry registry,
			PlayerGroupRuntime? groupRuntime = null)
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
				var compositionPlans = new List<GroupDataExchangeHandlerCompositionPlan>();
				var sentPackets = new List<GameServerPacket>();
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"group-data-exchange-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					connectionRegistry: registry,
					sentPacketObserver: sentPackets.Add,
					playerGroupRuntime: groupRuntime,
					crypt: crypt,
					groupDataExchangeHandlerCompositionPlanObserver: compositionPlans.Add);
				return new GroupDataExchangeFixture(client, connection, compositionPlans, sentPackets);
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

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<(WorldPosition SourcePosition, int SourceObjectId, GameServerPacket Packet, bool IncludeSourcePlayer)> Broadcasts { get; } = [];

		public List<(int PlayerObjectId, GameServerPacket Packet)> SentPackets { get; } = [];

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
			SentPackets.Add((playerObjectId, packet));
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
			Broadcasts.Add((sourcePosition, sourceObjectId, packet, includeSourcePlayer));
			return Task.FromResult(0);
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
}
