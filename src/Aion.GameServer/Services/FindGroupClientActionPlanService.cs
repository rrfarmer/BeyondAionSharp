using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Services;

public sealed class FindGroupClientActionPlanService
{
	private readonly FindGroupRecruitmentPlanService _findGroupService;

	public FindGroupClientActionPlanService(FindGroupRecruitmentPlanService findGroupService)
	{
		_findGroupService = findGroupService;
	}

	public FindGroupClientActionPlan Plan(
		Player player,
		FindGroupClientAction action,
		int nowEpochSeconds,
		Func<int, Player?>? resolvePlayer = null,
		FindGroupRecruitmentSubject? currentTeam = null,
		IReadOnlyList<FindGroupInstanceGroupMemberState>? currentMembers = null)
	{
		// Java parity: network/aion/clientpackets/CM_FIND_GROUP.runImpl. This planner only
		// composes disabled plan calls; live dispatch remains intentionally unimplemented.
		return action.Action switch
		{
			0 => FindGroupClientActionPlan.ForRecruitmentShow(
				action.Action,
				_findGroupService.ShowRecruitments(player.Race, nowEpochSeconds)),
			1 => FindGroupClientActionPlan.ForRecruitmentMutation(
				action.Action,
				FindGroupClientActionPlanKind.RemoveRecruitment,
				_findGroupService.RemoveRecruitment(
					player,
					action.ServerId,
					action.Unknown1,
					action.Unknown2,
					action.Unknown3)),
			2 => FindGroupClientActionPlan.ForRecruitmentMutation(
				action.Action,
				FindGroupClientActionPlanKind.AddRecruitment,
				_findGroupService.AddRecruitment(
					player,
					action.Message ?? string.Empty,
					action.GroupType,
					nowEpochSeconds,
					currentTeam)),
			3 => FindGroupClientActionPlan.ForRecruitmentMutation(
				action.Action,
				FindGroupClientActionPlanKind.UpdateRecruitment,
				_findGroupService.UpdateRecruitment(
					player,
					action.Message ?? string.Empty,
					action.GroupType,
					nowEpochSeconds)),
			4 => FindGroupClientActionPlan.ForApplicationShow(
				action.Action,
				_findGroupService.ShowApplications(player.Race, nowEpochSeconds)),
			5 => FindGroupClientActionPlan.ForApplicationMutation(
				action.Action,
				FindGroupClientActionPlanKind.RemoveApplication,
				_findGroupService.RemoveApplication(player)),
			6 => FindGroupClientActionPlan.ForApplicationMutation(
				action.Action,
				FindGroupClientActionPlanKind.AddApplication,
				_findGroupService.AddApplication(
					player,
					action.Message ?? string.Empty,
					action.GroupType,
					action.ClassId,
					action.Level,
					nowEpochSeconds)),
			7 => FindGroupClientActionPlan.ForApplicationMutation(
				action.Action,
				FindGroupClientActionPlanKind.UpdateApplication,
				_findGroupService.UpdateApplication(
					player,
					action.Message ?? string.Empty,
					action.GroupType,
					action.ClassId,
					action.Level,
					nowEpochSeconds)),
			8 => FindGroupClientActionPlan.ForInstanceGroupMutation(
				action.Action,
				FindGroupClientActionPlanKind.RegisterInstanceGroup,
				_findGroupService.RegisterInstanceGroup(
					player,
					action.InstanceMaskId,
					action.Message ?? string.Empty,
					action.MinMembers,
					nowEpochSeconds,
					currentMembers)),
			9 => FindGroupClientActionPlan.ForInstanceGroupMutation(
				action.Action,
				FindGroupClientActionPlanKind.RemoveInstanceGroup,
				_findGroupService.RemoveInstanceGroup(player, nowEpochSeconds)),
			10 => FindGroupClientActionPlan.ForInstanceGroupShow(
				action.Action,
				FindGroupClientActionPlanKind.ShowInstanceGroups,
				_findGroupService.ShowInstanceGroups(player.Race, nowEpochSeconds)),
			11 => FindGroupClientActionPlan.ForInstanceApplication(
				action.Action,
				FindGroupClientActionPlanKind.SendInstanceApplication,
				_findGroupService.SendInstanceApplication(player, resolvePlayer?.Invoke(action.PlayerOrTeamId))),
			12 => FindGroupClientActionPlan.ForInstanceApplication(
				action.Action,
				FindGroupClientActionPlanKind.SendInstanceApplicationResult,
				_findGroupService.SendInstanceApplicationResult(
					player,
					resolvePlayer?.Invoke(action.PlayerOrTeamId),
					action.PlayerOrTeamId,
					action.InstanceApplicationReply)),
			13 => FindGroupClientActionPlan.ForInstanceGroupShow(
				action.Action,
				FindGroupClientActionPlanKind.ShowInstanceGroupsUpdate,
				_findGroupService.ShowInstanceGroups(player.Race, nowEpochSeconds)),
			15 => FindGroupClientActionPlan.ForInstanceGroupMemberInfo(
				action.Action,
				_findGroupService.ShowInstanceGroupMembersInfo(
					player,
					action.PlayerOrTeamId,
					nowEpochSeconds,
					currentMembers)),
			17 => FindGroupClientActionPlan.ForInstanceGroupMutation(
				action.Action,
				FindGroupClientActionPlanKind.UpdateInstanceGroup,
				_findGroupService.UpdateInstanceGroup(
					player,
					action.Message ?? string.Empty,
					nowEpochSeconds,
					currentMembers)),
			20 or 25 => FindGroupClientActionPlan.NoRunImpl(action.Action),
			_ => FindGroupClientActionPlan.Unknown(action.Action),
		};
	}
}

