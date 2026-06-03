using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class VortexPassedPlayerSyncRiftEntryUpdateServiceTests
{
	[Fact]
	public void CreatePlan_AppliesPassedCountAndCreatesPortalEntryUpdatePacketLikeJavaSyncPassed()
	{
		var now = DateTimeOffset.FromUnixTimeSeconds(2000);
		var portal = CreateVortexPortal(despawnTimeUnixSeconds: 2000 + 7200);
		portal.SyncPassed(usePassedPlayerCount: true, passedPlayerCount: 4);
		var syncPlan = new VortexPassedPlayerSyncPlan(
			LocationId: 0,
			PassedPlayerCount: 2,
			UsePassedPlayerCount: true,
			"controllers/RVController.syncPassed(true)");

		var result = VortexPassedPlayerSyncRiftEntryUpdateService.CreatePlan(syncPlan, portal, () => now);

		Assert.Equal(VortexPassedPlayerSyncRiftEntryUpdateStatus.Updated, result.Status);
		Assert.True(result.AppliedPortalSync);
		Assert.True(result.HasPacketIntent);
		Assert.Equal(2, portal.UsedEntries);
		var packet = Assert.IsType<SmRiftAnnounce>(result.Packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(15, reader.ReadH());
		Assert.Equal(3, (int)reader.ReadC());
		Assert.Equal(7101, reader.ReadD());
		Assert.Equal(2, reader.ReadD());
		Assert.Equal(7200, reader.ReadD());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void CreatePlan_MissingSyncPlanDoesNotMutatePortalOrCreatePacket()
	{
		var portal = CreateVortexPortal();
		portal.SyncPassed(usePassedPlayerCount: true, passedPlayerCount: 3);

		var result = VortexPassedPlayerSyncRiftEntryUpdateService.CreatePlan(syncPlan: null, portal);

		Assert.Equal(VortexPassedPlayerSyncRiftEntryUpdateStatus.MissingSyncPlan, result.Status);
		Assert.False(result.AppliedPortalSync);
		Assert.False(result.HasPacketIntent);
		Assert.Null(result.Packet);
		Assert.Equal(3, portal.UsedEntries);
	}

	[Fact]
	public void CreatePlan_MissingPortalKeepsSyncPlanAsMetadataOnly()
	{
		var syncPlan = new VortexPassedPlayerSyncPlan(
			LocationId: 0,
			PassedPlayerCount: 2,
			UsePassedPlayerCount: true,
			"controllers/RVController.syncPassed(true)");

		var result = VortexPassedPlayerSyncRiftEntryUpdateService.CreatePlan(syncPlan, portal: null);

		Assert.Equal(VortexPassedPlayerSyncRiftEntryUpdateStatus.MissingPortal, result.Status);
		Assert.Same(syncPlan, result.SyncPlan);
		Assert.False(result.AppliedPortalSync);
		Assert.False(result.HasPacketIntent);
		Assert.Null(result.Packet);
	}

	private static RiftPortalState CreateVortexPortal(long despawnTimeUnixSeconds = 5000)
	{
		var definition = new RiftDefinition(
			1170,
			"MARCHUTAN",
			"MARCHUTAN_AM",
			"MARCHUTAN_AS",
			2,
			45,
			65,
			"ASMODIANS",
			IsVortex: true);
		var template = new NpcTemplateSummary(831143, "Vortex", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC");
		var master = new WorldNpc(
			ObjectId: 7101,
			TemplateId: 831143,
			Template: template,
			Position: new WorldPosition(210060000, 10, 20, 30, 0),
			Anchor: definition.MasterAnchor);
		var slave = new WorldNpc(
			ObjectId: 7102,
			TemplateId: 831144,
			Template: template,
			Position: new WorldPosition(120080000, 40, 50, 60, 0),
			Anchor: definition.SlaveAnchor);

		return new RiftPortalState(definition, master, slave, guardsRequested: false, despawnTimeUnixSeconds);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
