using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionKiskReviveWorkflowTests
{
	[Fact]
	public async Task HandleReviveAsync_KiskReviveConsumesChargeRestoresAndTeleports()
	{
		await using var fixture = await KiskReviveWorkflowFixture.CreateAsync();
		var player = CreateDeadPlayer(boundKiskObjectId: 9001);
		var kiskPosition = new WorldPosition(210010000, 11, 22, 33, 0);
		var kisk = fixture.RegisterKisk(objectId: 9001, kiskPosition, maxResurrects: 2);

		await fixture.Connection.HandleReviveAsync(player, CreateRevive(PlayerKiskReviveService.KiskReviveId));

		Assert.Equal(1, kisk.RemainingResurrects);
		Assert.Equal(kiskPosition, player.Position);
		Assert.Equal(new PlayerLifeStats(51, 63, 12), player.LifeStats);
		Assert.Equal(0, player.Dp);
		Assert.False(player.IsInState(PlayerCreatureState.Dead));
		Assert.True(player.IsInState(PlayerCreatureState.Active));
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmKiskUpdate>(packet),
			packet => Assert.IsType<SmEmotion>(packet),
			packet => Assert.IsType<SmChannelInfo>(packet),
			packet => Assert.IsType<SmPlayerSpawn>(packet),
			packet => Assert.IsType<SmPlayerInfo>(packet),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => Assert.IsType<SmMotion>(packet));
	}

	[Fact]
	public async Task HandleReviveAsync_LastKiskReviveChargeRemovesKiskAfterUpdate()
	{
		await using var fixture = await KiskReviveWorkflowFixture.CreateAsync();
		var player = CreateDeadPlayer(boundKiskObjectId: 9001);
		var kiskPosition = new WorldPosition(210010000, 11, 22, 33, 0);
		var kisk = fixture.RegisterKisk(objectId: 9001, kiskPosition, maxResurrects: 1);

		await fixture.Connection.HandleReviveAsync(player, CreateRevive(PlayerKiskReviveService.KiskReviveId));

		Assert.Equal(0, kisk.RemainingResurrects);
		Assert.False(fixture.RuntimeContext.Kisks.HaveKisk(kisk.OwnerObjectId));
		Assert.False(fixture.World.TryGetObject(kisk.ObjectId, out _));
		Assert.Equal(kiskPosition, player.Position);
		Assert.Equal(new PlayerLifeStats(51, 63, 12), player.LifeStats);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmKiskUpdate>(packet),
			packet => Assert.IsType<SmEmotion>(packet),
			packet => Assert.IsType<SmChannelInfo>(packet),
			packet => Assert.IsType<SmPlayerSpawn>(packet),
			packet => Assert.IsType<SmPlayerInfo>(packet),
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => Assert.IsType<SmMotion>(packet));
	}

	private static Player CreateDeadPlayer(int boundKiskObjectId)
	{
		return new Player
		{
			ObjectId = 1002,
			Name = "KiskUser",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 1,
			BoundKiskObjectId = boundKiskObjectId,
			CreatureState = PlayerCreatureState.Dead,
			Dp = 500,
			LifeStats = new PlayerLifeStats(CurrentHp: 0, CurrentMp: 0, CurrentFp: 12),
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
		};
	}

	private static WorldNpc CreateKiskNpc(int objectId, WorldPosition position)
	{
		var template = new NpcTemplateSummary(
			700273,
			"test_kisk",
			0,
			1,
			"NORMAL",
			"NORMAL",
			"PC_ALL",
			string.Empty,
			"NPC",
			KiskStats: new KiskStatsSummary(UseMask: 4, MaxMembers: 6, MaxResurrects: 2));
		return new WorldNpc(objectId, template.TemplateId, template, position);
	}

	private static CmRevive CreateRevive(int reviveId)
	{
		using var writer = new PacketBuffer();
		writer.WriteC(reviveId);
		var packet = new CmRevive(55, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private sealed class KiskReviveWorkflowFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;
		private readonly GameServerConnection _connection;

		private KiskReviveWorkflowFixture(
			TcpClient client,
			GameServerConnection connection,
			GameServerRuntimeContext runtimeContext,
			GameWorld world,
			List<GameServerPacket> sentPackets)
		{
			_client = client;
			_connection = connection;
			RuntimeContext = runtimeContext;
			World = world;
			SentPackets = sentPackets;
		}

		public GameServerConnection Connection => _connection;

		public GameServerRuntimeContext RuntimeContext { get; }

		public GameWorld World { get; }

		public List<GameServerPacket> SentPackets { get; }

		public PlayerKiskRuntimeState RegisterKisk(int objectId, WorldPosition position, int maxResurrects)
		{
			var kisk = new PlayerKiskRuntimeState(
			objectId,
			ownerObjectId: 1001,
			npcId: 700273,
			maxResurrects: maxResurrects,
			spawnedAt: DateTimeOffset.UtcNow.AddMinutes(-1),
			ownerRace: "ELYOS");
			RuntimeContext.Kisks.RegisterKisk(kisk);
			Assert.True(World.TryAddObject(objectId, CreateKiskNpc(objectId, position)));
			return kisk;
		}

		public static async Task<KiskReviveWorkflowFixture> CreateAsync()
		{
			var runtimeContext = new GameServerRuntimeContext();
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			world.Initialize();
			var sentPackets = new List<GameServerPacket>();

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
					"kisk-revive-workflow-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					runtimeContext: runtimeContext,
					world: world,
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new KiskReviveWorkflowFixture(client, connection, runtimeContext, world, sentPackets);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			await _connection.DisposeAsync();
			_client.Dispose();
		}
	}
}
