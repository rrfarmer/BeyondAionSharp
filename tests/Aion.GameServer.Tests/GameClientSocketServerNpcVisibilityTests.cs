using System.Net;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
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

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
