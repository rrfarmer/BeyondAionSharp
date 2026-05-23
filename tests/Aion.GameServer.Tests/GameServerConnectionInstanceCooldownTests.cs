using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using GameWorld = Aion.GameServer.World.World;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionInstanceCooldownTests
{
	[Fact]
	public async Task ApplyInstanceEntranceCooldownAsync_SendsJavaEntryInfoPacketToOwner()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(
			new GameServerOptions
			{
				Membership = new GameServerMembershipOptions { InstancesCooldown = 10 },
				Instance = new GameServerInstanceOptions { CooldownRate = 2 },
			},
			new PlayerEnterWorldService(
				new GameServerOptions(),
				repository,
				new GameWorld(NullLogger<GameWorld>.Instance),
				NullLogger<PlayerEnterWorldService>.Instance));
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
		var savedCooldowns = repository.SavedPortalCooldowns;
		Assert.NotNull(savedCooldowns);
		var savedCooldown = Assert.Single(savedCooldowns);
		Assert.Equal(300030000, savedCooldown.Key);
		Assert.Equal(result.ReuseTimeMillis, savedCooldown.Value.ReuseTimeMillis);
		Assert.Equal(1, savedCooldown.Value.EntryCount);
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

	[Fact]
	public async Task QueueInstancePortalTransferAsync_SendsTeleportBeforeCooldownLikeJavaPortalTransfer()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(
			new GameServerOptions
			{
				Membership = new GameServerMembershipOptions { InstancesCooldown = 10 },
				Instance = new GameServerInstanceOptions { CooldownRate = 1 },
			},
			new PlayerEnterWorldService(
				new GameServerOptions(),
				repository,
				new GameWorld(NullLogger<GameWorld>.Instance),
				NullLogger<PlayerEnterWorldService>.Instance));
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Character",
			Race = "ELYOS",
			AccountMembership = 10,
			Position = new WorldPosition(110010000, 1, 1, 1, 0, 0),
		};
		var destination = new WorldPosition(300030000, 10, 20, 30, 90, 2);
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, 300030000, "PC_ALL", MaxCount: 5, CoolTimeType: "RELATIVE", EntCoolTime: 30),
		]);
		var result = await pair.Connection.QueueInstancePortalTransferAsync(
			player,
			destination,
			reenter: false,
			cooltimes,
			TeleportAnimation.FadeOutBeam,
			staticData: null,
			now);

		Assert.Equal(destination, result.Teleport.PendingTeleport.Destination);
		Assert.True(result.Cooldown.Added);
		Assert.Equal(now.AddMinutes(30).ToUnixTimeMilliseconds(), result.Cooldown.ReuseTimeMillis);
		Assert.Collection(
			pair.SentPackets,
			packet => Assert.IsType<SmTeleportLoc>(packet),
			packet => Assert.IsType<SmInstanceInfo>(packet));
		var savedCooldowns = repository.SavedPortalCooldowns;
		Assert.NotNull(savedCooldowns);
		var savedCooldown = Assert.Single(savedCooldowns);
		Assert.Equal(300030000, savedCooldown.Key);
		Assert.Equal(result.Cooldown.ReuseTimeMillis, savedCooldown.Value.ReuseTimeMillis);
		Assert.Equal(1, savedCooldown.Value.EntryCount);
	}

	[Fact]
	public async Task QueueAllocatedInstancePortalTransferAsync_AllocatesRegistersAndTransfers()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var pair = await TestConnectionPair.CreateAsync(
			new GameServerOptions
			{
				Membership = new GameServerMembershipOptions { InstancesCooldown = 10 },
				Instance = new GameServerInstanceOptions { CooldownRate = 1 },
			},
			new PlayerEnterWorldService(
				new GameServerOptions(),
				repository,
				new GameWorld(NullLogger<GameWorld>.Instance),
				NullLogger<PlayerEnterWorldService>.Instance));
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Character",
			Race = "ELYOS",
			AccountMembership = 10,
			Position = new WorldPosition(110010000, 1, 1, 1, 0, 0),
		};
		var worldMaps = new WorldMapRuntimeStateTable(
		[
			new WorldMapSummary(300030000, IsInstance: true, TwinCount: 1),
		]);
		var cooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, 300030000, "PC_ALL", MaxCount: 5, CoolTimeType: "RELATIVE", EntCoolTime: 30),
		]);
		var portalLocation = new WorldPosition(300030000, 10, 20, 30, 90, InstanceId: 1);
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);

		var result = await pair.Connection.QueueAllocatedInstancePortalTransferAsync(
			player,
			portalLocation,
			reenter: false,
			worldMaps,
			cooltimes,
			ownerId: player.ObjectId,
			maxPlayers: 6,
			TeleportAnimation.FadeOutBeam,
			staticData: null,
			now);

		Assert.Equal(2, result.RuntimePlan.Instance.InstanceId);
		Assert.Equal(player.ObjectId, result.RuntimePlan.Instance.OwnerId);
		Assert.Equal(6, result.RuntimePlan.Instance.MaxPlayers);
		Assert.True(result.RuntimePlan.Instance.IsRegistered(player.ObjectId));
		Assert.Equal(portalLocation with { InstanceId = 2 }, result.RuntimePlan.Destination);
		Assert.Equal(result.RuntimePlan.Destination, result.Transfer.Teleport.PendingTeleport.Destination);
		Assert.True(result.Transfer.Cooldown.Added);
		Assert.Collection(
			pair.SentPackets,
			packet => Assert.IsType<SmTeleportLoc>(packet),
			packet => Assert.IsType<SmInstanceInfo>(packet));
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
			return await CreateAsync(options, playerEnterWorldService: null);
		}

		public static async Task<TestConnectionPair> CreateAsync(
			GameServerOptions options,
			PlayerEnterWorldService? playerEnterWorldService)
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
					playerEnterWorldService: playerEnterWorldService,
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
