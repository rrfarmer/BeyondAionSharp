using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class CmVersionCheckTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaVersionCheckOpcodeAsConnectedOnly()
	{
		Assert.IsType<CmVersionCheck>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(0, buffer => WriteVersionCheckPayload(buffer)),
				GameConnectionState.Connected));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(0, buffer => WriteVersionCheckPayload(buffer)),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_ReadsUnsignedVersionsAndLiteInfoLikeJava()
	{
		var packet = CreatePacket();
		using var writeBuffer = new PacketBuffer();
		WriteVersionCheckPayload(writeBuffer);

		var readBuffer = new PacketBuffer(writeBuffer.ToArray());
		packet.ReadFrom(readBuffer);

		Assert.Equal(0xffff, packet.AionClientVersion);
		Assert.Equal(0x8001, packet.NpcScriptInterfaceVersion);
		Assert.Equal(65001, packet.WindowsEncoding);
		Assert.Equal(10, packet.WindowsVersion);
		Assert.Equal(19045, packet.WindowsSubVersion);
		Assert.Equal(2, packet.LiteInfo);
		Assert.Equal(0, readBuffer.Remaining);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_CompatibleVersionSendsSmVersionCheck()
	{
		await using var pair = await TestConnectionPair.CreateAsync();
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		WriteVersionCheckPayload(buffer, SmVersionCheck.InternalVersion);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, packet);

		var response = Assert.Single(pair.SentPackets);
		var versionCheck = Assert.IsType<SmVersionCheck>(response);
		Assert.Equal(SmVersionCheck.InternalVersion, versionCheck.Version);
		Assert.Equal(EventTheme.None, versionCheck.CityDecoration);
	}

	private static CmVersionCheck CreatePacket()
	{
		return new CmVersionCheck(0, new HashSet<GameConnectionState> { GameConnectionState.Connected });
	}

	private static void WriteVersionCheckPayload(PacketBuffer buffer, int aionClientVersion = 0xffff)
	{
		buffer.WriteH(aionClientVersion);
		buffer.WriteH(0x8001);
		buffer.WriteD(65001);
		buffer.WriteD(10);
		buffer.WriteD(19045);
		buffer.WriteC(2);
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

	private static async Task InvokeHandleInfrastructurePacketAsync(GameServerConnection connection, GameClientPacket packet)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"HandleInfrastructurePacketAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = (Task)method.Invoke(connection, [packet])!;
		await task;
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
					"version-check-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions { Core = new GameServerCoreOptions { TimeZoneId = "UTC" } },
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
