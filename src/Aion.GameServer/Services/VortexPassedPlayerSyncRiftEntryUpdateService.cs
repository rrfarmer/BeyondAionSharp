using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum VortexPassedPlayerSyncRiftEntryUpdateStatus
{
	MissingSyncPlan,
	MissingPortal,
	Updated,
}

public sealed record VortexPassedPlayerSyncRiftEntryUpdateResult(
	VortexPassedPlayerSyncRiftEntryUpdateStatus Status,
	VortexPassedPlayerSyncPlan? SyncPlan,
	RiftPortalState? Portal,
	SmRiftAnnounce? Packet,
	bool AppliedPortalSync,
	bool HasPacketIntent,
	string JavaSource);

public static class VortexPassedPlayerSyncRiftEntryUpdateService
{
	public static VortexPassedPlayerSyncRiftEntryUpdateResult CreatePlan(
		VortexPassedPlayerSyncPlan? syncPlan,
		RiftPortalState? portal,
		Func<DateTimeOffset>? clock = null)
	{
		if (syncPlan == null)
		{
			return new VortexPassedPlayerSyncRiftEntryUpdateResult(
				VortexPassedPlayerSyncRiftEntryUpdateStatus.MissingSyncPlan,
				null,
				portal,
				Packet: null,
				AppliedPortalSync: false,
				HasPacketIntent: false,
				"services/vortex/Invasion.kickPlayer did not produce a syncPassed(true) plan");
		}

		if (portal == null)
		{
			return new VortexPassedPlayerSyncRiftEntryUpdateResult(
				VortexPassedPlayerSyncRiftEntryUpdateStatus.MissingPortal,
				syncPlan,
				null,
				Packet: null,
				AppliedPortalSync: false,
				HasPacketIntent: false,
				"RiftInformer.sendRiftInfo requires the active RVController/RiftPortalState before SM_RIFT_ANNOUNCE can be created");
		}

		// Java parity: RVController.syncPassed(true) sets usedEntries from passedPlayers.size(),
		// then RiftInformer creates SM_RIFT_ANNOUNCE(controller, false) entry-update packets.
		portal.SyncPassed(syncPlan.UsePassedPlayerCount, syncPlan.PassedPlayerCount);
		var packet = new SmRiftAnnounce(portal, isMaster: false, clock);
		return new VortexPassedPlayerSyncRiftEntryUpdateResult(
			VortexPassedPlayerSyncRiftEntryUpdateStatus.Updated,
			syncPlan,
			portal,
			packet,
			AppliedPortalSync: true,
			HasPacketIntent: true,
			"controllers/RVController.syncPassed(true) -> services/rift/RiftInformer.sendRiftInfo -> SM_RIFT_ANNOUNCE(controller, false)");
	}
}
