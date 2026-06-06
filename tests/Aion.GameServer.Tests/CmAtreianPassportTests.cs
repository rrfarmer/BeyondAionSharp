using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class CmAtreianPassportTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaAtreianPassportOpcodeAsInGameOnly()
	{
		Assert.IsType<CmAtreianPassport>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(248, buffer => buffer.WriteH(0)),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(248, buffer => buffer.WriteH(0)),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_SentinelCountConsumesCompletePassportPairsUntilTrailingBytes()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH(0xffff);
		buffer.WriteD(1001);
		buffer.WriteD(1717200000);
		buffer.WriteD(1001);
		buffer.WriteD(1717286400);
		buffer.WriteD(9999);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(-1, packet.Count);
		var timestamps = Assert.Single(packet.Passports);
		Assert.Equal(1001, timestamps.Key);
		Assert.True(timestamps.Value.SetEquals([1717200000, 1717286400]));
	}

	[Fact]
	public void ReadFrom_PositiveCountConsumesOnlyDeclaredPassportPairs()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH(1);
		buffer.WriteD(1001);
		buffer.WriteD(1717200000);
		buffer.WriteD(2002);
		buffer.WriteD(1717286400);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(1, packet.Count);
		var timestamps = Assert.Single(packet.Passports);
		Assert.Equal(1001, timestamps.Key);
		Assert.True(timestamps.Value.SetEquals([1717200000]));
	}

	[Fact]
	public void SmAtreianPassport_WritePayload_WritesJavaSnapshotFields()
	{
		var passport = new PlayerPassport(
			PassportId: 1001,
			Rewarded: false,
			ArriveDate: DateTimeOffset.FromUnixTimeSeconds(1_717_200_000).UtcDateTime);
		var payload = SerializeUnencryptedPayload(new SmAtreianPassport(
			[passport],
			stamps: 7,
			creationDate: new DateTime(2020, 5, 6, 7, 8, 9, DateTimeKind.Utc)));

		Assert.Equal(2020, ReadShort(payload, 0));
		Assert.Equal(5, ReadShort(payload, 2));
		Assert.Equal(6, ReadShort(payload, 4));
		Assert.Equal(1, ReadShort(payload, 6));
		Assert.Equal(1001, ReadInt(payload, 8));
		Assert.Equal(7, ReadInt(payload, 12));
		Assert.Equal(1, ReadInt(payload, 16)); // Passport.RewardStatus.AVAILABLE.
		Assert.Equal(1_717_200_000, ReadInt(payload, 20));
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_AtreianPassportSendsLiveSnapshotForActivePlayer()
	{
		await using var pair = await TestConnectionPair.CreateAsync();
		var player = new Player
		{
			ObjectId = 5001,
			AccountId = 77,
			Name = "PassportTester",
			CreationDate = new DateTime(2021, 3, 4, 12, 30, 0, DateTimeKind.Utc),
			PassportStamps = 3,
			Passports =
			[
				new PlayerPassport(
					PassportId: 2002,
					Rewarded: true,
					ArriveDate: DateTimeOffset.FromUnixTimeSeconds(1_717_286_400).UtcDateTime)
			],
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};
		SetActivePlayer(pair.Connection, player);
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH(0);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, packet);

		var response = Assert.Single(pair.SentPackets);
		var passport = Assert.IsType<SmAtreianPassport>(response);
		var payload = SerializeUnencryptedPayload(passport);
		Assert.Equal(2021, ReadShort(payload, 0));
		Assert.Equal(3, ReadShort(payload, 2));
		Assert.Equal(4, ReadShort(payload, 4));
		Assert.Equal(1, ReadShort(payload, 6));
		Assert.Equal(2002, ReadInt(payload, 8));
		Assert.Equal(3, ReadInt(payload, 12));
		Assert.Equal(2, ReadInt(payload, 16)); // Passport.RewardStatus.TAKEN.
		Assert.Equal(1_717_286_400, ReadInt(payload, 20));
	}

	private static CmAtreianPassport CreatePacket()
	{
		return new CmAtreianPassport(248, new HashSet<GameConnectionState> { GameConnectionState.InGame });
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

	private static int ReadInt(byte[] payload, int offset)
	{
		return BitConverter.ToInt32(payload, offset);
	}

	private static int ReadShort(byte[] payload, int offset)
	{
		return BitConverter.ToUInt16(payload, offset);
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
					"atreian-passport-test",
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
