namespace Aion.GameServer.Services;

public enum VortexRiftEntryUpdatePipelinePlanStatus
{
	MissingSyncPlan,
	MissingPortal,
	NoTargetPlayers,
	Ready,
}

public sealed record VortexRiftEntryUpdatePipelinePlan(
	VortexRiftEntryUpdatePipelinePlanStatus Status,
	VortexPassedPlayerSyncPlan? SyncPlan,
	RiftPortalState? Portal,
	bool IsMasterController,
	IReadOnlyList<VortexRiftEntryUpdateOnlinePlayerSnapshot> OnlinePlayers,
	VortexPassedPlayerSyncRiftEntryUpdateResult EntryUpdate,
	VortexRiftEntryUpdateWorldTargetPlan WorldTargetPlan,
	VortexRiftEntryUpdatePlayerTargetPlan PlayerTargetPlan,
	VortexRiftEntryUpdateCompositionPlan CompositionPlan,
	IReadOnlyList<int> WorldIds,
	IReadOnlyList<int> TargetPlayerObjectIds,
	bool ReadyForBridge,
	string JavaSource);

public static class VortexRiftEntryUpdatePipelinePlanService
{
	public static VortexRiftEntryUpdatePipelinePlan CreatePlan(
		VortexPassedPlayerSyncPlan? syncPlan,
		RiftPortalState? portal,
		bool isMasterController,
		IReadOnlyList<VortexRiftEntryUpdateOnlinePlayerSnapshot> onlinePlayers,
		Func<DateTimeOffset>? clock = null)
	{
		ArgumentNullException.ThrowIfNull(onlinePlayers);

		var entryUpdate = VortexPassedPlayerSyncRiftEntryUpdateService.CreatePlan(syncPlan, portal, clock);
		var worldTargetPlan = VortexRiftEntryUpdateWorldTargetPlanService.CreatePlan(portal, isMasterController);
		var playerTargetPlan = VortexRiftEntryUpdatePlayerTargetPlanService.CreatePlan(worldTargetPlan, onlinePlayers);
		var compositionPlan = VortexRiftEntryUpdateCompositionPlanService.CreatePlan(
			entryUpdate,
			worldTargetPlan,
			playerTargetPlan);

		return CreateResult(
			ResolveStatus(syncPlan, portal, compositionPlan),
			syncPlan,
			portal,
			isMasterController,
			onlinePlayers,
			entryUpdate,
			worldTargetPlan,
			playerTargetPlan,
			compositionPlan);
	}

	private static VortexRiftEntryUpdatePipelinePlanStatus ResolveStatus(
		VortexPassedPlayerSyncPlan? syncPlan,
		RiftPortalState? portal,
		VortexRiftEntryUpdateCompositionPlan compositionPlan)
	{
		if (syncPlan == null)
			return VortexRiftEntryUpdatePipelinePlanStatus.MissingSyncPlan;

		if (portal == null)
			return VortexRiftEntryUpdatePipelinePlanStatus.MissingPortal;

		if (!compositionPlan.ReadyForDispatch)
			return VortexRiftEntryUpdatePipelinePlanStatus.NoTargetPlayers;

		return VortexRiftEntryUpdatePipelinePlanStatus.Ready;
	}

	private static VortexRiftEntryUpdatePipelinePlan CreateResult(
		VortexRiftEntryUpdatePipelinePlanStatus status,
		VortexPassedPlayerSyncPlan? syncPlan,
		RiftPortalState? portal,
		bool isMasterController,
		IReadOnlyList<VortexRiftEntryUpdateOnlinePlayerSnapshot> onlinePlayers,
		VortexPassedPlayerSyncRiftEntryUpdateResult entryUpdate,
		VortexRiftEntryUpdateWorldTargetPlan worldTargetPlan,
		VortexRiftEntryUpdatePlayerTargetPlan playerTargetPlan,
		VortexRiftEntryUpdateCompositionPlan compositionPlan)
	{
		return new VortexRiftEntryUpdatePipelinePlan(
			status,
			syncPlan,
			portal,
			isMasterController,
			onlinePlayers,
			entryUpdate,
			worldTargetPlan,
			playerTargetPlan,
			compositionPlan,
			compositionPlan.WorldIds,
			compositionPlan.TargetPlayerObjectIds,
			compositionPlan.ReadyForDispatch,
			"services/vortex/Invasion.kickPlayer -> controllers/RVController.syncPassed(true) -> RiftInformer.sendRiftInfo(getWorldsList(this))");
	}
}