public sealed record FindGroupClientAction(
	int Action,
	int PlayerOrTeamId = 0,
	int BannedPlayerId = 0,
	string? Message = null,
	int GroupType = 0,
	int ClassId = 0,
	int Level = 0,
	byte ServerId = 0,
	byte Unknown1 = 0,
	byte Unknown2 = 0,
	byte Unknown3 = 0,
	int InstanceMaskId = 0,
	int MinMembers = 0,
	byte InstanceApplicationReply = 0)
{
	public static FindGroupClientAction FromPacket(CmFindGroup packet)
	{
		return new FindGroupClientAction(
			packet.Action,
			packet.PlayerOrTeamId,
			packet.BannedPlayerId,
			packet.Message,
			packet.GroupType,
			packet.ClassId,
			packet.Level,
			packet.ServerId,
			packet.Unknown1,
			packet.Unknown2,
			packet.Unknown3,
			packet.InstanceMaskId,
			packet.MinMembers,
			packet.InstanceApplicationReply);
	}
}

public enum FindGroupClientActionPlanKind
{
	ShowRecruitments,
	RemoveRecruitment,
	AddRecruitment,
	UpdateRecruitment,
	ShowApplications,
	RemoveApplication,
	AddApplication,
	UpdateApplication,
	RegisterInstanceGroup,
	RemoveInstanceGroup,
	ShowInstanceGroups,
	SendInstanceApplication,
	SendInstanceApplicationResult,
	ShowInstanceGroupsUpdate,
	ShowInstanceGroupMembersInfo,
	UpdateInstanceGroup,
	ParsedButNoRunImpl,
	UnknownAction,
}

