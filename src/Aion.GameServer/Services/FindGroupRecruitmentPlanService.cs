using System.Collections.Concurrent;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

public sealed class FindGroupRecruitmentPlanService
{
	private readonly ConcurrentDictionary<int, FindGroupRecruitmentState> _recruitments = [];
	private readonly ConcurrentDictionary<int, FindGroupApplicationState> _applications = [];
	private readonly ConcurrentDictionary<int, FindGroupInstanceGroupState> _instanceGroups = [];

	public FindGroupRecruitmentMutationPlan AddRecruitment(
		Player player,
		string message,
		int groupType,
		int nowEpochSeconds,
		FindGroupRecruitmentSubject? currentTeam = null)
	{
		// Java parity: services/findgroup/FindGroupService.addRecruitment uses player.getCurrentTeam()
		// when present, otherwise the solo player, then sends STR_PARTY_MATCH_OFFER_PARTY_POSTED and
		// showRecruitments(player). This planner records those side effects without live sends.
		var subject = currentTeam ?? FindGroupRecruitmentSubject.FromSoloPlayer(player);
		var state = FindGroupRecruitmentState.FromSubject(subject, message, groupType, nowEpochSeconds);
		_recruitments[subject.ObjectId] = state;

		return new FindGroupRecruitmentMutationPlan(
			FindGroupRecruitmentPlanStatus.Added,
			state,
			RemovedRecruitment: null,
			DirectPacketIntents:
			[
				new FindGroupDirectPacketIntent(
					player.ObjectId,
					new SmSystemMessage(1400392),
					"SM_SYSTEM_MESSAGE.STR_PARTY_MATCH_OFFER_PARTY_POSTED")
			],
			WorldBroadcastIntent: null,
			ShowRecruitments(player.Race, nowEpochSeconds));
	}

	public FindGroupRecruitmentMutationPlan UpdateRecruitment(
		Player player,
		string message,
		int groupType,
		int nowEpochSeconds)
	{
		var recruitmentId = player.CurrentTeamId == 0 ? player.ObjectId : player.CurrentTeamId;
		if (!_recruitments.TryGetValue(recruitmentId, out var state))
		{
			return new FindGroupRecruitmentMutationPlan(
				FindGroupRecruitmentPlanStatus.Missing,
				CurrentRecruitment: null,
				RemovedRecruitment: null,
				DirectPacketIntents: [],
				WorldBroadcastIntent: null,
				ShowRecruitmentsPlan: null);
		}

		var updated = state with
		{
			Message = message,
			GroupType = groupType,
			LastUpdate = nowEpochSeconds,
		};
		_recruitments[recruitmentId] = updated;

		return new FindGroupRecruitmentMutationPlan(
			FindGroupRecruitmentPlanStatus.Updated,
			updated,
			RemovedRecruitment: null,
			DirectPacketIntents: [],
			WorldBroadcastIntent: null,
			ShowRecruitmentsPlan: null);
	}

	public FindGroupRecruitmentMutationPlan RemoveRecruitment(
		Player player,
		byte serverId,
		byte unknown1,
		byte unknown2,
		byte unknown3)
	{
		// Java parity: removeRecruitment(Player, ...) resolves current team id, falling back to player id.
		var recruitmentId = player.CurrentTeamId == 0 ? player.ObjectId : player.CurrentTeamId;
		return RemoveRecruitment(recruitmentId, serverId, unknown1, unknown2, unknown3);
	}

	public FindGroupRecruitmentMutationPlan RemoveRecruitment(
		int playerOrTeamId,
		byte serverId,
		byte unknown1,
		byte unknown2,
		byte unknown3)
	{
		if (!_recruitments.TryRemove(playerOrTeamId, out var removed))
		{
			return new FindGroupRecruitmentMutationPlan(
				FindGroupRecruitmentPlanStatus.Missing,
				CurrentRecruitment: null,
				RemovedRecruitment: null,
				DirectPacketIntents: [],
				WorldBroadcastIntent: null,
				ShowRecruitmentsPlan: null);
		}

		return new FindGroupRecruitmentMutationPlan(
			FindGroupRecruitmentPlanStatus.Removed,
			CurrentRecruitment: null,
			removed,
			DirectPacketIntents: [],
			new FindGroupWorldBroadcastIntent(
				removed.Race,
				SmFindGroup.RemoveRecruitment(playerOrTeamId, serverId, unknown1, unknown2, unknown3),
				"PacketSendUtility.broadcastToWorld(..., p -> p.getRace() == recruitment.getRace())"),
			ShowRecruitmentsPlan: null);
	}

