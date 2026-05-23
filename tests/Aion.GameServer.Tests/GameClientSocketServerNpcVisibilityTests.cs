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

	private static WorldNpc CreateNpc(int objectId, int templateId)
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
			new WorldPosition(210010000, 10, 20, 30, 90));
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
