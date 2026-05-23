using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionInstanceCooldownTests
{
	[Fact]
	public async Task ApplyInstanceEntranceCooldownAsync_SendsJavaEntryInfoPacketToOwner()
	{
		await using var pair = await TestConnectionPair.CreateAsync(
			new GameServerOptions
			{
				Membership = new GameServerMembershipOptions { InstancesCooldown = 10 },
				Instance = new GameServerInstanceOptions { CooldownRate = 2 },
			});
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Character",
			Race = "ELYOS",
			AccountMembership = 10,
		};
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, 300030000, "PC_ALL", MaxCount: 5, CoolTimeType: "RELATIVE", EntCoolTime: 30),
		]);

		var result = await pair.Connection.ApplyInstanceEntranceCooldownAsync(
			player,
			300030000,
			reenter: false,
			cooltimes,
			now);

		Assert.True(result.Added);
		var packet = Assert.IsType<SmInstanceInfo>(Assert.Single(pair.SentPackets));
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(2, (int)reader.ReadC());
		Assert.Equal(8, reader.ReadD());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(8, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(900, reader.ReadD());
		Assert.Equal(5, reader.ReadD());
		Assert.Equal(-1, reader.ReadD());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal("Character", reader.ReadS());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public async Task ApplyInstanceEntranceCooldownAsync_SkipsSendWhenJavaAddGuardIsFalse()
	{
		await using var pair = await TestConnectionPair.CreateAsync(new GameServerOptions());
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Character",
			Race = "ELYOS",
		};
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, 300030000, "PC_ALL", MaxCount: 5, CoolTimeType: "RELATIVE", EntCoolTime: 30),
		]);

		var result = await pair.Connection.ApplyInstanceEntranceCooldownAsync(
			player,
			300030000,
			reenter: true,
			cooltimes,
			DateTimeOffset.FromUnixTimeMilliseconds(100_000));

		Assert.False(result.Added);
		Assert.Empty(pair.SentPackets);
		Assert.Empty(player.PortalCooldowns);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private sealed class TestConnectionPair : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private TestConnectionPair(
			TcpClient client,
			GameServerConnection connection,
			List<GameServerPacket> sentPackets)
		{
			_client = client;
			Connection = connection;
			SentPackets = sentPackets;
		}

		public GameServerConnection Connection { get; }

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<TestConnectionPair> CreateAsync(GameServerOptions options)
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
					"instance-cooldown-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: options,
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