	public FindGroupRecruitmentShowPlan ShowRecruitments(string playerRace, int nowEpochSeconds)
	{
		// Java parity: showRecruitments filters ConcurrentHashMap values by race and writes the current
		// server second into the SM_FIND_GROUP action 0 packet header.
		var snapshots = _recruitments.Values
			.Where(recruitment => string.Equals(recruitment.Race, playerRace, StringComparison.Ordinal))
			.Select(recruitment => recruitment.ToSnapshot())
			.ToArray();

		return new FindGroupRecruitmentShowPlan(
			playerRace,
			nowEpochSeconds,
			snapshots,
			SmFindGroup.ShowRecruitments(nowEpochSeconds, snapshots));
	}

	public FindGroupApplicationMutationPlan AddApplication(
		Player player,
		string message,
		int groupType,
		int classId,
		int level,
		int nowEpochSeconds)
	{
		// Java parity: FindGroupService.addApplication stores by player object id, sends
		// STR_PARTY_MATCH_SEEK_PARTY_POSTED, then showApplications(player). Live sends stay disabled.
		var state = FindGroupApplicationState.FromPlayer(player, message, groupType, classId, level, nowEpochSeconds);
		_applications[player.ObjectId] = state;

		return new FindGroupApplicationMutationPlan(
			FindGroupApplicationPlanStatus.Added,
			state,
			RemovedApplication: null,
			DirectPacketIntents:
			[
				new FindGroupDirectPacketIntent(
					player.ObjectId,
					new SmSystemMessage(1400393),
					"SM_SYSTEM_MESSAGE.STR_PARTY_MATCH_SEEK_PARTY_POSTED")
			],
			WorldBroadcastIntent: null,
			ShowApplications(player.Race, nowEpochSeconds));
	}

	public FindGroupApplicationMutationPlan UpdateApplication(
		Player player,
		string message,
		int groupType,
		int classId,
		int level,
		int nowEpochSeconds)
	{
		if (!_applications.TryGetValue(player.ObjectId, out var state))
		{
			return new FindGroupApplicationMutationPlan(
				FindGroupApplicationPlanStatus.Missing,
				CurrentApplication: null,
				RemovedApplication: null,
				DirectPacketIntents: [],
				WorldBroadcastIntent: null,
				ShowApplicationsPlan: null);
		}

		var updated = state with
		{
			Message = message,
			GroupType = groupType,
			ClassId = classId,
			Level = level,
			LastUpdate = nowEpochSeconds,
		};
		_applications[player.ObjectId] = updated;

		return new FindGroupApplicationMutationPlan(
			FindGroupApplicationPlanStatus.Updated,
			updated,
			RemovedApplication: null,
			DirectPacketIntents: [],
			WorldBroadcastIntent: null,
			ShowApplicationsPlan: null);
	}

	public FindGroupApplicationMutationPlan RemoveApplication(Player player)
	{
		if (!_applications.TryRemove(player.ObjectId, out var removed))
		{
			return new FindGroupApplicationMutationPlan(
				FindGroupApplicationPlanStatus.Missing,
				CurrentApplication: null,
				RemovedApplication: null,
				DirectPacketIntents: [],
				WorldBroadcastIntent: null,
				ShowApplicationsPlan: null);
		}

		return new FindGroupApplicationMutationPlan(
			FindGroupApplicationPlanStatus.Removed,
			CurrentApplication: null,
			removed,
			DirectPacketIntents: [],
			new FindGroupWorldBroadcastIntent(
				removed.Race,
				SmFindGroup.RemoveApplication(player.ObjectId),
				"PacketSendUtility.broadcastToWorld(..., p -> p.getRace() == application.getPlayer().getRace())"),
			ShowApplicationsPlan: null);
	}

	public FindGroupApplicationShowPlan ShowApplications(string playerRace, int nowEpochSeconds)
	{
		// Java parity: showApplications filters application players by race and writes the current
		// server second into the SM_FIND_GROUP action 4 packet header.
		var snapshots = _applications.Values
			.Where(application => string.Equals(application.Race, playerRace, StringComparison.Ordinal))
			.Select(application => application.ToSnapshot())
			.ToArray();

		return new FindGroupApplicationShowPlan(
			playerRace,
			nowEpochSeconds,
			snapshots,
			SmFindGroup.ShowApplications(nowEpochSeconds, snapshots));
	}

	public FindGroupInstanceGroupMutationPlan RegisterInstanceGroup(
		Player player,
		int instanceMaskId,
		string message,
		int minMembers,
		int nowEpochSeconds,
		IReadOnlyList<FindGroupInstanceGroupMemberState>? currentMembers = null)
	{
		// Java parity: FindGroupService.registerInstanceGroup stores a ServerWideGroup keyed by
		// recruiter object id, then sends SM_FIND_GROUP action 14 to the registering player.
		var state = FindGroupInstanceGroupState.FromPlayer(
			player,
			instanceMaskId,
			minMembers,
			message,
			nowEpochSeconds,
			currentMembers);
		_instanceGroups[player.ObjectId] = state;

		return new FindGroupInstanceGroupMutationPlan(
			FindGroupInstanceGroupPlanStatus.Added,
			state,
			RemovedInstanceGroup: null,
			DirectPacketIntents:
			[
				new FindGroupDirectPacketIntent(
					player.ObjectId,
					SmFindGroup.RegisterInstanceGroup([state.ToRegistrationSnapshot()]),
					"PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(14, List.of(instanceGroup)))")
			],
			ShowInstanceGroupsPlan: null);
	}

