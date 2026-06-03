using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class RiftPortalUseServiceTests
{
	[Fact]
	public void AcceptPortal_ForOrdinaryRift_TeleportsToSlaveSpawnAndIncrementsUsedEntries()
	{
		var service = new RiftPortalUseService();
		var destination = new WorldPosition(220020000, 100, 200, 300, 11);
		var portal = CreatePortal(
			new RiftDefinition(2120, "ELTNEN", "ELTNEN_AM", "MORHEIM_AS", 36, 20, 40, "ASMODIANS"),
			slaveSpawnPosition: destination);
		var player = CreatePlayer(level: 30);

		var result = service.AcceptPortal(player, portal);

		Assert.True(result.Accepted);
		Assert.Equal(RiftPortalUseStatus.Accepted, result.Status);
		Assert.Equal(destination, result.Destination);
		Assert.Equal(destination, player.Position);
		Assert.Equal(1, portal.UsedEntries);
		Assert.Equal(0, portal.PassedPlayerCount);
	}

	[Theory]
	[InlineData(19)]
	[InlineData(41)]
	public void AcceptPortal_RejectsPlayersOutsideLevelRange(int playerLevel)
	{
		var service = new RiftPortalUseService();
		var portal = CreatePortal(new RiftDefinition(2120, "ELTNEN", "ELTNEN_AM", "MORHEIM_AS", 36, 20, 40, "ASMODIANS"));
		var player = CreatePlayer(playerLevel);
		var originalPosition = player.Position;

		var result = service.AcceptPortal(player, portal);

		Assert.False(result.Accepted);
		Assert.Equal(RiftPortalUseStatus.LevelRestricted, result.Status);
		Assert.Null(result.Destination);
		Assert.Equal(originalPosition, player.Position);
		Assert.Equal(0, portal.UsedEntries);
	}

	[Fact]
	public void AcceptPortal_ForVortexRift_UsesResolverAndPassedPlayerCount()
	{
		var service = new RiftPortalUseService();
		var destination = new WorldPosition(120080000, 10, 20, 30, 4);
		var portal = CreatePortal(
			new RiftDefinition(1170, "KAISINEL", "KAISINEL_AM", "KAISINEL_AS", 2, 45, 65, "ASMODIANS", IsVortex: true));
		var firstPlayer = CreatePlayer(level: 50, objectId: 100);
		var secondPlayer = CreatePlayer(level: 50, objectId: 101);

		var first = service.AcceptPortal(firstPlayer, portal, _ => destination);
		var second = service.AcceptPortal(secondPlayer, portal, _ => destination);

		Assert.True(first.Accepted);
		Assert.Equal(destination, firstPlayer.Position);
		Assert.True(second.Accepted);
		Assert.Equal(destination, secondPlayer.Position);
		Assert.Equal(2, portal.PassedPlayerCount);
		Assert.Equal(2, portal.UsedEntries);
	}

	[Fact]
	public void AcceptPortal_ForVortexRift_RecordsPassedPlayerInVortexRuntimeLikeJavaController()
	{
		var vortexRuntime = new VortexInvasionRuntime();
		var service = new RiftPortalUseService(vortexRuntime);
		var location = new VortexLocationSummary(
			0,
			"ELYOS",
			"ASMODIANS",
			new WorldPosition(120080000, 559.4f, 207.8f, 93.5f, 0),
			new WorldPosition(210060000, 951.0f, 2433.0f, 107.0f, 0),
			new WorldPosition(210060000, 951.0f, 2433.0f, 107.0f, 0));
		var portal = CreatePortal(
			new RiftDefinition(1170, "MARCHUTAN", "MARCHUTAN_AM", "MARCHUTAN_AS", 2, 45, 65, "ASMODIANS", IsVortex: true),
			masterTemplateId: 831143);
		var player = CreatePlayer(level: 50, objectId: 1002);
		vortexRuntime.StartInvasion(location);

		var result = service.AcceptPortal(
			player,
			portal,
			_ => location.StartPoint,
			_ => location);

		Assert.True(result.Accepted);
		Assert.Equal(location.StartPoint, player.Position);
		Assert.Equal(1, portal.PassedPlayerCount);
		Assert.Equal(1, portal.UsedEntries);
		var snapshot = Assert.IsType<VortexInvasionSnapshot>(vortexRuntime.GetSnapshot(location.Id));
		Assert.Equal([1002], snapshot.PassedPlayerObjectIds);
		Assert.Empty(snapshot.InvaderObjectIds);

		var join = vortexRuntime.AddInvaderFromPassedPortal(location, player);
		Assert.True(join.Added);
		Assert.Equal([1002], Assert.IsType<VortexInvasionSnapshot>(vortexRuntime.GetSnapshot(location.Id)).InvaderObjectIds);
	}

	[Fact]
	public void AcceptPortal_ForVortexRift_RejectsWhenEntryLimitReached()
	{
		var service = new RiftPortalUseService();
		var destination = new WorldPosition(120080000, 10, 20, 30, 4);
		var portal = CreatePortal(
			new RiftDefinition(1170, "KAISINEL", "KAISINEL_AM", "KAISINEL_AS", 1, 45, 65, "ASMODIANS", IsVortex: true));
		Assert.True(service.AcceptPortal(CreatePlayer(level: 50, objectId: 100), portal, _ => destination).Accepted);
		var blockedPlayer = CreatePlayer(level: 50, objectId: 101);
		var originalPosition = blockedPlayer.Position;

		var result = service.AcceptPortal(blockedPlayer, portal, _ => destination);

		Assert.False(result.Accepted);
		Assert.Equal(RiftPortalUseStatus.EntryLimitReached, result.Status);
		Assert.Equal(originalPosition, blockedPlayer.Position);
		Assert.Equal(1, portal.PassedPlayerCount);
		Assert.Equal(1, portal.UsedEntries);
	}

	[Fact]
	public void AcceptPortal_ForVortexRift_RequiresDestinationResolver()
	{
		var service = new RiftPortalUseService();
		var portal = CreatePortal(
			new RiftDefinition(1170, "KAISINEL", "KAISINEL_AM", "KAISINEL_AS", 2, 45, 65, "ASMODIANS", IsVortex: true));
		var player = CreatePlayer(level: 50);

		var result = service.AcceptPortal(player, portal);

		Assert.False(result.Accepted);
		Assert.Equal(RiftPortalUseStatus.MissingVortexDestination, result.Status);
		Assert.Equal(0, portal.UsedEntries);
	}

	[Theory]
	[InlineData(false, true, RiftPortalUseStatus.NotAccepting)]
	[InlineData(true, false, RiftPortalUseStatus.OwnerNotSpawned)]
	public void AcceptPortal_RejectsClosedOrDespawnedRifts(
		bool isAccepting,
		bool ownerSpawned,
		RiftPortalUseStatus expectedStatus)
	{
		var service = new RiftPortalUseService();
		var portal = CreatePortal(new RiftDefinition(2120, "ELTNEN", "ELTNEN_AM", "MORHEIM_AS", 36, 20, 40, "ASMODIANS"));
		var player = CreatePlayer(level: 30);

		var result = service.AcceptPortal(player, portal, isAccepting: isAccepting, ownerSpawned: ownerSpawned);

		Assert.False(result.Accepted);
		Assert.Equal(expectedStatus, result.Status);
		Assert.Equal(0, portal.UsedEntries);
	}

	private static Player CreatePlayer(int level, int objectId = 100)
	{
		return new Player
		{
			ObjectId = objectId,
			Level = level,
			Race = "ELYOS",
			Position = new WorldPosition(210020000, 1, 1, 1, 0),
		};
	}

	private static RiftPortalState CreatePortal(
		RiftDefinition definition,
		WorldPosition? slaveSpawnPosition = null,
		int masterTemplateId = 730100)
	{
		var template = new NpcTemplateSummary(masterTemplateId, "Rift", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC");
		var master = new WorldNpc(
			ObjectId: 7101,
			TemplateId: masterTemplateId,
			Template: template,
			Position: new WorldPosition(210020000, 10, 20, 30, 0),
			Anchor: definition.MasterAnchor);
		var slave = new WorldNpc(
			ObjectId: 7102,
			TemplateId: 730101,
			Template: template,
			Position: new WorldPosition(220020000, 40, 50, 60, 0),
			Anchor: definition.SlaveAnchor,
			SpawnPosition: slaveSpawnPosition);

		return new RiftPortalState(definition, master, slave, guardsRequested: false, despawnTimeUnixSeconds: 5000);
	}
}
