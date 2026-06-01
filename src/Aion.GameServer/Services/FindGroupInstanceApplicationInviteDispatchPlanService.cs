using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class FindGroupInstanceApplicationInviteDispatchPlanService(
	PlayerGroupInviteRequestService? groupInviteRequestService = null,
	PlayerAllianceInviteRequestService? allianceInviteRequestService = null)
{
	private readonly PlayerGroupInviteRequestService _groupInviteRequestService = groupInviteRequestService ?? new PlayerGroupInviteRequestService();
	private readonly PlayerAllianceInviteRequestService _allianceInviteRequestService = allianceInviteRequestService ?? new PlayerAllianceInviteRequestService();

	public FindGroupInstanceApplicationInviteDispatchPlan CreateDisabledPlan(
		FindGroupInstanceInviteIntent? inviteIntent,
		Func<int, Player?> resolvePlayer,
		PlayerGroupRuntime groupRuntime,
		PlayerAllianceRuntime allianceRuntime)
	{
		// Java parity: FindGroupService.sendInstanceApplicationResult accepts by invoking
		// PlayerGroupService.inviteToGroup or PlayerAllianceService.inviteToAlliance. This
		// connection-adjacent executor composes those request-service side effects without
		// sending packets or marking CM_FIND_GROUP live dispatch ready.
		if (inviteIntent is null)
			return FindGroupInstanceApplicationInviteDispatchPlan.SkippedMissingIntent();

		var inviter = resolvePlayer(inviteIntent.InviterObjectId);
		var invited = resolvePlayer(inviteIntent.InvitedObjectId);
		if (inviter is null || invited is null)
		{
			return FindGroupInstanceApplicationInviteDispatchPlan.SkippedMissingPlayer(
				inviteIntent,
				inviter is null,
				invited is null);
		}

		return inviteIntent.Kind switch
		{
			FindGroupInstanceInviteKind.Group => FindGroupInstanceApplicationInviteDispatchPlan.GroupInvite(
				inviteIntent,
				_groupInviteRequestService.SendInvite(inviter, invited)),
			FindGroupInstanceInviteKind.Alliance => FindGroupInstanceApplicationInviteDispatchPlan.AllianceInvite(
				inviteIntent,
				_allianceInviteRequestService.SendInvite(inviter, invited, groupRuntime, allianceRuntime, resolvePlayer)),
			_ => FindGroupInstanceApplicationInviteDispatchPlan.SkippedUnsupportedKind(inviteIntent),
		};
	}
}

public enum FindGroupInstanceApplicationInviteDispatchStatus
{
	SkippedMissingIntent,
	SkippedMissingPlayer,
	SkippedUnsupportedKind,
	GroupInvitePlanned,
	AllianceInvitePlanned,
}

public sealed record FindGroupInstanceApplicationInviteDispatchPlan(
	FindGroupInstanceApplicationInviteDispatchStatus Status,
	FindGroupInstanceInviteIntent? InviteIntent,
	GroupInviteRequestResult? GroupInviteRequest,
	AllianceInviteRequestResult? AllianceInviteRequest,
	bool MissingInviter,
	bool MissingInvited,
	bool DispatchLiveSideEffects)
{
	public static FindGroupInstanceApplicationInviteDispatchPlan SkippedMissingIntent()
	{
		return new FindGroupInstanceApplicationInviteDispatchPlan(
			FindGroupInstanceApplicationInviteDispatchStatus.SkippedMissingIntent,
			InviteIntent: null,
			GroupInviteRequest: null,
			AllianceInviteRequest: null,
			MissingInviter: false,
			MissingInvited: false,
			DispatchLiveSideEffects: false);
	}

	public static FindGroupInstanceApplicationInviteDispatchPlan SkippedMissingPlayer(
		FindGroupInstanceInviteIntent inviteIntent,
		bool missingInviter,
		bool missingInvited)
	{
		return new FindGroupInstanceApplicationInviteDispatchPlan(
			FindGroupInstanceApplicationInviteDispatchStatus.SkippedMissingPlayer,
			inviteIntent,
			GroupInviteRequest: null,
			AllianceInviteRequest: null,
			missingInviter,
			missingInvited,
			DispatchLiveSideEffects: false);
	}

	public static FindGroupInstanceApplicationInviteDispatchPlan SkippedUnsupportedKind(FindGroupInstanceInviteIntent inviteIntent)
	{
		return new FindGroupInstanceApplicationInviteDispatchPlan(
			FindGroupInstanceApplicationInviteDispatchStatus.SkippedUnsupportedKind,
			inviteIntent,
			GroupInviteRequest: null,
			AllianceInviteRequest: null,
			MissingInviter: false,
			MissingInvited: false,
			DispatchLiveSideEffects: false);
	}

	public static FindGroupInstanceApplicationInviteDispatchPlan GroupInvite(
		FindGroupInstanceInviteIntent inviteIntent,
		GroupInviteRequestResult request)
	{
		return new FindGroupInstanceApplicationInviteDispatchPlan(
			FindGroupInstanceApplicationInviteDispatchStatus.GroupInvitePlanned,
			inviteIntent,
			request,
			AllianceInviteRequest: null,
			MissingInviter: false,
			MissingInvited: false,
			DispatchLiveSideEffects: false);
	}

	public static FindGroupInstanceApplicationInviteDispatchPlan AllianceInvite(
		FindGroupInstanceInviteIntent inviteIntent,
		AllianceInviteRequestResult request)
	{
		return new FindGroupInstanceApplicationInviteDispatchPlan(
			FindGroupInstanceApplicationInviteDispatchStatus.AllianceInvitePlanned,
			inviteIntent,
			GroupInviteRequest: null,
			request,
			MissingInviter: false,
			MissingInvited: false,
			DispatchLiveSideEffects: false);
	}
}
