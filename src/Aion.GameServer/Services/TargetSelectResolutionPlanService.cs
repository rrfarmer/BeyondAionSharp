using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public static class TargetSelectResolutionPlanService
{
	public static TargetSelectResolutionPlan CreatePlan(TargetSelectResolutionInput input)
	{
		// Java parity breadcrumb: CM_TARGET_SELECT.runImpl resolves the requested target
		// before calling player.setTarget(newTarget). This plan keeps that resolution
		// non-live until KnownList object references and team member objects are ported.
		if (input.SelectTargetOfTarget)
			return ResolveTargetOfTarget(input);

		if (input.RequestedTargetObjectId == 0)
			return TargetSelectResolutionPlan.SetTarget(TargetSelectResolutionStatus.ClearedTarget, input, resolvedTarget: null);

		if (input.RequestedTargetObjectId == input.PlayerObjectId)
			return TargetSelectResolutionPlan.SetTarget(
				TargetSelectResolutionStatus.SelectedSelf,
				input,
				TargetSelectedSnapshot.VisibleObject(input.PlayerObjectId));

		if (input.KnownTargetObjectId == input.RequestedTargetObjectId)
		{
			if (!input.KnownTargetSeenByPlayer)
				return TargetSelectResolutionPlan.SetTarget(
					TargetSelectResolutionStatus.InvisibleKnownTargetAuditedAndCleared,
					input,
					resolvedTarget: null,
					auditMessage: "possibly used radar hack: trying to target invisible known object");

			return TargetSelectResolutionPlan.SetTarget(
				TargetSelectResolutionStatus.SelectedKnownObject,
				input,
				TargetSelectedSnapshot.VisibleObject(input.RequestedTargetObjectId));
		}

		if (input.TeamMemberObjectId == input.RequestedTargetObjectId)
			return TargetSelectResolutionPlan.SetTarget(
				TargetSelectResolutionStatus.SelectedTeamMember,
				input,
				TargetSelectedSnapshot.VisibleObject(input.RequestedTargetObjectId));

		return TargetSelectResolutionPlan.SetTarget(TargetSelectResolutionStatus.UnknownTargetCleared, input, resolvedTarget: null);
	}

	private static TargetSelectResolutionPlan ResolveTargetOfTarget(TargetSelectResolutionInput input)
	{
		if (input.CurrentTargetObjectId == 0)
			return TargetSelectResolutionPlan.ReturnedEarly(
				TargetSelectResolutionStatus.AssistNoCurrentTarget,
				input,
				TargetSelectSystemMessage.AssistThisIsAssistKey);

		if (input.TargetOfTargetObjectId == 0)
			return TargetSelectResolutionPlan.ReturnedEarly(
				TargetSelectResolutionStatus.AssistNoTargetOfTarget,
				input,
				TargetSelectSystemMessage.AssistNoUser);

		if (input.TargetOfTargetObjectId != input.PlayerObjectId && !input.TargetOfTargetSeenByPlayer)
		{
			return TargetSelectResolutionPlan.ReturnedEarly(
				input.TargetOfTargetKnownByPlayer
					? TargetSelectResolutionStatus.AssistTargetNotVisible
					: TargetSelectResolutionStatus.AssistTargetTooFar,
				input,
				input.TargetOfTargetKnownByPlayer
					? TargetSelectSystemMessage.AssistNoUser
					: TargetSelectSystemMessage.AssistTooFar);
		}

		return TargetSelectResolutionPlan.SetTarget(
			TargetSelectResolutionStatus.SelectedTargetOfTarget,
			input,
			TargetSelectedSnapshot.VisibleObject(input.TargetOfTargetObjectId));
	}
}

public sealed record TargetSelectResolutionInput(
	int PlayerObjectId,
	int RequestedTargetObjectId,
	bool SelectTargetOfTarget,
	int CurrentTargetObjectId = 0,
	int TargetOfTargetObjectId = 0,
	bool TargetOfTargetKnownByPlayer = false,
	bool TargetOfTargetSeenByPlayer = false,
	int KnownTargetObjectId = 0,
	bool KnownTargetSeenByPlayer = false,
	int TeamMemberObjectId = 0);

public sealed record TargetSelectResolutionPlan(
	TargetSelectResolutionStatus Status,
	int PlayerObjectId,
	int RequestedTargetObjectId,
	bool SelectTargetOfTarget,
	TargetSelectedSnapshot? ResolvedTarget,
	bool ShouldCallSetTarget,
	TargetSelectSystemMessage SystemMessage,
	string? AuditMessage,
	string JavaSource)
{
	public int ResolvedTargetObjectId => ResolvedTarget?.TargetObjectId ?? 0;

	public static TargetSelectResolutionPlan SetTarget(
		TargetSelectResolutionStatus status,
		TargetSelectResolutionInput input,
		TargetSelectedSnapshot? resolvedTarget,
		string? auditMessage = null)
	{
		return new TargetSelectResolutionPlan(
			status,
			input.PlayerObjectId,
			input.RequestedTargetObjectId,
			input.SelectTargetOfTarget,
			resolvedTarget,
			ShouldCallSetTarget: true,
			TargetSelectSystemMessage.None,
			auditMessage,
			"CM_TARGET_SELECT.runImpl resolved newTarget then calls player.setTarget(newTarget)");
	}

	public static TargetSelectResolutionPlan ReturnedEarly(
		TargetSelectResolutionStatus status,
		TargetSelectResolutionInput input,
		TargetSelectSystemMessage systemMessage)
	{
		return new TargetSelectResolutionPlan(
			status,
			input.PlayerObjectId,
			input.RequestedTargetObjectId,
			input.SelectTargetOfTarget,
			ResolvedTarget: null,
			ShouldCallSetTarget: false,
			systemMessage,
			AuditMessage: null,
			"CM_TARGET_SELECT.runImpl sent assist-key system message and returned before player.setTarget");
	}
}

public enum TargetSelectResolutionStatus
{
	ClearedTarget,
	SelectedSelf,
	SelectedKnownObject,
	SelectedTeamMember,
	SelectedTargetOfTarget,
	UnknownTargetCleared,
	InvisibleKnownTargetAuditedAndCleared,
	AssistNoCurrentTarget,
	AssistNoTargetOfTarget,
	AssistTargetNotVisible,
	AssistTargetTooFar,
}

public enum TargetSelectSystemMessage
{
	None,
	AssistThisIsAssistKey,
	AssistNoUser,
	AssistTooFar,
}
