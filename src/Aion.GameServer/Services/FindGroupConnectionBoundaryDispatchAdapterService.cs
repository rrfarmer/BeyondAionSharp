using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class FindGroupConnectionBoundaryDispatchAdapterService(
	FindGroupInstanceApplicationInviteDispatchPlanService? inviteDispatchPlanService = null)
{
	private readonly FindGroupInstanceApplicationInviteDispatchPlanService _inviteDispatchPlanService =
		inviteDispatchPlanService ?? new FindGroupInstanceApplicationInviteDispatchPlanService();

	public FindGroupConnectionBoundaryDispatchAdapterPlan CreateDisabledPlan(
		FindGroupConnectionClientActionCompositionPlan compositionPlan,
		Func<int, Player?>? resolvePlayer = null,
		PlayerGroupRuntime? groupRuntime = null,
		PlayerAllianceRuntime? allianceRuntime = null)
	{
		// Java parity: this is a non-live adapter for CM_FIND_GROUP.runImpl -> FindGroupService.
		// It intentionally stops before GameServerConnection sends packets or mutates live
		// connection side effects. The result is a boundary-ready shape for future wiring review.
		var intentPlan = FindGroupConnectionBoundarySideEffectCompositionEvidenceService.CreateIntentPlan(compositionPlan);

		if (compositionPlan.Status == FindGroupConnectionClientActionCompositionStatus.SkippedMissingActivePlayer)
			return FindGroupConnectionBoundaryDispatchAdapterPlan.SkippedMissingActivePlayer(intentPlan);

		if (intentPlan.ClientActionKind == FindGroupClientActionPlanKind.ParsedButNoRunImpl)
			return FindGroupConnectionBoundaryDispatchAdapterPlan.ParsedButNoRunImpl(intentPlan);

		if (intentPlan.InviteIntent != null)
		{
			if (resolvePlayer == null || groupRuntime == null || allianceRuntime == null)
				return FindGroupConnectionBoundaryDispatchAdapterPlan.BlockedMissingInviteRuntime(intentPlan);

			var invitePlan = _inviteDispatchPlanService.CreateDisabledPlan(
				intentPlan.InviteIntent,
				resolvePlayer,
				groupRuntime,
				allianceRuntime);
			return FindGroupConnectionBoundaryDispatchAdapterPlan.Composed(intentPlan, invitePlan);
		}

		if (intentPlan.DirectPacketIntents.Count == 0 && intentPlan.WorldBroadcastIntents.Count == 0)
			return FindGroupConnectionBoundaryDispatchAdapterPlan.NoSideEffects(intentPlan);

		return FindGroupConnectionBoundaryDispatchAdapterPlan.Composed(intentPlan, invitePlan: null);
	}
}

public enum FindGroupConnectionBoundaryDispatchAdapterStatus
{
	ComposedDisabledSideEffects,
	NoSideEffects,
	ParsedButNoJavaRunImpl,
	SkippedMissingActivePlayer,
	BlockedMissingInviteRuntime,
}

public sealed record FindGroupConnectionBoundaryDispatchAdapterPlan(
	FindGroupConnectionBoundaryDispatchAdapterStatus Status,
	FindGroupConnectionBoundarySideEffectIntentPlan IntentPlan,
	FindGroupInstanceApplicationInviteDispatchPlan? InvitePlan,
	bool ShouldDispatchLiveSideEffects,
	bool IsCmFindGroupBoundaryWired,
	string BoundaryNote)
{
	public static FindGroupConnectionBoundaryDispatchAdapterPlan Composed(
		FindGroupConnectionBoundarySideEffectIntentPlan intentPlan,
		FindGroupInstanceApplicationInviteDispatchPlan? invitePlan)
	{
		return new FindGroupConnectionBoundaryDispatchAdapterPlan(
			FindGroupConnectionBoundaryDispatchAdapterStatus.ComposedDisabledSideEffects,
			intentPlan,
			invitePlan,
			ShouldDispatchLiveSideEffects: false,
			IsCmFindGroupBoundaryWired: false,
			"Disabled CM_FIND_GROUP adapter composed side-effect intents without invoking GameServerConnection.");
	}

	public static FindGroupConnectionBoundaryDispatchAdapterPlan NoSideEffects(
		FindGroupConnectionBoundarySideEffectIntentPlan intentPlan)
	{
		return new FindGroupConnectionBoundaryDispatchAdapterPlan(
			FindGroupConnectionBoundaryDispatchAdapterStatus.NoSideEffects,
			intentPlan,
			InvitePlan: null,
			ShouldDispatchLiveSideEffects: false,
			IsCmFindGroupBoundaryWired: false,
			"Disabled CM_FIND_GROUP adapter matched a Java branch with no packet side effects.");
	}

	public static FindGroupConnectionBoundaryDispatchAdapterPlan ParsedButNoRunImpl(
		FindGroupConnectionBoundarySideEffectIntentPlan intentPlan)
	{
		return new FindGroupConnectionBoundaryDispatchAdapterPlan(
			FindGroupConnectionBoundaryDispatchAdapterStatus.ParsedButNoJavaRunImpl,
			intentPlan,
			InvitePlan: null,
			ShouldDispatchLiveSideEffects: false,
			IsCmFindGroupBoundaryWired: false,
			"CM_FIND_GROUP.readImpl parses this action, but Java runImpl has no branch.");
	}

	public static FindGroupConnectionBoundaryDispatchAdapterPlan SkippedMissingActivePlayer(
		FindGroupConnectionBoundarySideEffectIntentPlan intentPlan)
	{
		return new FindGroupConnectionBoundaryDispatchAdapterPlan(
			FindGroupConnectionBoundaryDispatchAdapterStatus.SkippedMissingActivePlayer,
			intentPlan,
			InvitePlan: null,
			ShouldDispatchLiveSideEffects: false,
			IsCmFindGroupBoundaryWired: false,
			"CM_FIND_GROUP composition skipped because the connection had no active player.");
	}

	public static FindGroupConnectionBoundaryDispatchAdapterPlan BlockedMissingInviteRuntime(
		FindGroupConnectionBoundarySideEffectIntentPlan intentPlan)
	{
		return new FindGroupConnectionBoundaryDispatchAdapterPlan(
			FindGroupConnectionBoundaryDispatchAdapterStatus.BlockedMissingInviteRuntime,
			intentPlan,
			InvitePlan: null,
			ShouldDispatchLiveSideEffects: false,
			IsCmFindGroupBoundaryWired: false,
			"Action 12 invite intent requires player resolver plus group/alliance runtimes before a disabled invite plan can be composed.");
	}
}
