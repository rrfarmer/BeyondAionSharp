using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class CmLegionSendEmblemInfoTests
{
	[Fact]
	public void ClientPacketFactory_ParsesLegionSendEmblemInfoAsInGameOnly()
	{
		Assert.IsType<CmLegionSendEmblemInfo>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(16, buffer => buffer.WriteD(77)), GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(16, buffer => buffer.WriteD(77)),
			GameConnectionState.Authed));
	}

	[Fact]
	public void SmLegionSendEmblem_WritesJavaEmblemInfoPayload()
	{
		var packet = new SmLegionSendEmblem(
			legionId: 77,
			emblemId: 6,
			emblemType: 0x80,
			emblemDataSize: 0,
			colorA: 255,
			colorR: 10,
			colorG: 20,
			colorB: 30,
			legionName: "Hydrated Legion");

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(77, reader.ReadD());
		Assert.Equal(6, reader.ReadC());
		Assert.Equal(0x80, reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(255, reader.ReadC());
		Assert.Equal(10, reader.ReadC());
		Assert.Equal(20, reader.ReadC());
		Assert.Equal(30, reader.ReadC());
		Assert.Equal("Hydrated Legion", reader.ReadS());
		Assert.Equal(0x01, reader.ReadC());
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LegionSendEmblemInfoSendsActivePlayerLegionEmblemLikeJava()
	{
		await using var pair = await TestConnectionPair.CreateAsync();
		var player = CreateLegionPlayer();
		SetActivePlayer(pair.Connection, player);

		var packet = CreatePacket(player.LegionId);
		await InvokeHandleInfrastructurePacketAsync(pair.Connection, packet);

		var response = Assert.IsType<SmLegionSendEmblem>(Assert.Single(pair.SentPackets));
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal(player.LegionId, reader.ReadD());
		Assert.Equal(player.LegionEmblemId, reader.ReadC());
		Assert.Equal(player.LegionEmblemType, reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(player.LegionEmblemColorA, reader.ReadC());
		Assert.Equal(player.LegionEmblemColorR, reader.ReadC());
		Assert.Equal(player.LegionEmblemColorG, reader.ReadC());
		Assert.Equal(player.LegionEmblemColorB, reader.ReadC());
		Assert.Equal(player.LegionName, reader.ReadS());
		Assert.Equal(0x01, reader.ReadC());
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_LegionSendEmblemInfoSkipsUnknownLegionUntilRegistryExists()
	{
		await using var pair = await TestConnectionPair.CreateAsync();
		SetActivePlayer(pair.Connection, CreateLegionPlayer());

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreatePacket(999));

		Assert.Empty(pair.SentPackets);
	}

	private static Player CreateLegionPlayer()
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "Tester",
			LegionId = 77,
			LegionName = "Hydrated Legion",
			LegionEmblemId = 6,
			LegionEmblemType = 0x80,
			LegionEmblemColorA = 255,
			LegionEmblemColorR = 10,
			LegionEmblemColorG = 20,
			LegionEmblemColorB = 30,
		};
	}

	private static CmLegionSendEmblemInfo CreatePacket(int legionId)
	{
		var packet = new CmLegionSendEmblemInfo(16, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteD(legionId);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writePayload)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = EncodeClientPacketOpcode(opcode);
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		writePayload(buffer);
		return buffer.ToArray();
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

		public static async Task<TestConnectionPair> CreateAsync()
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
					"legion-emblem-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
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
