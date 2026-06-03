namespace Aion.GameServer.Services;

public enum VortexRemovalRiftEntryUpdateReportStatus
{
	MissingRemoval,
	NoRemoval,
	MissingSyncPlan,
	MissingActivePortal,
	NotReady,
	ReadyNoDispatch,
	Delegated,
}

public sealed record VortexRemovalRiftEntryUpdateReport(
	VortexRemovalRiftEntryUpdateReportStatus Status,
	int LocationId,
	int RemovedPlayerObjectId,
	bool RemovedPassedPlayer,
	VortexPassedPlayerSyncPlan? SyncPlan,
	RiftPortalState? ActivePortal,
	VortexRiftEntryUpdatePipelinePlan? PipelinePlan,
	VortexRiftEntryUpdateCompositionDispatchBridgeResult? BridgeResult,
	IReadOnlyList<VortexRiftEntryUpdateOnlinePlayerSnapshot> OnlinePlayers,
	IReadOnlyList<int> WorldIds,
	IReadOnlyList<int> TargetPlayerObjectIds,
	bool ReadyForDispatch,
	bool DidCallDispatch,
	bool SendsPackets,
	string JavaSource);

public sealed class VortexRemovalRiftEntryUpdateReportService(
	bool dispatchEnabled = false,
	VortexRiftEntryUpdateCompositionDispatchBridgeService? dispatchBridge = null)
{
	private readonly VortexRiftEntryUpdateCompositionDispatchBridgeService _dispatchBridge =
		dispatchBridge ?? new VortexRiftEntryUpdateCompositionDispatchBridgeService(enabled: dispatchEnabled);

	public Task<VortexRemovalRiftEntryUpdateReport> CreateReportAsync(
		VortexInvaderRemovalResult? removal,
		bool isMasterController,
		IReadOnlyList<VortexRiftEntryUpdateOnlinePlayerSnapshot> onlinePlayers,
		Func<DateTimeOffset>? clock = null,
		CancellationToken cancellationToken = default)
	{
		var removalView = removal == null
			? null
			: new RemovalView(
				removal.Removed,
				removal.PlayerObjectId,
				removal.LocationId,
				removal.RemovedPassedPlayer,
				removal.PassedPlayerSyncPlan,
				removal.ActivePortal);
		return CreateReportAsync(removalView, isMasterController, onlinePlayers, clock, cancellationToken);
	}

	public Task<VortexRemovalRiftEntryUpdateReport> CreateReportAsync(
		VortexDefenderRemovalResult? removal,
		bool isMasterController,
		IReadOnlyList<VortexRiftEntryUpdateOnlinePlayerSnapshot> onlinePlayers,
		Func<DateTimeOffset>? clock = null,
		CancellationToken cancellationToken = default)
	{
		var removalView = removal == null
			? null
			: new RemovalView(
				removal.Removed,
				removal.PlayerObjectId,
				removal.LocationId,
				removal.RemovedPassedPlayer,
				removal.PassedPlayerSyncPlan,
				removal.ActivePortal);
		return CreateReportAsync(removalView, isMasterController, onlinePlayers, clock, cancellationToken);
	}

	private async Task<VortexRemovalRiftEntryUpdateReport> CreateReportAsync(
		RemovalView? removal,
		bool isMasterController,
		IReadOnlyList<VortexRiftEntryUpdateOnlinePlayerSnapshot> onlinePlayers,
		Func<DateTimeOffset>? clock,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(onlinePlayers);

		if (removal == null)
			return CreateResult(VortexRemovalRiftEntryUpdateReportStatus.MissingRemoval, null, null, null, null, onlinePlayers);

		if (!removal.Removed)
			return CreateResult(VortexRemovalRiftEntryUpdateReportStatus.NoRemoval, removal, null, null, null, onlinePlayers);

		if (removal.SyncPlan == null)
			return CreateResult(VortexRemovalRiftEntryUpdateReportStatus.MissingSyncPlan, removal, null, null, null, onlinePlayers);

		var pipelinePlan = VortexRiftEntryUpdatePipelinePlanService.CreatePlan(
			removal.SyncPlan,
			removal.ActivePortal,
			isMasterController,
			onlinePlayers,
			clock);
		var bridgeResult = await _dispatchBridge.DispatchAsync(pipelinePlan.CompositionPlan, cancellationToken);
		return CreateResult(
			ResolveStatus(pipelinePlan, bridgeResult),
			removal,
			pipelinePlan,
			bridgeResult,
			pipelinePlan.TargetPlayerObjectIds,
			onlinePlayers);
	}

	private static VortexRemovalRiftEntryUpdateReportStatus ResolveStatus(
		VortexRiftEntryUpdatePipelinePlan pipelinePlan,
		VortexRiftEntryUpdateCompositionDispatchBridgeResult bridgeResult)
	{
		if (pipelinePlan.Status == VortexRiftEntryUpdatePipelinePlanStatus.MissingPortal)
			return VortexRemovalRiftEntryUpdateReportStatus.MissingActivePortal;

		if (!pipelinePlan.ReadyForBridge)
			return VortexRemovalRiftEntryUpdateReportStatus.NotReady;

		if (bridgeResult.Status == VortexRiftEntryUpdateCompositionDispatchBridgeStatus.Delegated)
			return VortexRemovalRiftEntryUpdateReportStatus.Delegated;

		return VortexRemovalRiftEntryUpdateReportStatus.ReadyNoDispatch;
	}

	private static VortexRemovalRiftEntryUpdateReport CreateResult(
		VortexRemovalRiftEntryUpdateReportStatus status,
		RemovalView? removal,
		VortexRiftEntryUpdatePipelinePlan? pipelinePlan,
		VortexRiftEntryUpdateCompositionDispatchBridgeResult? bridgeResult,
		IReadOnlyList<int>? targetPlayerObjectIds,
		IReadOnlyList<VortexRiftEntryUpdateOnlinePlayerSnapshot> onlinePlayers)
	{
		return new VortexRemovalRiftEntryUpdateReport(
			status,
			removal?.LocationId ?? 0,
			removal?.PlayerObjectId ?? 0,
			removal?.RemovedPassedPlayer ?? false,
			removal?.SyncPlan,
			removal?.ActivePortal,
			pipelinePlan,
			bridgeResult,
			onlinePlayers,
			pipelinePlan?.WorldIds ?? [],
			targetPlayerObjectIds ?? [],
			pipelinePlan?.ReadyForBridge ?? false,
			bridgeResult?.DidCallDispatch ?? false,
			bridgeResult?.SendsPackets ?? false,
			"services/vortex/Invasion.kickPlayer -> controllers/RVController.syncPassed(true) -> RiftInformer.sendRiftInfo -> PacketSendUtility.sendPacket");
	}

	private sealed record RemovalView(
		bool Removed,
		int PlayerObjectId,
		int LocationId,
		bool RemovedPassedPlayer,
		VortexPassedPlayerSyncPlan? SyncPlan,
		RiftPortalState? ActivePortal);
}