	public FindGroupInstanceGroupMutationPlan UpdateInstanceGroup(
		Player player,
		string message,
		int nowEpochSeconds,
		IReadOnlyList<FindGroupInstanceGroupMemberState>? currentMembers = null)
	{
		if (!_instanceGroups.TryGetValue(player.ObjectId, out var state))
		{
			return new FindGroupInstanceGroupMutationPlan(
				FindGroupInstanceGroupPlanStatus.Missing,
				CurrentInstanceGroup: null,
				RemovedInstanceGroup: null,
				DirectPacketIntents: [],
				ShowInstanceGroupsPlan: null);
		}

		var updated = state with
		{
			Message = message,
			LastUpdate = nowEpochSeconds,
			Members = currentMembers ?? state.Members,
		};
		_instanceGroups[player.ObjectId] = updated;

		return new FindGroupInstanceGroupMutationPlan(
			FindGroupInstanceGroupPlanStatus.Updated,
			updated,
			RemovedInstanceGroup: null,
			DirectPacketIntents: [],
			ShowInstanceGroups(player.Race, nowEpochSeconds));
	}

	public FindGroupInstanceGroupMutationPlan RemoveInstanceGroup(Player player, int nowEpochSeconds)
	{
		_instanceGroups.TryRemove(player.ObjectId, out var removed);

		return new FindGroupInstanceGroupMutationPlan(
			removed is null ? FindGroupInstanceGroupPlanStatus.Missing : FindGroupInstanceGroupPlanStatus.Removed,
			CurrentInstanceGroup: null,
			removed,
			DirectPacketIntents: [],
			ShowInstanceGroups(player.Race, nowEpochSeconds));
	}

	public FindGroupInstanceGroupShowPlan ShowInstanceGroups(string playerRace, int nowEpochSeconds)
	{
		// Java parity: showInstanceGroups filters ServerWideGroup values by recruiter race and
		// sends SM_FIND_GROUP action 10 after any optional action 26 mask-list packet.
		var snapshots = _instanceGroups.Values
			.Where(instanceGroup => string.Equals(instanceGroup.Race, playerRace, StringComparison.Ordinal))
			.Select(instanceGroup => instanceGroup.ToRegistrationSnapshot())
			.ToArray();

		return new FindGroupInstanceGroupShowPlan(
			playerRace,
			nowEpochSeconds,
			snapshots,
			SmFindGroup.ShowInstanceGroups(nowEpochSeconds, snapshots));
	}

	public FindGroupInstanceGroupClientShowPlan ShowInstanceGroupsForClient(
		Player player,
		bool isUpdate,
		bool formInstanceGroupAnywhere,
		IReadOnlyList<int>? targetNpcInstanceMaskIds,
		IReadOnlyList<int> allRecruitableInstanceMaskIds,
		int nowEpochSeconds)
	{
		// Java parity: FindGroupService.showInstanceGroups(player, isUpdate) sends action 26 only
		// when this is not an update and GroupConfig.FORM_INSTANCE_GROUP_ANYWHERE is enabled.
		FindGroupDirectPacketIntent? enableRegisterIntent = null;
		IReadOnlyList<int>? enabledInstanceMaskIds = null;
		if (!isUpdate && formInstanceGroupAnywhere)
		{
			enabledInstanceMaskIds = targetNpcInstanceMaskIds ?? allRecruitableInstanceMaskIds;
			enableRegisterIntent = new FindGroupDirectPacketIntent(
				player.ObjectId,
				SmFindGroup.EnableRegisterForInstances(enabledInstanceMaskIds),
				"PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(instanceMaskIds))");
		}

		return new FindGroupInstanceGroupClientShowPlan(
			IsUpdate: isUpdate,
			FormInstanceGroupAnywhere: formInstanceGroupAnywhere,
			EnabledInstanceMaskIds: enabledInstanceMaskIds,
			EnableRegisterForInstancesIntent: enableRegisterIntent,
			ShowInstanceGroupsPlan: ShowInstanceGroups(player.Race, nowEpochSeconds));
	}