public sealed record FindGroupClientActionPlan(
	int Action,
	FindGroupClientActionPlanKind Kind,
	FindGroupRecruitmentMutationPlan? RecruitmentMutationPlan,
	FindGroupRecruitmentShowPlan? RecruitmentShowPlan,
	FindGroupApplicationMutationPlan? ApplicationMutationPlan,
	FindGroupApplicationShowPlan? ApplicationShowPlan,
	FindGroupInstanceGroupMutationPlan? InstanceGroupMutationPlan,
	FindGroupInstanceGroupShowPlan? InstanceGroupShowPlan,
	FindGroupInstanceGroupMemberInfoPlan? InstanceGroupMemberInfoPlan,
	FindGroupInstanceApplicationPlan? InstanceApplicationPlan,
	bool DispatchLiveSideEffects)
{
	public static FindGroupClientActionPlan ForRecruitmentMutation(
		int action,
		FindGroupClientActionPlanKind kind,
		FindGroupRecruitmentMutationPlan plan)
	{
		return new FindGroupClientActionPlan(
			action,
			kind,
			plan,
			RecruitmentShowPlan: null,
			ApplicationMutationPlan: null,
			ApplicationShowPlan: null,
			InstanceGroupMutationPlan: null,
			InstanceGroupShowPlan: null,
			InstanceGroupMemberInfoPlan: null,
			InstanceApplicationPlan: null,
			DispatchLiveSideEffects: false);
	}

	public static FindGroupClientActionPlan ForRecruitmentShow(int action, FindGroupRecruitmentShowPlan plan)
	{
		return Empty(action, FindGroupClientActionPlanKind.ShowRecruitments) with { RecruitmentShowPlan = plan };
	}

	public static FindGroupClientActionPlan ForApplicationMutation(
		int action,
		FindGroupClientActionPlanKind kind,
		FindGroupApplicationMutationPlan plan)
	{
		return Empty(action, kind) with { ApplicationMutationPlan = plan };
	}

	public static FindGroupClientActionPlan ForApplicationShow(int action, FindGroupApplicationShowPlan plan)
	{
		return Empty(action, FindGroupClientActionPlanKind.ShowApplications) with { ApplicationShowPlan = plan };
	}

	public static FindGroupClientActionPlan ForInstanceGroupMutation(
		int action,
		FindGroupClientActionPlanKind kind,
		FindGroupInstanceGroupMutationPlan plan)
	{
		return Empty(action, kind) with { InstanceGroupMutationPlan = plan };
	}

	public static FindGroupClientActionPlan ForInstanceGroupShow(
		int action,
		FindGroupClientActionPlanKind kind,
		FindGroupInstanceGroupShowPlan plan)
	{
		return Empty(action, kind) with { InstanceGroupShowPlan = plan };
	}

	public static FindGroupClientActionPlan ForInstanceGroupMemberInfo(
		int action,
		FindGroupInstanceGroupMemberInfoPlan plan)
	{
		return Empty(action, FindGroupClientActionPlanKind.ShowInstanceGroupMembersInfo) with
		{
			InstanceGroupMemberInfoPlan = plan,
		};
	}

	public static FindGroupClientActionPlan ForInstanceApplication(
		int action,
		FindGroupClientActionPlanKind kind,
		FindGroupInstanceApplicationPlan plan)
	{
		return Empty(action, kind) with { InstanceApplicationPlan = plan };
	}

	public static FindGroupClientActionPlan NoRunImpl(int action)
	{
		return Empty(action, FindGroupClientActionPlanKind.ParsedButNoRunImpl);
	}

	public static FindGroupClientActionPlan Unknown(int action)
	{
		return Empty(action, FindGroupClientActionPlanKind.UnknownAction);
	}

	private static FindGroupClientActionPlan Empty(int action, FindGroupClientActionPlanKind kind)
	{
		return new FindGroupClientActionPlan(
			action,
			kind,
			RecruitmentMutationPlan: null,
			RecruitmentShowPlan: null,
			ApplicationMutationPlan: null,
			ApplicationShowPlan: null,
			InstanceGroupMutationPlan: null,
			InstanceGroupShowPlan: null,
			InstanceGroupMemberInfoPlan: null,
			InstanceApplicationPlan: null,
			DispatchLiveSideEffects: false);
	}
}
