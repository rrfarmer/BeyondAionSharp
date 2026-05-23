using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerKiskNpcInfoPacketServiceTests
{
	[Fact]
	public void CreatePacketUsesAttackableCreatureTypeForEnemyRegisteredKiskWithPvpCounters()
	{
		var registry = new PlayerKiskRegistry();
		var kiskNpc = CreateNpc(9001, 700273);
		registry.RegisterKisk(new PlayerKiskRuntimeState(
			objectId: kiskNpc.ObjectId,
			ownerObjectId: 1001,
			npcId: kiskNpc.TemplateId,
			ownerRace: "ELYOS"));
		var enemyViewer = new Player { ObjectId = 1002, Race = "ASMODIANS" };

		var plan = PlayerKiskNpcInfoPacketService.CreatePacket(
			kiskNpc,
			enemyViewer,
			registry,
			new PlayerKiskNpcInfoZoneCounters(
				KiskSiegeZoneCount: 0,
				KiskPvpZoneCount: 0,
				PlayerSiegeZoneCount: 0,
				PlayerPvpZoneCount: 0));

		Assert.True(plan.RegisteredKisk);
		Assert.True(plan.UsedCreatureTypeOverride);
		Assert.False(plan.MissingZoneCounters);
		Assert.Equal(PlayerKiskCreatureType.Attackable, plan.CreatureType);
		Assert.Equal((int)PlayerKiskCreatureType.Attackable, ReadCreatureType(plan.Packet));
	}

	[Fact]
	public void CreatePacketKeepsDefaultNpcInfoWhenRegisteredKiskIsMissingZoneCounters()
	{
		var registry = new PlayerKiskRegistry();
		var kiskNpc = CreateNpc(9001, 700273);
		registry.RegisterKisk(new PlayerKiskRuntimeState(
			objectId: kiskNpc.ObjectId,
			ownerObjectId: 1001,
			npcId: kiskNpc.TemplateId,
			ownerRace: "ELYOS"));
		var enemyViewer = new Player { ObjectId = 1002, Race = "ASMODIANS" };

		var plan = PlayerKiskNpcInfoPacketService.CreatePacket(kiskNpc, enemyViewer, registry);

		Assert.True(plan.RegisteredKisk);
		Assert.False(plan.UsedCreatureTypeOverride);
		Assert.True(plan.MissingZoneCounters);
		Assert.Null(plan.CreatureType);
		Assert.Equal(38, ReadCreatureType(plan.Packet));
	}

	[Fact]
	public void CreatePacketKeepsDefaultNpcInfoForUnregisteredNpc()
	{
		var registry = new PlayerKiskRegistry();
		var ordinaryNpc = CreateNpc(5001, 203000);
		var viewer = new Player { ObjectId = 1002, Race = "ASMODIANS" };

		var plan = PlayerKiskNpcInfoPacketService.CreatePacket(
			ordinaryNpc,
			viewer,
			registry,
			new PlayerKiskNpcInfoZoneCounters(
				KiskSiegeZoneCount: 0,
				KiskPvpZoneCount: 0,
				PlayerSiegeZoneCount: 0,
				PlayerPvpZoneCount: 0));

		Assert.False(plan.RegisteredKisk);
		Assert.False(plan.UsedCreatureTypeOverride);
		Assert.False(plan.MissingZoneCounters);
		Assert.Null(plan.CreatureType);
		Assert.Equal(38, ReadCreatureType(plan.Packet));
	}

	[Fact]
	public void CreatePacketUsesSupportCreatureTypeForSameRaceRegisteredKisk()
	{
		var registry = new PlayerKiskRegistry();
		var kiskNpc = CreateNpc(9001, 700273);
		registry.RegisterKisk(new PlayerKiskRuntimeState(
			objectId: kiskNpc.ObjectId,
			ownerObjectId: 1001,
			npcId: kiskNpc.TemplateId,
			ownerRace: "ELYOS"));
		var sameRaceViewer = new Player { ObjectId = 1003, Race = "ELYOS" };

		var plan = PlayerKiskNpcInfoPacketService.CreatePacket(
			kiskNpc,
			sameRaceViewer,
			registry,
			new PlayerKiskNpcInfoZoneCounters(
				KiskSiegeZoneCount: 0,
				KiskPvpZoneCount: 0,
				PlayerSiegeZoneCount: 0,
				PlayerPvpZoneCount: 0));

		Assert.True(plan.RegisteredKisk);
		Assert.True(plan.UsedCreatureTypeOverride);
		Assert.False(plan.MissingZoneCounters);
		Assert.Equal(PlayerKiskCreatureType.Support, plan.CreatureType);
		Assert.Equal((int)PlayerKiskCreatureType.Support, ReadCreatureType(plan.Packet));
	}

	[Fact]
	public void CreatePacketCanReadCreatureCountersFromZoneCounterService()
	{
		var registry = new PlayerKiskRegistry();
		var zoneCounterService = new CreaturePvpZoneCounterService();
		var kiskNpc = CreateNpc(9001, 700273);
		registry.RegisterKisk(new PlayerKiskRuntimeState(
			objectId: kiskNpc.ObjectId,
			ownerObjectId: 1001,
			npcId: kiskNpc.TemplateId,
			ownerRace: "ELYOS"));
		var enemyViewer = new Player { ObjectId = 1002, Race = "ASMODIANS" };
		zoneCounterService.EnterZone(kiskNpc.ObjectId, CreaturePvpZoneCounterType.Pvp);

		var blockedPlan = PlayerKiskNpcInfoPacketService.CreatePacket(
			kiskNpc,
			enemyViewer,
			registry,
			zoneCounterService);
		zoneCounterService.EnterZone(kiskNpc.ObjectId, CreaturePvpZoneCounterType.Pvp);
		var attackablePlan = PlayerKiskNpcInfoPacketService.CreatePacket(
			kiskNpc,
			enemyViewer,
			registry,
			zoneCounterService);

		Assert.Equal(PlayerKiskCreatureType.Support, blockedPlan.CreatureType);
		Assert.Equal((int)PlayerKiskCreatureType.Support, ReadCreatureType(blockedPlan.Packet));
		Assert.Equal(PlayerKiskCreatureType.Attackable, attackablePlan.CreatureType);
		Assert.Equal((int)PlayerKiskCreatureType.Attackable, ReadCreatureType(attackablePlan.Packet));
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