	public FindGroupPortalInstanceGroupShowPlan ShowInstanceGroupsForPortal(
		Player player,
		IReadOnlyList<int>? portalNpcInstanceMaskIds)
	{
		// Java parity: FindGroupService.showInstanceGroups(player, portalNpc) sends only action 26,
		// and only when DataManager.AUTO_GROUP has mask ids for the portal NPC.
		var enableRegisterIntent = portalNpcInstanceMaskIds is null
			? null
			: new FindGroupDirectPacketIntent(
				player.ObjectId,
				SmFindGroup.EnableRegisterForInstances(portalNpcInstanceMaskIds),
				"PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(instanceMaskIds))");

		return new FindGroupPortalInstanceGroupShowPlan(
			portalNpcInstanceMaskIds,
			enableRegisterIntent);
	}

	public FindGroupPrepareWindowPlan ShowEnterButtonInPrepareForEntryWindow(
		Player player,
		FindGroupInstanceGroupWindowSnapshot instanceGroup)
	{
		return FindGroupPrepareWindowPlan.WithIntent(
			FindGroupPrepareWindowPlanKind.ShowEnterButton,
			new FindGroupDirectPacketIntent(
				player.ObjectId,
				SmFindGroup.ShowEnterButtonInPrepareForEntryWindow(instanceGroup),
				"PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(18, List.of(instanceGroup)))"));
	}

	public FindGroupPrepareWindowPlan ShowPrepareForEntryWindow(
		Player player,
		FindGroupInstanceGroupWindowSnapshot instanceGroup)
	{
		return FindGroupPrepareWindowPlan.WithIntent(
			FindGroupPrepareWindowPlanKind.ShowPrepareWindow,
			new FindGroupDirectPacketIntent(
				player.ObjectId,
				SmFindGroup.ShowPrepareForEntryWindow(instanceGroup),
				"PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(22, List.of(instanceGroup)))"));
	}

	public FindGroupPrepareWindowPlan DestroyPrepareForEntryWindow(
		Player player,
		FindGroupInstanceGroupWindowSnapshot instanceGroup,
		bool showEnterInstanceMessage)
	{
		return FindGroupPrepareWindowPlan.WithIntent(
			FindGroupPrepareWindowPlanKind.DestroyPrepareWindow,
			new FindGroupDirectPacketIntent(
				player.ObjectId,
				SmFindGroup.DestroyPrepareForEntryWindow(instanceGroup, showEnterInstanceMessage),
				"PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(23, List.of(instanceGroup), showEnterInstanceMessage))"));
	}

	public FindGroupPrepareWindowPlan UpdatePrepareForEntryWindow(
		Player player,
		FindGroupInstanceGroupPrepareWindowSnapshot instanceGroup)
	{
		return FindGroupPrepareWindowPlan.WithIntent(
			FindGroupPrepareWindowPlanKind.UpdatePrepareWindow,
			new FindGroupDirectPacketIntent(
				player.ObjectId,
				SmFindGroup.UpdatePrepareForEntryWindow(instanceGroup),
				"PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(24, List.of(instanceGroup)))"));
	}

	public FindGroupLogoutCleanupPlan OnLogout(Player player)
	{
		// Java parity: FindGroupService.onLogout removes entries keyed by player.getObjectId()
		// from all three maps and sends no packets.
		_recruitments.TryRemove(player.ObjectId, out var removedRecruitment);
		_applications.TryRemove(player.ObjectId, out var removedApplication);
		_instanceGroups.TryRemove(player.ObjectId, out var removedInstanceGroup);

		return new FindGroupLogoutCleanupPlan(
			player.ObjectId,
			removedRecruitment,
			removedApplication,
			removedInstanceGroup,
			DirectPacketIntents: [],
			DispatchLiveSideEffects: false,
			"recruitments.remove(player.getObjectId()); applications.remove(player.getObjectId()); instanceGroups.remove(player.getObjectId())");
	}

	public FindGroupInstanceGroupMemberInfoPlan ShowInstanceGroupMembersInfo(
		Player player,
		int playerObjectId,
		int nowEpochSeconds,
		IReadOnlyList<FindGroupInstanceGroupMemberState>? currentMembers = null)
	{
		if (!_instanceGroups.TryGetValue(playerObjectId, out var state))
		{
			return new FindGroupInstanceGroupMemberInfoPlan(
				FindGroupInstanceGroupPlanStatus.Missing,
				MemberInfo: null,
				DirectPacketIntents: []);
		}

		var snapshot = state.ToMemberInfoSnapshot(nowEpochSeconds, currentMembers);
		return new FindGroupInstanceGroupMemberInfoPlan(
			FindGroupInstanceGroupPlanStatus.Shown,
			snapshot,
			DirectPacketIntents:
			[
				new FindGroupDirectPacketIntent(
					player.ObjectId,
					SmFindGroup.ShowInstanceGroupMemberInfo(snapshot),
					"PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(16, List.of(instanceGroup)))")
			]);
	}

