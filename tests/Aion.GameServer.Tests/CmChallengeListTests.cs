using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class CmChallengeListTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaChallengeListOpcodeAsInGameOnly()
	{
		Assert.IsType<CmChallengeList>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(232, buffer =>
				{
					buffer.WriteC(0);
					buffer.WriteD(77);
					buffer.WriteC(1);
					buffer.WriteD(1001);
					buffer.WriteD(0);
				}),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(232, buffer =>
			{
				buffer.WriteC(0);
				buffer.WriteD(77);
				buffer.WriteC(1);
				buffer.WriteD(1001);
				buffer.WriteD(0);
			}),
			GameConnectionState.Authed));
	}

	[Fact]
	public void SmChallengeList_TaskListSerializesJavaActionTwoShape()
	{
		var packet = SmChallengeList.TaskList(
			ownerId: 77,
			ownerTypeId: SmChallengeList.LegionOwnerTypeId,
			playerObjectId: 1001,
			currentEpochSeconds: 1_771_000_000,
			[
				new ChallengeTaskState(
					300,
					1_771_000_100,
					false,
					[new ChallengeQuestState(17000, 6, 5, 2)]),
			]);

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(2, reader.ReadC());
		Assert.Equal(77, reader.ReadD());
		Assert.Equal(1, reader.ReadC());
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal(1_771_000_000, reader.ReadD());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(32, reader.ReadD());
		Assert.Equal(300, reader.ReadD());
		Assert.Equal(1, reader.ReadC());
		Assert.Equal(21, reader.ReadC());
		Assert.Equal(0, reader.ReadC());
		Assert.Equal(1_771_000_100, reader.ReadD());
	}

	[Fact]
	public void SmChallengeList_TaskInfoSerializesJavaActionSevenShape()
	{
		var packet = SmChallengeList.TaskInfo(
			ownerId: 77,
			ownerTypeId: SmChallengeList.LegionOwnerTypeId,
			playerObjectId: 1001,
			new ChallengeTaskState(
				300,
				0,
				false,
				[
					new ChallengeQuestState(17000, 6, 5, 2),
					new ChallengeQuestState(17001, 12, 6, 4),
				]));

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(7, reader.ReadC());
		Assert.Equal(77, reader.ReadD());
		Assert.Equal(1, reader.ReadC());
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal(32, reader.ReadD());
		Assert.Equal(300, reader.ReadD());
		Assert.Equal(2, reader.ReadH());
		Assert.Equal(17000, reader.ReadD());
		Assert.Equal(6, reader.ReadH());
		Assert.Equal(5, reader.ReadD());
		Assert.Equal(2, reader.ReadH());
		Assert.Equal(17001, reader.ReadD());
		Assert.Equal(12, reader.ReadH());
		Assert.Equal(6, reader.ReadD());
		Assert.Equal(4, reader.ReadH());
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LegionChallengeListCreatesStoresAndSendsLikeJava()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository, CreateOptions(), CreateChallengeTaskTable());
		var player = CreatePlayer();
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChallengeListPacket(ownerId: 77, ownerType: 1));

		Assert.Equal(1, repository.LoadLegionChallengeTasksCalls);
		Assert.Equal(77, repository.LoadedLegionChallengeTasksLegionId);
		var saved = Assert.Single(repository.SavedNewLegionChallengeTasks);
		Assert.Equal(77, saved.LegionId);
		Assert.Equal(300, saved.Task.TaskId);
		Assert.Collection(
			pair.SentPackets,
			packet => AssertTaskListPacket(packet, player.ObjectId, taskId: 300),
			packet => AssertTaskInfoPacket(packet, player.ObjectId, taskId: 300));
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ChallengeListTownRequestStaysDeferredWithoutPackets()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(repository, CreateOptions(), CreateChallengeTaskTable());
		SetActivePlayer(pair.Connection, CreatePlayer());

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateChallengeListPacket(ownerId: 50, ownerType: 2));

		Assert.Empty(pair.SentPackets);
		Assert.Equal(0, repository.LoadLegionChallengeTasksCalls);
	}

	private static void AssertTaskListPacket(GameServerPacket packet, int playerObjectId, int taskId)
	{
		var response = Assert.IsType<SmChallengeList>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal(2, reader.ReadC());
		Assert.Equal(77, reader.ReadD());
		Assert.Equal(1, reader.ReadC());
		Assert.Equal(playerObjectId, reader.ReadD());
		Assert.True(reader.ReadD() > 0);
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(32, reader.ReadD());
		Assert.Equal(taskId, reader.ReadD());
		Assert.Equal(1, reader.ReadC());
		Assert.Equal(21, reader.ReadC());
		Assert.Equal(0, reader.ReadC());
		Assert.Equal(0, reader.ReadD());
	}

	private static void AssertTaskInfoPacket(GameServerPacket packet, int playerObjectId, int taskId)
	{
		var response = Assert.IsType<SmChallengeList>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal(7, reader.ReadC());
		Assert.Equal(77, reader.ReadD());
		Assert.Equal(1, reader.ReadC());
		Assert.Equal(playerObjectId, reader.ReadD());
		Assert.Equal(32, reader.ReadD());
		Assert.Equal(taskId, reader.ReadD());
		Assert.Equal(3, reader.ReadH());
		Assert.Equal(17000, reader.ReadD());
		Assert.Equal(6, reader.ReadH());
		Assert.Equal(5, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(17001, reader.ReadD());
		Assert.Equal(12, reader.ReadH());
		Assert.Equal(6, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(17002, reader.ReadD());
		Assert.Equal(42, reader.ReadH());
		Assert.Equal(7, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
	}

	private static GameClientPacket CreateChallengeListPacket(int ownerId, int ownerType)
	{
		var packet = Assert.IsType<CmChallengeList>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(232, buffer =>
				{
					buffer.WriteC(0);
					buffer.WriteD(ownerId);
					buffer.WriteC(ownerType);
					buffer.WriteD(1001);
					buffer.WriteD(0);
				}),
				GameConnectionState.InGame));
		return packet;
	}

	private static GameServerOptions CreateOptions()
	{
		return new GameServerOptions
		{
			Custom = new GameServerCustomOptions { ChallengeTasksEnabled = true },
		};
	}

	private static ChallengeTaskTable CreateChallengeTaskTable()
	{
		return new ChallengeTaskTable(
			[
				new ChallengeTaskSummary(
					300,
					"LEGION",
					"ELYOS",
					5,
					5,
					true,
					false,
					null,
					[
						new ChallengeQuestSummary(17000, 6, 5),
						new ChallengeQuestSummary(17001, 12, 6),
						new ChallengeQuestSummary(17002, 42, 7),
					]),
				new ChallengeTaskSummary(
					400,
					"LEGION",
					"ASMODIANS",
					5,
					5,
					true,
					false,
					null,
					[
						new ChallengeQuestSummary(27000, 6, 5),
					]),
			]);
	}

	private static Player CreatePlayer()
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "Tester",
			Race = "ELYOS",
			LegionId = 77,
			LegionName = "Hydrated Legion",
			LegionLevel = 5,
		};
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writeBody)
	{
		using var body = new PacketBuffer();
		writeBody(body);
		var bodyBytes = body.ToArray();

		var encodedOpcode = EncodeClientPacketOpcode(opcode);
		using var payload = new PacketBuffer(5 + bodyBytes.Length);
		payload.WriteH(encodedOpcode);
		payload.WriteC(0x65);
		payload.WriteH((ushort)~encodedOpcode);
		payload.WriteB(bodyBytes);
		return payload.ToArray();
	}

	private static int EncodeClientPacketOpcode(int opcode)
	{
		return ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static async Task InvokeHandleInfrastructurePacketAsync(GameServerConnection connection, GameClientPacket packet)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"HandleInfrastructurePacketAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = (Task)method.Invoke(connection, [packet])!;
		await task;
	}

	private static void SetActivePlayer(GameServerConnection connection, Player player)
	{
		var activePlayerField = typeof(GameServerConnection).GetField("_activePlayer", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(activePlayerField);
		activePlayerField.SetValue(connection, player);
	}

	private sealed class TestConnectionPair : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private TestConnectionPair(TcpClient client, GameServerConnection connection, List<GameServerPacket> sentPackets)
		{
			_client = client;
			Connection = connection;
			SentPackets = sentPackets;
		}

		public GameServerConnection Connection { get; }
		public List<GameServerPacket> SentPackets { get; }

		public static async Task<TestConnectionPair> CreateAsync(
			IPlayerEnterWorldRepository playerEnterWorldRepository,
			GameServerOptions options,
			ChallengeTaskTable challengeTaskTable)
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
				var sentPackets = new List<GameServerPacket>();
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"challenge-list-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: options,
					playerEnterWorldRepository: playerEnterWorldRepository,
					sentPacketObserver: sentPackets.Add,
					crypt: crypt,
					challengeTaskTable: challengeTaskTable);
				return new TestConnectionPair(client, connection, sentPackets);
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
