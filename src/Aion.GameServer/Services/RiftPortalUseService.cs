using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed class RiftPortalUseService(
	VortexLocationService? vortexLocationService = null)
{
	public RiftPortalUseResult AcceptPortal(
		Player player,
		RiftPortalState portal,
		Func<RiftPortalState, WorldPosition?>? vortexDestinationResolver = null,
		Func<RiftPortalState, VortexLocationSummary?>? vortexLocationResolver = null,
		bool isAccepting = true,
		bool ownerSpawned = true)
	{
		// Java parity: controllers/RVController.onAccept rejects closed/despawned rifts before level and entry checks.
		if (!isAccepting)
			return RiftPortalUseResult.Rejected(RiftPortalUseStatus.NotAccepting);
		if (!ownerSpawned)
			return RiftPortalUseResult.Rejected(RiftPortalUseStatus.OwnerNotSpawned);
		if (player.Level > portal.MaxLevel || player.Level < portal.MinLevel)
			return RiftPortalUseResult.Rejected(RiftPortalUseStatus.LevelRestricted);
		if (portal.IsVortex && portal.UsedEntries >= portal.MaxEntries)
			return RiftPortalUseResult.Rejected(RiftPortalUseStatus.EntryLimitReached);

		var destination = portal.IsVortex
			? vortexDestinationResolver?.Invoke(portal)
			: portal.SlaveNpc.SpawnLocation;
		if (destination == null)
			return RiftPortalUseResult.Rejected(RiftPortalUseStatus.MissingVortexDestination);

		// Java parity: controllers/RVController.onAccept -> TeleportService.teleportTo(player, startPoint).
		Aion.GameServer.Services.Teleport.TeleportService.TeleportTo(player, destination);
		if (portal.IsVortex)
		{
			// Java parity: RVController vortex branch records passedPlayers, then syncPassed(true).
			portal.AddPassedPlayer(player.ObjectId);
			portal.SyncPassed(usePassedPlayerCount: true, passedPlayerCount: portal.PassedPlayerCount);
			// Java parity: RVController.onAccept records the vortex pass; the faithful path runs through VortexService
			// (the reworked VortexInvasionRuntime recorder was deleted), so no per-pass recorder call here.
			_ = vortexLocationResolver?.Invoke(portal)
				?? vortexLocationService?.GetLocationByRift(portal.MasterNpc.TemplateId);
		}
		else
		{
			// Java parity: RVController ordinary branch teleports to slaveSpawnTemplate and syncPassed(false).
			portal.SyncPassed(usePassedPlayerCount: false);
		}

		return RiftPortalUseResult.CreateAccepted(destination);
	}
}

public sealed record RiftPortalUseResult(
	bool Accepted,
	RiftPortalUseStatus Status,
	WorldPosition? Destination)
{
	public static RiftPortalUseResult CreateAccepted(WorldPosition destination)
	{
		return new RiftPortalUseResult(true, RiftPortalUseStatus.Accepted, destination);
	}

	public static RiftPortalUseResult Rejected(RiftPortalUseStatus status)
	{
		return new RiftPortalUseResult(false, status, null);
	}
}

public enum RiftPortalUseStatus
{
	Accepted,
	NotAccepting,
	OwnerNotSpawned,
	LevelRestricted,
	EntryLimitReached,
	MissingVortexDestination,
}