	public FindGroupInstanceApplicationPlan SendInstanceApplication(Player applicant, Player? recruiter)
	{
		// Java parity: FindGroupService.sendInstanceApplication resolves the target player through
		// World.getPlayer(playerOrTeamId) and sends SM_FIND_GROUP action 11 only when online.
		if (recruiter is null)
		{
			return new FindGroupInstanceApplicationPlan(
				FindGroupInstanceApplicationPlanStatus.MissingRecipient,
				DirectPacketIntents: [],
				InviteIntent: null);
		}

		return new FindGroupInstanceApplicationPlan(
			FindGroupInstanceApplicationPlanStatus.ApplicationSent,
			DirectPacketIntents:
			[
				new FindGroupDirectPacketIntent(
					recruiter.ObjectId,
					SmFindGroup.SendInstanceGroupApplicationAsWhisperChatMessage(
						new FindGroupInstanceApplicantSnapshot(
							applicant.ObjectId,
							(byte)FindGroupRecruitmentSubject.ToJavaClassId(applicant.PlayerClass),
							applicant.Level,
							applicant.Name)),
					"PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(applicant))")
			],
			InviteIntent: null);
	}

	public FindGroupInstanceApplicationPlan SendInstanceApplicationResult(
		Player responder,
		Player? applicant,
		int applicantId,
		byte instanceApplicationReply)
	{
		// Java parity: FindGroupService.sendInstanceApplicationResult resolves the applicant through
		// World.getPlayer(applicantId). Accept plans group/alliance invite; denial sends localized whisper.
		if (applicant is null)
		{
			return new FindGroupInstanceApplicationPlan(
				FindGroupInstanceApplicationPlanStatus.MissingApplicant,
				DirectPacketIntents: [],
				InviteIntent: null);
		}

		if (instanceApplicationReply == 1)
		{
			if (!_instanceGroups.TryGetValue(responder.ObjectId, out var instanceGroup))
			{
				return new FindGroupInstanceApplicationPlan(
					FindGroupInstanceApplicationPlanStatus.MissingInstanceGroup,
					DirectPacketIntents: [],
					InviteIntent: null);
			}

			var inviteKind = instanceGroup.MinMembers <= 6
				? FindGroupInstanceInviteKind.Group
				: FindGroupInstanceInviteKind.Alliance;
			return new FindGroupInstanceApplicationPlan(
				inviteKind == FindGroupInstanceInviteKind.Group
					? FindGroupInstanceApplicationPlanStatus.AcceptedGroupInvite
					: FindGroupInstanceApplicationPlanStatus.AcceptedAllianceInvite,
				DirectPacketIntents: [],
				new FindGroupInstanceInviteIntent(
					inviteKind,
					responder.ObjectId,
					applicant.ObjectId,
					inviteKind == FindGroupInstanceInviteKind.Group
						? "PlayerGroupService.inviteToGroup(responder, applicant)"
						: "PlayerAllianceService.inviteToAlliance(responder, applicant)"));
		}

		return new FindGroupInstanceApplicationPlan(
			FindGroupInstanceApplicationPlanStatus.Declined,
			DirectPacketIntents:
			[
				new FindGroupDirectPacketIntent(
					applicant.ObjectId,
					new SmMessage(responder, ChatUtil.L10n(1400217)!, 4),
					"PacketSendUtility.sendPacket(applicant, new SM_MESSAGE(responder, ChatUtil.l10n(1400217), ChatType.WHISPER))")
			],
			InviteIntent: null);
	}

