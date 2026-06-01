using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class FindGroupRecruitmentPlanService
{
	private readonly Dictionary<int, FindGroupRecruitmentState> _recruitments = [];
	private readonly Dictionary<int, FindGroupApplicationState> _applications = [];

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
		if (!_recruitments.Remove(playerOrTeamId, out var removed))
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
		if (!_applications.Remove(player.ObjectId, out var removed))
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
		var instanceGroupRemoval = new FindGroupInstanceGroupRemovalPlan(
			instanceGroup is not null
				&& instanceGroup.PlayerObjectId == player.ObjectId
				&& instanceGroup.MemberCount >= instanceGroup.MinMembers,
			"instanceGroups.remove(player.getObjectId()) when members >= minMembers");
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
	string JavaSource);

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

	private static int ToJavaClassId(string playerClass)
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
