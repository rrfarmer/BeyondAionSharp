using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionAutoGroupTests
{
	[Fact]
	public async Task ProcessPacketAsync_AutoGroupDisabledSendsJavaTextMessageAndStopsWindowDispatch()
	{
		var sentPackets = new List<GameServerPacket>();
		await using var fixture = await ConnectionFixture.CreateAsync(
			new GameServerOptions { AutoGroup = new GameServerAutoGroupOptions { Enabled = false } },
			sentPackets.Add);
		SetConnectionState(fixture.Connection, GameConnectionState.InGame);
		SetActivePlayer(
			fixture.Connection,
			new Player
			{
				ObjectId = 1001,
				Name = "DisabledTester",
				Race = "ELYOS",
				Level = 50,
			});

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(
				200,
				buffer =>
				{
					buffer.WriteD(107);
					buffer.WriteC(104);
					buffer.WriteC(0);
				}));

		var message = Assert.IsType<SmMessage>(Assert.Single(sentPackets));
		Assert.Equal("Auto Group is disabled", ReadMessage(message));
	}

	[Fact]
	public async Task ProcessPacketAsync_AutoGroupWindow105IsJavaNoOp()
	{
		var sentPackets = new List<GameServerPacket>();
		await using var fixture = await ConnectionFixture.CreateAsync(new GameServerOptions(), sentPackets.Add);
		SetConnectionState(fixture.Connection, GameConnectionState.InGame);
		SetActivePlayer(
			fixture.Connection,
			new Player
			{
				ObjectId = 1002,
				Name = "NoOpTester",
				Race = "ELYOS",
				Level = 50,
			});

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(
				200,
				buffer =>
				{
					buffer.WriteD(107);
					buffer.WriteC(105);
					buffer.WriteC(0);
				}));

		Assert.Empty(sentPackets);
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

	private static async Task InvokeProcessPacketAsync(GameServerConnection connection, byte[] payload)
	{
		var method = typeof(GameServerConnection).GetMethod("ProcessPacketAsync", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		using var packet = new PacketBuffer(payload);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [packet]));
		await task;
	}

	private static void SetActivePlayer(GameServerConnection connection, Player player)
	{
		var field = typeof(GameServerConnection).GetField("_activePlayer", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(field);
		field.SetValue(connection, player);
	}

	private static void SetConnectionState(GameServerConnection connection, GameConnectionState state)
	{
		var field = typeof(GameServerConnection).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(field);
		field.SetValue(connection, state);
	}

	private static string ReadMessage(SmMessage packet)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(25, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
		return reader.ReadS();
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
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
			GameServerOptions options,
			Action<GameServerPacket> sentPacketObserver)
		{
			using var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			var client = new TcpClient();
			var acceptTask = listener.AcceptTcpClientAsync();
			await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
			var serverClient = await acceptTask;
			var processor = new GamePacketProcessor<string>((_, _) => Task.CompletedTask);
			var crypt = new GameCrypt(() => 0x01020304);
			crypt.EnableKey();

			try
			{
				var connection = new GameServerConnection(
					NullLogger<GameServerConnectionAutoGroupTests>.Instance,
					serverClient,
					"autogroup-test",
					processor,
					options,
					sentPacketObserver: sentPacketObserver,
					crypt: crypt);
				return new ConnectionFixture(client, connection);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			_client.Dispose();
			await Connection.DisposeAsync();
		}
	}
}