	public FindGroupJoinedTeamPlan OnJoinedTeam(
		Player player,
		FindGroupRecruitmentSubject currentTeam,
		bool isLeader,
		bool isFull,
		int nowEpochSeconds,
		byte serverId,
		FindGroupInstanceGroupJoinState? instanceGroup = null)
	{
		// Java parity: FindGroupService.onJoinedTeam first removes a qualifying server-wide
		// instance-group registration, then removes applications, removes the old solo
		// recruitment with unknown3=16, and either re-adds it as the current team or removes
		// the full team's recruitment. This is a disabled planner: callers must dispatch nothing.
		_instanceGroups.TryGetValue(player.ObjectId, out var trackedInstanceGroup);
		var instanceGroupPlayerMatches = instanceGroup is null || instanceGroup.PlayerObjectId == player.ObjectId;
		var instanceGroupMemberCount = instanceGroup?.MemberCount
			?? (trackedInstanceGroup is null ? 0 : currentTeam.Size);
		var instanceGroupMinMembers = instanceGroup?.MinMembers
			?? trackedInstanceGroup?.MinMembers
			?? 0;
		var shouldRemoveInstanceGroup = instanceGroupPlayerMatches
			&& (trackedInstanceGroup is not null || instanceGroup is not null)
			&& instanceGroupMemberCount >= instanceGroupMinMembers;
		FindGroupInstanceGroupState? removedInstanceGroup = null;
		if (shouldRemoveInstanceGroup)
			_instanceGroups.TryRemove(player.ObjectId, out removedInstanceGroup);

		var instanceGroupRemoval = new FindGroupInstanceGroupRemovalPlan(
			shouldRemoveInstanceGroup,
			"instanceGroups.remove(player.getObjectId()) when members >= minMembers",
			removedInstanceGroup);
		var applicationRemoval = RemoveApplication(player);
		var soloRecruitmentRemoval = RemoveRecruitment(
			player.ObjectId,
			serverId,
			unknown1: 0,
			unknown2: 0,
			unknown3: 16);

		FindGroupRecruitmentMutationPlan? teamRecruitmentAdd = null;
		FindGroupRecruitmentMutationPlan? fullTeamRecruitmentRemoval = null;

		if (soloRecruitmentRemoval.RemovedRecruitment is not null && isLeader)
		{
			teamRecruitmentAdd = AddRecruitment(
				player,
				soloRecruitmentRemoval.RemovedRecruitment.Message,
				soloRecruitmentRemoval.RemovedRecruitment.GroupType,
				nowEpochSeconds,
				currentTeam);
		}
		else if (isFull)
		{
			fullTeamRecruitmentRemoval = RemoveRecruitment(
				currentTeam.ObjectId,
				serverId,
				unknown1: 0,
				unknown2: 0,
				unknown3: 0);
		}

		return new FindGroupJoinedTeamPlan(
			instanceGroupRemoval,
			applicationRemoval,
			soloRecruitmentRemoval,
			teamRecruitmentAdd,
			fullTeamRecruitmentRemoval,
			DispatchLiveSideEffects: false);
	}
}

public enum FindGroupRecruitmentPlanStatus
{
	Added,
	Updated,
	Removed,
	Missing,
}

public enum FindGroupApplicationPlanStatus
{
	Added,
	Updated,
	Removed,
	Missing,
}

public enum FindGroupInstanceGroupPlanStatus
{
	Added,
	Updated,
	Removed,
	Missing,
	Shown,
}

public enum FindGroupInstanceApplicationPlanStatus
{
	ApplicationSent,
	MissingRecipient,
	AcceptedGroupInvite,
	AcceptedAllianceInvite,
	Declined,
	MissingApplicant,
	MissingInstanceGroup,
}

public enum FindGroupInstanceInviteKind
{
	Group,
	Alliance,
}

public sealed record FindGroupJoinedTeamPlan(
	FindGroupInstanceGroupRemovalPlan InstanceGroupRemoval,
	FindGroupApplicationMutationPlan ApplicationRemoval,
	FindGroupRecruitmentMutationPlan SoloRecruitmentRemoval,
	FindGroupRecruitmentMutationPlan? TeamRecruitmentAdd,
	FindGroupRecruitmentMutationPlan? FullTeamRecruitmentRemoval,
	bool DispatchLiveSideEffects);

public sealed record FindGroupInstanceGroupJoinState(
	int PlayerObjectId,
	int MemberCount,
	int MinMembers);

public sealed record FindGroupInstanceGroupRemovalPlan(
	bool ShouldRemove,
	string JavaSource,
	FindGroupInstanceGroupState? RemovedInstanceGroup = null);

public sealed record FindGroupRecruitmentMutationPlan(
	FindGroupRecruitmentPlanStatus Status,
	FindGroupRecruitmentState? CurrentRecruitment,
	FindGroupRecruitmentState? RemovedRecruitment,
	IReadOnlyList<FindGroupDirectPacketIntent> DirectPacketIntents,
	FindGroupWorldBroadcastIntent? WorldBroadcastIntent,
	FindGroupRecruitmentShowPlan? ShowRecruitmentsPlan);

public sealed record FindGroupDirectPacketIntent(
	int RecipientObjectId,
	GameServerPacket Packet,
	string JavaSource);

public sealed record FindGroupWorldBroadcastIntent(
	string Race,
	GameServerPacket Packet,
	string JavaSource);

public sealed record FindGroupRecruitmentShowPlan(
	string Race,
	int LastUpdate,
	IReadOnlyList<FindGroupRecruitmentSnapshot> Recruitments,
	GameServerPacket Packet);

public sealed record FindGroupApplicationMutationPlan(
	FindGroupApplicationPlanStatus Status,
	FindGroupApplicationState? CurrentApplication,
	FindGroupApplicationState? RemovedApplication,
	IReadOnlyList<FindGroupDirectPacketIntent> DirectPacketIntents,
	FindGroupWorldBroadcastIntent? WorldBroadcastIntent,
	FindGroupApplicationShowPlan? ShowApplicationsPlan);

