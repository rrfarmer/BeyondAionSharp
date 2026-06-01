using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class FindGroupConnectionClientActionCompositionPlanServiceTests
{
	[Fact]
	public async Task CreateDisabledPlan_ExtractsConnectionActivePlayerForParsedPacket()
	{
		await using var fixture = await ConnectionFixture.CreateAsync();
		var player = CreatePlayer(0x01020304, "Recruiter", "ELYOS", "CLERIC", 65);
		SetActivePlayer(fixture.Connection, player);
		var service = CreateService();
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(2);
				buffer.WriteD(player.ObjectId);
				buffer.WriteS("Need healer");
				buffer.WriteC(3);
			});

		var plan = service.CreateDisabledPlan(fixture.Connection, packet, nowEpochSeconds: 200);

		Assert.Equal(FindGroupConnectionClientActionCompositionStatus.ComposedDisabledPlan, plan.Status);
		Assert.Same(player, plan.ActivePlayer);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.NotNull(plan.ClientActionPlan);
		Assert.Equal(FindGroupClientActionPlanKind.AddRecruitment, plan.ClientActionPlan!.Kind);
		Assert.False(plan.ClientActionPlan.DispatchLiveSideEffects);
		Assert.Equal("Need healer", plan.ClientActionPlan.RecruitmentMutationPlan!.CurrentRecruitment!.Message);
	}

	[Fact]
	public async Task CreateDisabledPlan_MissingActivePlayerRecordsDisabledSkip()
	{
		await using var fixture = await ConnectionFixture.CreateAsync();
		var service = CreateService();
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(0));

		var plan = service.CreateDisabledPlan(fixture.Connection, packet, nowEpochSeconds: 200);

		Assert.Equal(FindGroupConnectionClientActionCompositionStatus.SkippedMissingActivePlayer, plan.Status);
		Assert.Null(plan.ActivePlayer);
		Assert.Equal(0, plan.Action.Action);
		Assert.Null(plan.ClientActionPlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
	}

	private static FindGroupConnectionClientActionCompositionPlanService CreateService()
	{
		return new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(new FindGroupRecruitmentPlanService()));
	}

	private static CmFindGroup CreateFindGroupPacket(Action<PacketBuffer> writePayload)
	{
		var packet = GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(77, writePayload),
			GameConnectionState.InGame);
		return Assert.IsType<CmFindGroup>(packet);
	}

	private static Player CreatePlayer(int objectId, string name, string race, string playerClass, int level)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = race,
			PlayerClass = playerClass,
			Level = level,
		};
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

		public static async Task<ConnectionFixture> CreateAsync()
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
				return new ConnectionFixture(
					client,
					new GameServerConnection(
						NullLogger.Instance,
						serverClient,
						"find-group-composition-test",
						new GamePacketProcessor<string>((_, _) => Task.CompletedTask)));
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
