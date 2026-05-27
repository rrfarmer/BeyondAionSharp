using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using GameWorld = Aion.GameServer.World.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameClientSocketServerNpcVisibilityTests
{
	[Fact]
	public void CreateNpcInfoPacketForViewerUsesKiskCreatureTypeFromZoneCounterService()
	{
		var runtimeContext = new GameServerRuntimeContext();
		var zoneCounterService = new CreaturePvpZoneCounterService();
		var server = CreateServer(runtimeContext, zoneCounterService);
		var kiskNpc = CreateNpc(9001, 700273);
		runtimeContext.Kisks.RegisterKisk(new PlayerKiskRuntimeState(
			objectId: kiskNpc.ObjectId,
			ownerObjectId: 1001,
			npcId: kiskNpc.TemplateId,
			ownerRace: "ELYOS"));
		var enemyViewer = new Player { ObjectId = 1002, Race = "ASMODIANS" };
		zoneCounterService.EnterZone(kiskNpc.ObjectId, CreaturePvpZoneCounterType.Pvp);

		var supportPacket = server.CreateNpcInfoPacketForViewer(kiskNpc, enemyViewer);
		zoneCounterService.EnterZone(kiskNpc.ObjectId, CreaturePvpZoneCounterType.Pvp);
		var attackablePacket = server.CreateNpcInfoPacketForViewer(kiskNpc, enemyViewer);

		Assert.Equal((int)PlayerKiskCreatureType.Support, ReadCreatureType(supportPacket));
		Assert.Equal((int)PlayerKiskCreatureType.Attackable, ReadCreatureType(attackablePacket));
	}

	[Fact]
	public void CreateNpcInfoPacketForViewerUsesMovementFedPvpZoneCounters()
	{
		var runtimeContext = new GameServerRuntimeContext();
		var zoneCounterService = new CreaturePvpZoneCounterService();
		var server = CreateServer(runtimeContext, zoneCounterService);
		var zones = new CreaturePvpZoneTable(
		[
			CreateZone("PVP_A_210010000", 0, 0, 20, 20),
			CreateZone("PVP_B_210010000", 10, 0, 30, 20),
		]);
		var kiskNpc = CreateNpc(9001, 700273, new WorldPosition(210010000, 5, 5, 50, 0));
		runtimeContext.Kisks.RegisterKisk(new PlayerKiskRuntimeState(
			objectId: kiskNpc.ObjectId,
			ownerObjectId: 1001,
			npcId: kiskNpc.TemplateId,
			ownerRace: "ELYOS"));
		var enemyViewer = new Player { ObjectId = 1002, Race = "ASMODIANS" };

		CreaturePvpZoneRevalidationService.Revalidate(kiskNpc.ObjectId, kiskNpc.Position, zones, zoneCounterService);
		var supportPacket = server.CreateNpcInfoPacketForViewer(kiskNpc, enemyViewer);
		var overlappedKiskNpc = kiskNpc with { Position = new WorldPosition(210010000, 15, 5, 50, 0) };
		CreaturePvpZoneRevalidationService.Revalidate(overlappedKiskNpc.ObjectId, overlappedKiskNpc.Position, zones, zoneCounterService);
		var attackablePacket = server.CreateNpcInfoPacketForViewer(overlappedKiskNpc, enemyViewer);

		Assert.Equal((int)PlayerKiskCreatureType.Support, ReadCreatureType(supportPacket));
		Assert.Equal((int)PlayerKiskCreatureType.Attackable, ReadCreatureType(attackablePacket));
	}

	[Fact]
	public void CreateNpcInfoPacketForViewerKeepsDefaultNpcInfoWhenKiskCountersAreUnavailable()
	{
		var runtimeContext = new GameServerRuntimeContext();
		var server = CreateServer(runtimeContext, zoneCounterService: null);
		var kiskNpc = CreateNpc(9001, 700273);
		runtimeContext.Kisks.RegisterKisk(new PlayerKiskRuntimeState(
			objectId: kiskNpc.ObjectId,
			ownerObjectId: 1001,
			npcId: kiskNpc.TemplateId,
			ownerRace: "ELYOS"));
		var enemyViewer = new Player { ObjectId = 1002, Race = "ASMODIANS" };

		var packet = server.CreateNpcInfoPacketForViewer(kiskNpc, enemyViewer);

		Assert.Equal(38, ReadCreatureType(packet));
	}

	[Fact]
	public void CreateNpcInfoPacketForViewerKeepsOrdinaryNpcInfoUnchanged()
	{
		var runtimeContext = new GameServerRuntimeContext();
		var zoneCounterService = new CreaturePvpZoneCounterService();
		var server = CreateServer(runtimeContext, zoneCounterService);
		var ordinaryNpc = CreateNpc(5001, 203000);
		var viewer = new Player { ObjectId = 1002, Race = "ASMODIANS" };

		var packet = server.CreateNpcInfoPacketForViewer(ordinaryNpc, viewer);

		Assert.Equal(38, ReadCreatureType(packet));
	}

	[Fact]
	public async Task RefreshNpcVisibilityAsync_SendsAppearedNpcInfoBeforeDisappearedDelete()
	{
		await using var fixture = await NpcVisibilityFixture.CreateAsync();
		var player = new Player
		{
			ObjectId = 1002,
			Name = "NpcViewer",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};
		var oldKiskNpc = CreateNpc(9001, 700273, new WorldPosition(210010000, 10, 0, 0, 0));
		var newNpc = CreateNpc(5001, 203000, new WorldPosition(210010000, 15, 0, 0, 0));
		SetActivePlayer(fixture.Connection, player);
		fixture.Server.RegisterPlayerConnection(player.ObjectId, fixture.Connection);

		await fixture.Server.RefreshNpcVisibilityAsync([oldKiskNpc], player.ObjectId);
		fixture.SentPackets.Clear();
		var sent = await fixture.Server.RefreshNpcVisibilityAsync([newNpc], player.ObjectId);

		Assert.Equal(2, sent);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmNpcInfo>(packet),
			packet => AssertDeletePayload(Assert.IsType<SmDelete>(packet), oldKiskNpc.ObjectId));
	}

	[Fact]
	public async Task RemoveRuntimeKiskAsync_RefreshesKnownViewerWithDeleteAfterWorldRemoval()
	{
		await using var fixture = await NpcVisibilityFixture.CreateAsync();
		var player = new Player
		{
			ObjectId = 1002,
			Name = "KiskViewer",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};
		var kiskNpc = CreateNpc(9001, 700273, new WorldPosition(210010000, 10, 0, 0, 0));
		fixture.RuntimeContext.Kisks.RegisterKisk(new PlayerKiskRuntimeState(
			objectId: kiskNpc.ObjectId,
			ownerObjectId: 1001,
			npcId: kiskNpc.TemplateId,
			ownerRace: "ELYOS"));
		Assert.True(fixture.World.TryAddObject(kiskNpc.ObjectId, kiskNpc));
		SetActivePlayer(fixture.Connection, player);
		fixture.Server.RegisterPlayerConnection(player.ObjectId, fixture.Connection);

		await fixture.Server.RefreshNpcVisibilityAsync(fixture.World.GetNpcs(kiskNpc.Position.WorldId), player.ObjectId);
		Assert.IsType<SmNpcInfo>(Assert.Single(fixture.SentPackets));
		fixture.SentPackets.Clear();
		await fixture.Connection.RemoveRuntimeKiskAsync(kiskNpc.ObjectId);

		Assert.False(fixture.World.TryGetObject(kiskNpc.ObjectId, out _));
		Assert.False(fixture.RuntimeContext.Kisks.HaveKisk(1001));
		var deletePacket = Assert.IsType<SmDelete>(Assert.Single(fixture.SentPackets));
		AssertDeletePayload(deletePacket, kiskNpc.ObjectId);
	}

	private static GameClientSocketServer CreateServer(
		GameServerRuntimeContext runtimeContext,
		CreaturePvpZoneCounterService? zoneCounterService)
	{
		var options = new GameServerOptions
		{
			Network = new GameServerNetworkOptions
			{
				ClientEndPoint = new IPEndPoint(IPAddress.Loopback, 0),
				MaxOnlinePlayers = 10,
			},
		};
		return new GameClientSocketServer(
			NullLogger<GameClientSocketServer>.Instance,
			options,
			new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
			runtimeContext: runtimeContext,
			creaturePvpZoneCounterService: zoneCounterService);
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

	private static WorldNpc CreateNpc(int objectId, int templateId, WorldPosition? position = null)
	{
		var template = new NpcTemplateSummary(
			templateId,
			$"npc-{templateId}",
			NameId: 1,
			Level: 1,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "ELYOS",
			Tribe: "GENERAL",
			Type: "GENERAL",
			Height: 1,
			AttackSpeed: 2000,
			MaxHp: 1000,
			RunSpeed: 4,
			BoundRadius: 0.5f);
		return new WorldNpc(
			objectId,
			template.TemplateId,
			template,
			position ?? new WorldPosition(210010000, 10, 20, 30, 90));
	}

	private static CreaturePvpZoneSummary CreateZone(
		string name,
		float left,
		float bottom,
		float right,
		float top)
	{
		return new CreaturePvpZoneSummary(
			210010000,
			name,
			CreaturePvpZoneType.Pvp,
			Flags: 0,
			Bottom: 0,
			Top: 100,
			Points:
			[
				new ZonePoint2D(left, bottom),
				new ZonePoint2D(right, bottom),
				new ZonePoint2D(right, top),
				new ZonePoint2D(left, top),
			]);
	}

	private static int ReadCreatureType(SmNpcInfo packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		reader.ReadF();
		reader.ReadF();
		reader.ReadF();
		reader.ReadD();
		reader.ReadD();
		reader.ReadD();
		return reader.ReadC();
	}

	private static void AssertDeletePayload(SmDelete packet, int expectedObjectId)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedObjectId, reader.ReadD());
		Assert.Equal((byte)ObjectDeleteAnimation.FadeOut, reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private sealed class NpcVisibilityFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private NpcVisibilityFixture(
			TcpClient client,
			GameClientSocketServer server,
			GameServerConnection connection,
			GameServerRuntimeContext runtimeContext,
			GameWorld world,
			List<GameServerPacket> sentPackets)
		{
			_client = client;
			Server = server;
			Connection = connection;
			RuntimeContext = runtimeContext;
			World = world;
			SentPackets = sentPackets;
		}

		public GameClientSocketServer Server { get; }

		public GameServerConnection Connection { get; }

		public GameServerRuntimeContext RuntimeContext { get; }

		public GameWorld World { get; }

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<NpcVisibilityFixture> CreateAsync()
		{
			var sentPackets = new List<GameServerPacket>();
			var runtimeContext = new GameServerRuntimeContext();
			var world = new GameWorld(NullLogger<GameWorld>.Instance);
			world.Initialize();
			var idFactory = new IDFactory([9001]);
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
				packetProcessor,
				runtimeContext: runtimeContext);
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
					"npc-visibility-test",
					packetProcessor,
					options: options,
					runtimeContext: runtimeContext,
					connectionRegistry: server,
					idFactory: idFactory,
					world: world,
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new NpcVisibilityFixture(client, server, connection, runtimeContext, world, sentPackets);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			Server.UnregisterPlayerConnection(1002, Connection);
			await Connection.DisposeAsync();
			_client.Dispose();
		}
	}
}