public sealed record FindGroupApplicationShowPlan(
	string Race,
	int LastUpdate,
	IReadOnlyList<FindGroupApplicationSnapshot> Applications,
	GameServerPacket Packet);

public sealed record FindGroupInstanceGroupMutationPlan(
	FindGroupInstanceGroupPlanStatus Status,
	FindGroupInstanceGroupState? CurrentInstanceGroup,
	FindGroupInstanceGroupState? RemovedInstanceGroup,
	IReadOnlyList<FindGroupDirectPacketIntent> DirectPacketIntents,
	FindGroupInstanceGroupShowPlan? ShowInstanceGroupsPlan);

public sealed record FindGroupInstanceGroupShowPlan(
	string Race,
	int LastUpdate,
	IReadOnlyList<FindGroupInstanceGroupRegistrationSnapshot> InstanceGroups,
	GameServerPacket Packet);

public sealed record FindGroupInstanceGroupClientShowPlan(
	bool IsUpdate,
	bool FormInstanceGroupAnywhere,
	IReadOnlyList<int>? EnabledInstanceMaskIds,
	FindGroupDirectPacketIntent? EnableRegisterForInstancesIntent,
	FindGroupInstanceGroupShowPlan ShowInstanceGroupsPlan);

public sealed record FindGroupPortalInstanceGroupShowPlan(
	IReadOnlyList<int>? EnabledInstanceMaskIds,
	FindGroupDirectPacketIntent? EnableRegisterForInstancesIntent);

public enum FindGroupPrepareWindowPlanKind
{
	ShowEnterButton,
	ShowPrepareWindow,
	DestroyPrepareWindow,
	UpdatePrepareWindow,
}

public sealed record FindGroupPrepareWindowPlan(
	FindGroupPrepareWindowPlanKind Kind,
	IReadOnlyList<FindGroupDirectPacketIntent> DirectPacketIntents,
	bool DispatchLiveSideEffects)
{
	public static FindGroupPrepareWindowPlan WithIntent(
		FindGroupPrepareWindowPlanKind kind,
		FindGroupDirectPacketIntent intent)
	{
		return new FindGroupPrepareWindowPlan(kind, [intent], DispatchLiveSideEffects: false);
	}
}

public sealed record FindGroupLogoutCleanupPlan(
	int PlayerObjectId,
	FindGroupRecruitmentState? RemovedRecruitment,
	FindGroupApplicationState? RemovedApplication,
	FindGroupInstanceGroupState? RemovedInstanceGroup,
	IReadOnlyList<FindGroupDirectPacketIntent> DirectPacketIntents,
	bool DispatchLiveSideEffects,
	string JavaSource);

public sealed record FindGroupInstanceGroupMemberInfoPlan(
	FindGroupInstanceGroupPlanStatus Status,
	FindGroupInstanceGroupMemberInfoSnapshot? MemberInfo,
	IReadOnlyList<FindGroupDirectPacketIntent> DirectPacketIntents);

public sealed record FindGroupInstanceApplicationPlan(
	FindGroupInstanceApplicationPlanStatus Status,
	IReadOnlyList<FindGroupDirectPacketIntent> DirectPacketIntents,
	FindGroupInstanceInviteIntent? InviteIntent);

public sealed record FindGroupInstanceInviteIntent(
	FindGroupInstanceInviteKind Kind,
	int InviterObjectId,
	int InvitedObjectId,
	string JavaSource);

public sealed record FindGroupRecruitmentState(
	int ObjectId,
	string Race,
	bool IsSoloPlayer,
	int GroupType,
	string Message,
	string RecruiterName,
	int Size,
	int MinLevel,
	int MaxLevel,
	int ClassId,
	int LastUpdate)
{
	public static FindGroupRecruitmentState FromSubject(
		FindGroupRecruitmentSubject subject,
		string message,
		int groupType,
		int nowEpochSeconds)
	{
		return new FindGroupRecruitmentState(
			subject.ObjectId,
			subject.Race,
			subject.IsSoloPlayer,
			groupType,
			message,
			subject.RecruiterName,
			subject.Size,
			subject.MinLevel,
			subject.MaxLevel,
			subject.ClassId,
			nowEpochSeconds);
	}

	public FindGroupRecruitmentSnapshot ToSnapshot()
	{
		return new FindGroupRecruitmentSnapshot(
			ObjectId,
			ServerId: 0,
			IsSoloPlayer,
			(byte)GroupType,
			Message,
			RecruiterName,
			(byte)Size,
			(byte)MinLevel,
			(byte)MaxLevel,
			LastUpdate);
	}
}

