using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameClientSocketServerMailFanoutTests
{
	[Fact]
	public async Task NotifyMailReceivedAsync_SendsMailboxStateListThenPostmanNotifyForOpenExpressMailbox()
	{
		await using var fixture = await MailFanoutFixture.CreateAsync();
		var player = CreatePlayer(Player.MailboxExpressState);
		SetActivePlayer(fixture.Connection, player);
		fixture.Server.RegisterPlayerConnection(player.ObjectId, fixture.Connection);
		var mail = CreateMail(id: 12, letterType: 1, receivedTime: new DateTime(2026, 5, 26, 12, 0, 0));

		var notified = await fixture.Server.NotifyMailReceivedAsync(player.ObjectId, mail);

		Assert.True(notified);
		Assert.Equal([10, 11, 12], player.Mailbox.Select(letter => letter.Id).ToArray());
		Assert.Equal(
			[typeof(SmMailService), typeof(SmMailService), typeof(SmSystemMessage)],
			fixture.SentPackets.Select(packet => packet.GetType()).ToArray());
		Assert.Equal(0, ReadMailServiceId((SmMailService)fixture.SentPackets[0]));
		Assert.Equal(2, ReadMailServiceId((SmMailService)fixture.SentPackets[1]));
		Assert.Equal([12, 11], ReadMailListIds((SmMailService)fixture.SentPackets[1]));
		Assert.Equal(1300899, ReadSystemMessageId((SmSystemMessage)fixture.SentPackets[2]));
	}

	[Fact]
	public async Task NotifyMailReceivedAsync_SendsOnlyMailboxStateForClosedNormalMailbox()
	{
		await using var fixture = await MailFanoutFixture.CreateAsync();
		var player = CreatePlayer(Player.MailboxClosedState);
		SetActivePlayer(fixture.Connection, player);
		fixture.Server.RegisterPlayerConnection(player.ObjectId, fixture.Connection);
		var mail = CreateMail(id: 13, letterType: 0, receivedTime: new DateTime(2026, 5, 26, 12, 5, 0));

		var notified = await fixture.Server.NotifyMailReceivedAsync(player.ObjectId, mail);

		Assert.True(notified);
		Assert.Equal([10, 11, 13], player.Mailbox.Select(letter => letter.Id).ToArray());
		var statePacket = Assert.IsType<SmMailService>(Assert.Single(fixture.SentPackets));
		Assert.Equal(0, ReadMailServiceId(statePacket));
	}

	[Fact]
	public async Task NotifyMailReceivedAsync_ReturnsFalseWithoutSendingWhenRecipientOffline()
	{
		await using var fixture = await MailFanoutFixture.CreateAsync();
		var mail = CreateMail(id: 14, letterType: 1, receivedTime: new DateTime(2026, 5, 26, 12, 10, 0));

		var notified = await fixture.Server.NotifyMailReceivedAsync(recipientObjectId: 1001, mail);

		Assert.False(notified);
		Assert.Empty(fixture.SentPackets);
	}

	private static Player CreatePlayer(byte mailboxState)
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "Mailreward",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Gender = "MALE",
			MailboxState = mailboxState,
			Mailbox =
			[
				CreateMail(id: 10, letterType: 0, receivedTime: new DateTime(2026, 5, 26, 11, 0, 0)),
				CreateMail(id: 11, letterType: 1, receivedTime: new DateTime(2026, 5, 26, 11, 5, 0)),
			],
		};
	}

	private static PlayerMail CreateMail(int id, int letterType, DateTime receivedTime)
	{
		return new PlayerMail(
			id,
			RecipientId: 1001,
			SenderName: "Beyond Aion",
			Title: "Reward",
			Message: "Body",
			IsUnread: true,
			AttachedItemObjectId: 0,
			AttachedItemTemplateId: 0,
			AttachedKinah: 0,
			letterType,
			receivedTime);
	}

	private static void SetActivePlayer(GameServerConnection connection, Player player)
	{
		var activePlayerField = typeof(GameServerConnection).GetField("_activePlayer", BindingFlags.Instance | BindingFlags.NonPublic);
		var stateField = typeof(GameServerConnection).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(activePlayerField);
		Assert.NotNull(stateField);
		activePlayerField.SetValue(connection, player);
		stateField.SetValue(connection, GameConnectionState.InGame);
	}

	private static int ReadMailServiceId(SmMailService packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		return reader.ReadC();
	}

	private static int[] ReadMailListIds(SmMailService packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(2, (int)reader.ReadC());
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal(0, (int)reader.ReadC());
		_ = reader.ReadH();
		var ids = new List<int>();
		while (reader.Remaining > 0)
		{
			ids.Add(reader.ReadD());
			_ = reader.ReadS();
			_ = reader.ReadS();
			_ = reader.ReadC();
			_ = reader.ReadD();
			_ = reader.ReadD();
			_ = reader.ReadQ();
			_ = reader.ReadC();
		}

		return ids.ToArray();
	}

	private static int ReadSystemMessageId(SmSystemMessage packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(25, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		return reader.ReadD();
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private sealed class MailFanoutFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private MailFanoutFixture(TcpClient client, GameClientSocketServer server, GameServerConnection connection, List<GameServerPacket> sentPackets)
		{
			_client = client;
			Server = server;
			Connection = connection;
			SentPackets = sentPackets;
		}

		public GameClientSocketServer Server { get; }

		public GameServerConnection Connection { get; }

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<MailFanoutFixture> CreateAsync()
		{
			var sentPackets = new List<GameServerPacket>();
			var options = new GameServerOptions
			{
				Network = new GameServerNetworkOptions
				{
					ClientEndPoint = new IPEndPoint(IPAddress.Loopback, 0),
					MaxOnlinePlayers = 10,
				},
			};
			var packetProcessor = new GamePacketProcessor<string>((_, _) => Task.CompletedTask);
			var server = new GameClientSocketServer(
				NullLogger<GameClientSocketServer>.Instance,
				options,
				packetProcessor);
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
					"mail-fanout-test",
					packetProcessor,
					options: options,
					connectionRegistry: server,
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new MailFanoutFixture(client, server, connection, sentPackets);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			Server.UnregisterPlayerConnection(1001, Connection);
			await Connection.DisposeAsync();
			_client.Dispose();
		}
	}
}
