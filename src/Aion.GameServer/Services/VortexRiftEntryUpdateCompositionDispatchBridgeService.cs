using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public enum VortexRiftEntryUpdateCompositionDispatchBridgeStatus
{
	MissingComposition,
	NotReady,
	DisabledNoDispatch,
	Delegated,
}

public sealed record VortexRiftEntryUpdateCompositionDispatchBridgeResult(
	VortexRiftEntryUpdateCompositionDispatchBridgeStatus Status,
	VortexRiftEntryUpdateCompositionPlan? CompositionPlan,
	VortexPassedPlayerSyncRiftEntryUpdateDispatchResult? DispatchResult,
	IReadOnlyList<int> TargetPlayerObjectIds,
	bool DidCallDispatch,
	bool SendsPackets,
	bool IsEnabled,
	string JavaSource);

public sealed class VortexRiftEntryUpdateCompositionDispatchBridgeService
{
	private readonly VortexPassedPlayerSyncRiftEntryUpdateDispatchService _dispatchService;
	private readonly bool _enabled;

	public VortexRiftEntryUpdateCompositionDispatchBridgeService(
		IGameClientConnectionRegistry? connectionRegistry = null,
		bool enabled = false,
		VortexPassedPlayerSyncRiftEntryUpdateDispatchService? dispatchService = null)
	{
		_enabled = enabled;
		_dispatchService = dispatchService ?? new VortexPassedPlayerSyncRiftEntryUpdateDispatchService(
			connectionRegistry,
			enabled);
	}

	public async Task<VortexRiftEntryUpdateCompositionDispatchBridgeResult> DispatchAsync(
		VortexRiftEntryUpdateCompositionPlan? compositionPlan,
		CancellationToken cancellationToken = default)
	{
		if (compositionPlan == null)
		{
			return CreateResult(
				VortexRiftEntryUpdateCompositionDispatchBridgeStatus.MissingComposition,
				null,
				dispatchResult: null,
				[],
				didCallDispatch: false,
				sendsPackets: false);
		}

		if (!compositionPlan.ReadyForDispatch || compositionPlan.EntryUpdate == null)
		{
			return CreateResult(
				VortexRiftEntryUpdateCompositionDispatchBridgeStatus.NotReady,
				compositionPlan,
				dispatchResult: null,
				compositionPlan.TargetPlayerObjectIds,
				didCallDispatch: false,
				sendsPackets: false);
		}

		if (!_enabled)
		{
			return CreateResult(
				VortexRiftEntryUpdateCompositionDispatchBridgeStatus.DisabledNoDispatch,
				compositionPlan,
				dispatchResult: null,
				compositionPlan.TargetPlayerObjectIds,
				didCallDispatch: false,
				sendsPackets: false);
		}

		var dispatchResult = await _dispatchService.DispatchAsync(
			compositionPlan.EntryUpdate,
			compositionPlan.TargetPlayerObjectIds,
			cancellationToken);
		return CreateResult(
			VortexRiftEntryUpdateCompositionDispatchBridgeStatus.Delegated,
			compositionPlan,
			dispatchResult,
			compositionPlan.TargetPlayerObjectIds,
			didCallDispatch: true,
			dispatchResult.SendsPackets);
	}

	private VortexRiftEntryUpdateCompositionDispatchBridgeResult CreateResult(
		VortexRiftEntryUpdateCompositionDispatchBridgeStatus status,
		VortexRiftEntryUpdateCompositionPlan? compositionPlan,
		VortexPassedPlayerSyncRiftEntryUpdateDispatchResult? dispatchResult,
		IReadOnlyList<int> targetPlayerObjectIds,
		bool didCallDispatch,
		bool sendsPackets)
	{
		return new VortexRiftEntryUpdateCompositionDispatchBridgeResult(
			status,
			compositionPlan,
			dispatchResult,
			targetPlayerObjectIds,
			didCallDispatch,
			sendsPackets,
			_enabled,
			"controllers/RVController.syncPassed(true) -> RiftInformer.sendRiftInfo -> PacketSendUtility.sendPacket(player, SM_RIFT_ANNOUNCE)");
	}
}
