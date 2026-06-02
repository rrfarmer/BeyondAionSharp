using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionFindGroupBoundaryTests
{
	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ComposesAdapterPlanWithoutLiveDispatch()
	{
		var sentPackets = new List<GameServerPacket>();
		var player = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.AddRecruitment(player, "Need healer", groupType: 2, nowEpochSeconds: 100);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet));
		SetActivePlayer(fixture.Connection, player);
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(0));

		var plan = fixture.Connection.CreateDisabledFindGroupBoundaryPlan(packet, nowEpochSeconds: 101);

		Assert.NotNull(plan);
		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.ComposedDisabledSideEffects, plan!.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.IsCmFindGroupBoundaryWired);
		Assert.Equal(0, plan.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.ShowRecruitments, plan.IntentPlan.ClientActionKind);
		var intent = Assert.Single(plan.IntentPlan.DirectPacketIntents);
		Assert.Equal(player.ObjectId, intent.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), intent.Packet.GetType().Name);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Empty(sentPackets);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_UnconfiguredConnectionPreservesDeferredBoundary()
	{
		await using var fixture = await ConnectionFixture.CreateAsync(findGroupService: null);
		SetActivePlayer(fixture.Connection, CreatePlayer(0x01020304, "Recruiter", "ELYOS"));
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(0));

		var plan = fixture.Connection.CreateDisabledFindGroupBoundaryPlan(packet, nowEpochSeconds: 101);

		Assert.Null(plan);
	}

	private static CmFindGroup CreateFindGroupPacket(Action<PacketBuffer> writePayload)
	{
		var packet = GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(77, writePayload),
			GameConnectionState.InGame);
		return Assert.IsType<CmFindGroup>(packet);
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writePayload)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		writePayload(buffer);
		return buffer.ToArray();
	}

	private static Player CreatePlayer(int objectId, string name, string race)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = race,
			PlayerClass = "CLERIC",
			Level = 65,
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
		};
	}

	private static void SetActivePlayer(GameServerConnection connection, Player player)
	{
		var activePlayerField = typeof(GameServerConnection).GetField("_activePlayer", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(activePlayerField);
		activePlayerField.SetValue(connection, player);

		var stateField = typeof(GameServerConnection).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(stateField);
		stateField.SetValue(connection, GameConnectionState.InGame);
	}

	private sealed class ConnectionFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private ConnectionFixture(TcpClient client, GameServerConnection connection)
		{
			_client = client;
			Connection = connection;
		}

		public GameServerConnection Connection { get; }

		public static async Task<ConnectionFixture> CreateAsync(
			FindGroupRecruitmentPlanService? findGroupService,
			Action<GameServerPacket>? sentPacketObserver = null)
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
				var compositionService = findGroupService == null
					? null
					: new FindGroupConnectionClientActionCompositionPlanService(
						new FindGroupClientActionPlanService(findGroupService));
				var dispatchAdapterService = findGroupService == null
					? null
					: new FindGroupConnectionBoundaryDispatchAdapterService();
				return new ConnectionFixture(
					client,
					new GameServerConnection(
						NullLogger.Instance,
						serverClient,
						"find-group-boundary-test",
						new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
						options: new GameServerOptions(),
						sentPacketObserver: sentPacketObserver,
						crypt: crypt,
						findGroupConnectionClientActionCompositionPlanService: compositionService,
						findGroupConnectionBoundaryDispatchAdapterService: dispatchAdapterService));
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
