using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class RiftAnnouncePacketTests
{
	[Fact]
	public void Aggregate_WritesJavaShapedAnnouncePayload()
	{
		var packet = new SmRiftAnnounce(new RiftAnnounceData([1, 0, 0, 0, 2, 0, 3, 0, 0, 0, 0, 0]));

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(49, reader.ReadH());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(1, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(2, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(3, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void Silentera_WritesJavaShapedWorldFlags()
	{
		var packet = new SmRiftAnnounce(gelkmaros: true, inggison: false);

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(9, reader.ReadH());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(1, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void Despawn_WritesJavaShapedObjectId()
	{
		var packet = new SmRiftAnnounce(123456);

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(5, reader.ReadH());
		Assert.Equal(4, (int)reader.ReadC());
		Assert.Equal(123456, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void PortalDetail_WritesJavaShapedRiftInfo()
	{
		var now = DateTimeOffset.FromUnixTimeSeconds(1000);
		var portal = CreatePortal(
			new RiftDefinition(1170, "KAISINEL", "KAISINEL_AM", "KAISINEL_AS", 24, 45, 65, "ASMODIANS", IsVortex: true),
			guardsRequested: false,
			despawnTimeUnixSeconds: 1000 + 3600);
		var packet = new SmRiftAnnounce(portal, isMaster: true, () => now);

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(35, reader.ReadH());
		Assert.Equal(2, (int)reader.ReadC());
		Assert.Equal(7101, reader.ReadD());
		Assert.Equal(24, reader.ReadD());
		Assert.Equal(3600, reader.ReadD());
		Assert.Equal(45, reader.ReadD());
		Assert.Equal(65, reader.ReadD());
		Assert.Equal(1.25f, reader.ReadF());
		Assert.Equal(2.5f, reader.ReadF());
		Assert.Equal(3.75f, reader.ReadF());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void PortalEntryUpdate_WritesJavaShapedUsedEntries()
	{
		var now = DateTimeOffset.FromUnixTimeSeconds(2000);
		var portal = CreatePortal(
			new RiftDefinition(2176, "CYGNEA", "CYGNEA_GM", "ENSHAR_GS", 144, 60, 65, "ASMODIANS", CanBeVolatile: true),
			guardsRequested: true,
			despawnTimeUnixSeconds: 2000 + 7200);
		portal.SyncPassed(isInvasion: false);
		portal.SyncPassed(isInvasion: false);
		var packet = new SmRiftAnnounce(portal, isMaster: false, () => now);

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(15, reader.ReadH());
		Assert.Equal(3, (int)reader.ReadC());
		Assert.Equal(7101, reader.ReadD());
		Assert.Equal(2, reader.ReadD());
		Assert.Equal(7200, reader.ReadD());
		Assert.Equal(4, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	[Theory]
	[InlineData(false, false, false, false, 0)]
	[InlineData(true, false, false, false, 1)]
	[InlineData(false, true, false, true, 4)]
	[InlineData(false, false, true, false, 5)]
	public void PortalPackets_WriteJavaRiftTypeByte(
		bool isVortex,
		bool canBeVolatile,
		bool isInvasion,
		bool guardsRequested,
		int expectedType)
	{
		var portal = CreatePortal(
			new RiftDefinition(
				1,
				"TEST",
				"MASTER",
				"SLAVE",
				10,
				20,
				30,
				"ELYOS",
				IsVortex: isVortex,
				CanBeVolatile: canBeVolatile,
				IsInvasionRift: isInvasion),
			guardsRequested,
			despawnTimeUnixSeconds: 5000);
		var packet = new SmRiftAnnounce(portal, isMaster: false, () => DateTimeOffset.FromUnixTimeSeconds(4000));

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));

		reader.ReadH();
		reader.ReadC();
		reader.ReadD();
		reader.ReadD();
		reader.ReadD();
		Assert.Equal(expectedType, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static RiftPortalState CreatePortal(
		RiftDefinition definition,
		bool guardsRequested,
		long despawnTimeUnixSeconds)
	{
		var template = new NpcTemplateSummary(730100, "Rift", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC");
		var master = new WorldNpc(
			ObjectId: 7101,
			TemplateId: 730100,
			Template: template,
			Position: new WorldPosition(210070000, 1.25f, 2.5f, 3.75f, 0),
			Anchor: definition.MasterAnchor);
		var slave = new WorldNpc(
			ObjectId: 7102,
			TemplateId: 730101,
			Template: template,
			Position: new WorldPosition(220080000, 4.25f, 5.5f, 6.75f, 0),
			Anchor: definition.SlaveAnchor);

		return new RiftPortalState(definition, master, slave, guardsRequested, despawnTimeUnixSeconds);
	}
}