public sealed record FindGroupRecruitmentSubject(
	int ObjectId,
	string Race,
	bool IsSoloPlayer,
	string RecruiterName,
	int Size,
	int MinLevel,
	int MaxLevel,
	int ClassId)
{
	public static FindGroupRecruitmentSubject FromSoloPlayer(Player player)
	{
		return new FindGroupRecruitmentSubject(
			player.ObjectId,
			player.Race,
			IsSoloPlayer: true,
			player.Name,
			Size: 1,
			player.Level,
			player.Level,
			ToJavaClassId(player.PlayerClass));
	}

	public static int ToJavaClassId(string playerClass)
	{
		// Java parity: model/PlayerClass.getClassId.
		return playerClass.ToUpperInvariant() switch
		{
			"GLADIATOR" => 1,
			"TEMPLAR" => 2,
			"SCOUT" => 3,
			"ASSASSIN" => 4,
			"RANGER" => 5,
			"MAGE" => 6,
			"SORCERER" => 7,
			"SPIRIT_MASTER" => 8,
			"PRIEST" => 9,
			"CLERIC" => 10,
			"CHANTER" => 11,
			"ENGINEER" => 12,
			"RIDER" => 13,
			"GUNNER" => 14,
			"ARTIST" => 15,
			"BARD" => 16,
			_ => 0,
		};
	}
}

public sealed record FindGroupInstanceGroupState(
	int RecruiterObjectId,
	string Race,
	int InstanceMaskId,
	int MinMembers,
	string Message,
	int LastUpdate,
	string RecruiterName,
	IReadOnlyList<FindGroupInstanceGroupMemberState> Members)
{
	public static FindGroupInstanceGroupState FromPlayer(
		Player player,
		int instanceMaskId,
		int minMembers,
		string message,
		int nowEpochSeconds,
		IReadOnlyList<FindGroupInstanceGroupMemberState>? currentMembers)
	{
		return new FindGroupInstanceGroupState(
			player.ObjectId,
			player.Race,
			instanceMaskId,
			minMembers,
			message,
			nowEpochSeconds,
			player.Name,
			currentMembers ?? [FindGroupInstanceGroupMemberState.FromPlayer(player)]);
	}

	public FindGroupInstanceGroupRegistrationSnapshot ToRegistrationSnapshot()
	{
		return new FindGroupInstanceGroupRegistrationSnapshot(
			RecruiterObjectId,
			InstanceMaskId,
			Members.Count,
			MinMembers,
			RecruiterObjectId,
			// Java parity: ServerWideGroup.getMinLevel/getMaxLevel use inverted comparators.
			MinLevel: Members.Max(member => member.Level),
			MaxLevel: Members.Min(member => member.Level),
			LastUpdate,
			RecruiterName,
			Message);
	}

	public FindGroupInstanceGroupMemberInfoSnapshot ToMemberInfoSnapshot(
		int nowEpochSeconds,
		IReadOnlyList<FindGroupInstanceGroupMemberState>? currentMembers = null)
	{
		var members = currentMembers ?? Members;
		return new FindGroupInstanceGroupMemberInfoSnapshot(
			nowEpochSeconds,
			members.Select(member => member.ToMemberInfoSnapshot()).ToArray());
	}
}

public sealed record FindGroupInstanceGroupMemberState(
	int WorldId,
	int PlayerObjectId,
	int Level,
	int ClassId,
	string Name)
{
	public static FindGroupInstanceGroupMemberState FromPlayer(Player player)
	{
		return new FindGroupInstanceGroupMemberState(
			player.Position.WorldId,
			player.ObjectId,
			player.Level,
			FindGroupRecruitmentSubject.ToJavaClassId(player.PlayerClass),
			player.Name);
	}

	public FindGroupInstanceGroupMemberInfoMemberSnapshot ToMemberInfoSnapshot()
	{
		return new FindGroupInstanceGroupMemberInfoMemberSnapshot(
			WorldId,
			PlayerObjectId,
			Level,
			ClassId,
			Name);
	}
}

public sealed record FindGroupApplicationState(
	int PlayerObjectId,
	string Race,
	int GroupType,
	string Message,
	string PlayerName,
	int ClassId,
	int Level,
	int LastUpdate)
{
	public static FindGroupApplicationState FromPlayer(
		Player player,
		string message,
		int groupType,
		int classId,
		int level,
		int nowEpochSeconds)
	{
		return new FindGroupApplicationState(
			player.ObjectId,
			player.Race,
			groupType,
			message,
			player.Name,
			classId,
			level,
			nowEpochSeconds);
	}

	public FindGroupApplicationSnapshot ToSnapshot()
	{
		return new FindGroupApplicationSnapshot(
			PlayerObjectId,
			(byte)GroupType,
			Message,
			PlayerName,
			(byte)ClassId,
			(byte)Level,
			LastUpdate);
	}
}
