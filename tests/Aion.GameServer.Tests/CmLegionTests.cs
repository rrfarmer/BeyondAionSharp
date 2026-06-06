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

public sealed class CmLegionTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaLegionOpcodeAsInGameOnly()
	{
		Assert.IsType<CmLegion>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(45, buffer => buffer.WriteC(0x0D)),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(45, buffer => buffer.WriteC(0x0D)),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_EditPermissionsReadsSignedShorts()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x0D);
		buffer.WriteH(0xffff);
		buffer.WriteH(0x8000);
		buffer.WriteH(0x7fff);
		buffer.WriteH(1);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(0x0D, packet.ExOpcode);
		Assert.Equal((short)-1, packet.DeputyPermission);
		Assert.Equal(short.MinValue, packet.CenturionPermission);
		Assert.Equal(short.MaxValue, packet.LegionaryPermission);
		Assert.Equal((short)1, packet.VolunteerPermission);
	}

	[Fact]
	public void ReadFrom_RankBranchConsumesRankAndCharacterName()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x06);
		buffer.WriteD(3);
		buffer.WriteS("Lurion");

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(0x06, packet.ExOpcode);
		Assert.Equal(3, packet.Rank);
		Assert.Equal("Lurion", packet.CharacterName);
	}

	[Fact]
	public void ReadFrom_RefreshInfoConsumesJavaEmptyFields()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x08);
		buffer.WriteD(0);
		buffer.WriteH(0);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(0x08, packet.ExOpcode);
	}

	[Fact]
	public void ReadFrom_ShowNoticeConsumesJavaEmptyFields()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x07);
		buffer.WriteD(0);
		buffer.WriteH(0);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(0x07, packet.ExOpcode);
	}

	[Fact]
	public void SmSystemMessage_LegionNoticeHelpersUseJavaIdsAndParameters()
	{
		var noNotice = SmSystemMessage.MsgNoSetGuildNotice();
		Assert.Equal(1390127, noNotice.MessageId);
		Assert.Empty(noNotice.Parameters);

		var notice = SmSystemMessage.GuildNotice("Assemble", 1_771_234_500);
		Assert.Equal(1400019, notice.MessageId);
		Assert.Equal(["Assemble", "1771234500", "2"], notice.Parameters);
	}

	[Fact]
	public void SmLegionInfo_WritesJavaPayloadWithCurrentRuntimeFields()
	{
		var packet = new SmLegionInfo(
			"Hydrated Legion",
			legionLevel: 4,
			rankingPosition: 123,
			deputyPermission: 1,
			centurionPermission: 2,
			legionaryPermission: 3,
			volunteerPermission: 4,
			contributionPoints: 55_000,
			disbandTime: 1_771_234_567,
			occupiedLegionDominion: 5,
			lastLegionDominion: 6,
			currentLegionDominion: 7,
			announcement: "Assemble",
			announcementTime: 1_771_234_500);

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal("Hydrated Legion", reader.ReadS());
		Assert.Equal(4, reader.ReadC());
		Assert.Equal(123, reader.ReadD());
		Assert.Equal(1, reader.ReadSignedH());
		Assert.Equal(2, reader.ReadSignedH());
		Assert.Equal(3, reader.ReadSignedH());
		Assert.Equal(4, reader.ReadSignedH());
		Assert.Equal(55_000, reader.ReadQ());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(1_771_234_567, reader.ReadD());
		Assert.Equal(5, reader.ReadD());
		Assert.Equal(6, reader.ReadD());
		Assert.Equal(7, reader.ReadD());
		Assert.Equal("Assemble", reader.ReadS());
		Assert.Equal(1_771_234_500, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
	}

	[Fact]
	public void SmLegionInfo_FromPlayerWritesLoadedAnnouncementLikeJava()
	{
		var player = CreateLegionPlayer();
		player.LegionAnnouncement = "Assemble";
		player.LegionAnnouncementEpochSeconds = 1_771_234_500;

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(SmLegionInfo.FromPlayer(player)));

		Assert.Equal("Hydrated Legion", reader.ReadS());
		Assert.Equal(4, reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(11, reader.ReadSignedH());
		Assert.Equal(12, reader.ReadSignedH());
		Assert.Equal(13, reader.ReadSignedH());
		Assert.Equal(14, reader.ReadSignedH());
		Assert.Equal(0, reader.ReadQ());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(1_771_234_567, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal("Assemble", reader.ReadS());
		Assert.Equal(1_771_234_500, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_RefreshInfoSendsActivePlayerLegionInfoLikeJava()
	{
		await using var pair = await TestConnectionPair.CreateAsync();
		SetActivePlayer(pair.Connection, CreateLegionPlayer());

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateRefreshInfoPacket());

		var response = Assert.IsType<SmLegionInfo>(Assert.Single(pair.SentPackets));
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(response));
		Assert.Equal("Hydrated Legion", reader.ReadS());
		Assert.Equal(4, reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(11, reader.ReadSignedH());
		Assert.Equal(12, reader.ReadSignedH());
		Assert.Equal(13, reader.ReadSignedH());
		Assert.Equal(14, reader.ReadSignedH());
		Assert.Equal(0, reader.ReadQ());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(1_771_234_567, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ShowNoticeSendsNoNoticeMessageWhenAnnouncementMissingLikeJava()
	{
		await using var pair = await TestConnectionPair.CreateAsync();
		SetActivePlayer(pair.Connection, CreateLegionPlayer());

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateShowNoticePacket());

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1390127, response.MessageId);
		Assert.Empty(response.Parameters);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_ShowNoticeSendsLoadedAnnouncementLikeJava()
	{
		await using var pair = await TestConnectionPair.CreateAsync();
		var player = CreateLegionPlayer();
		player.LegionAnnouncement = "Assemble";
		player.LegionAnnouncementEpochSeconds = 1_771_234_500;
		SetActivePlayer(pair.Connection, player);

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateShowNoticePacket());

		var response = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1400019, response.MessageId);
		Assert.Equal(["Assemble", "1771234500", "2"], response.Parameters);
	}

	[Fact]
	public async Task HandleInfrastructurePacketAsync_RefreshInfoSkipsPlayerWithoutLegionLikeJava()
	{
		await using var pair = await TestConnectionPair.CreateAsync();
		SetActivePlayer(pair.Connection, new Player { ObjectId = 1001, Name = "Unguilded" });

		await InvokeHandleInfrastructurePacketAsync(pair.Connection, CreateRefreshInfoPacket());

		Assert.Empty(pair.SentPackets);
	}

	private static CmLegion CreatePacket()
	{
		return new CmLegion(45, new HashSet<GameConnectionState> { GameConnectionState.InGame });
	}

	private static CmLegion CreateShowNoticePacket()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x07);
		buffer.WriteD(0);
		buffer.WriteH(0);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static CmLegion CreateRefreshInfoPacket()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(0x08);
		buffer.WriteD(0);
		buffer.WriteH(0);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static Player CreateLegionPlayer()
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "Tester",
			LegionId = 77,
			LegionName = "Hydrated Legion",
			LegionLevel = 4,
			LegionDisbandTime = 1_771_234_567,
			LegionDeputyPermission = 11,
			LegionCenturionPermission = 12,
			LegionLegionaryPermission = 13,
			LegionVolunteerPermission = 14,
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
					"legion-info-test",
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
