using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class FindGroupRecruitmentPlanService
{
	private readonly Dictionary<int, FindGroupRecruitmentState> _recruitments = [];

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
}

public enum FindGroupRecruitmentPlanStatus
{
	Added,
	Updated,
	Removed,
	Missing,
}

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
