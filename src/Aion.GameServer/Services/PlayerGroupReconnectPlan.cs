using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed record PlayerGroupReconnectResult(
	bool Reconnected,
	PlayerGroupReconnectPacketPlan? PacketPlan)
{
	public static PlayerGroupReconnectResult NotFound()
	{
		return new PlayerGroupReconnectResult(false, null);
	}
}

public sealed record PlayerGroupReconnectPacketPlan(
	int TeamId,
	int ReconnectingPlayerObjectId,
	bool SendGroupInfoToReconnectingPlayer,
	IReadOnlyList<PlayerGroupMemberInfoIntent> MemberInfoIntents,
	PlayerGroupInfoPacketPlan? GroupInfoPlan)
{
	public SmGroupInfo? CreateGroupInfoPacket()
	{
		// Java parity: model/team/group/events/PlayerConnectedEvent sends SM_GROUP_INFO to the reconnecting player.
		return SendGroupInfoToReconnectingPlayer && GroupInfoPlan != null
			? new SmGroupInfo(GroupInfoPlan)
			: null;
	}
}

public sealed record PlayerGroupMemberInfoIntent(
	int RecipientObjectId,
	int SubjectObjectId,
	PlayerGroupEvent Event,
	PlayerGroupMemberInfoPacketPlan? PacketPlan = null)
{
	public SmGroupMemberInfo? CreatePacket()
	{
		// Java parity: callers send SM_GROUP_MEMBER_INFO when packet planning metadata is available.
		return PacketPlan == null ? null : new SmGroupMemberInfo(PacketPlan);
	}
}

public sealed record PlayerGroupMemberInfoUpdatePlan(
	int TeamId,
	int SubjectObjectId,
	PlayerGroupEvent Event,
	int Slot,
	IReadOnlyList<PlayerGroupMemberInfoIntent> MemberInfoIntents);

public sealed record PlayerGroupMentorStatusChangePlan(
	int TeamId,
	int MentorObjectId,
	bool IsMentor,
	IReadOnlyList<PlayerGroupSystemMessageIntent> SystemMessageIntents,
	IReadOnlyList<PlayerGroupMemberInfoIntent> MemberInfoIntents,
	PlayerGroupMentorAbyssRankUpdateIntent? AbyssRankUpdateIntent);

public sealed record PlayerGroupLeaderChangePlan(
	int TeamId,
	int NewLeaderObjectId,
	IReadOnlyList<PlayerGroupLeaderChangePacketIntent> PacketIntents);

public sealed record PlayerGroupLeaderChangePacketIntent(
	int Sequence,
	int RecipientObjectId,
	PlayerGroupInfoPacketPlan GroupInfoPlan,
	SmSystemMessage SystemMessage);

public enum PlayerGroupLeaveReason
{
	Leave,
	Ban,
	LeaveTimeout,
	Disband,
}

public enum PlayerGroupLeavePacketIntentKind
{
	MemberInfo,
	SystemMessage,
}

public sealed record PlayerGroupLeavePlan(
	int TeamId,
	int LeavedPlayerObjectId,
	PlayerGroupLeaveReason Reason,
	IReadOnlyList<PlayerGroupLeavePacketIntent> PacketIntents,
	PlayerBaseLeaveSideEffectPlan BaseLeavePlan,
	PlayerGroupLeaderChangePlan? LeaderChangePlan,
	bool WouldDisband,
	bool WouldStopMentoring,
	bool WouldInvokeEventServiceOnLeftTeam);

public sealed record PlayerGroupLeavePacketIntent(
	int Sequence,
	int RecipientObjectId,
	PlayerGroupLeavePacketIntentKind Kind,
	PlayerGroupMemberInfoPacketPlan? MemberInfoPlan = null,
	SmSystemMessage? SystemMessage = null)
{
	public GameServerPacket CreatePacket()
	{
		// Java parity: PlayerGroupLeavedEvent sends SM_GROUP_MEMBER_INFO and leave reason messages to remaining members.
		return Kind switch
		{
			PlayerGroupLeavePacketIntentKind.MemberInfo when MemberInfoPlan != null => new SmGroupMemberInfo(MemberInfoPlan),
			PlayerGroupLeavePacketIntentKind.SystemMessage when SystemMessage != null => SystemMessage,
			_ => throw new InvalidOperationException("Group leave packet intent is missing packet metadata."),
		};
	}
}

public sealed record PlayerGroupMentorAbyssRankUpdateIntent(
	int PlayerObjectId,
	bool IsMentor)
{
	public SmAbyssRankUpdate CreatePacket()
	{
		// Java parity: mentoring events broadcast SM_ABYSS_RANK_UPDATE(2, player).
		return SmAbyssRankUpdate.MentorStatusChange(PlayerObjectId, IsMentor);
	}
}

public sealed record PlayerGroupMemberInfoPacketPlan(
	int GroupId,
	int MemberObjectId,
	PlayerGroupEvent RequestedEvent,
	PlayerGroupEvent EffectiveEvent,
	int Slot,
	bool IsOnline,
	PlayerGroupMemberInfoPrefixSnapshot PrefixSnapshot,
	bool WritesLifeStatsBlock,
	bool WritesPositionBlock,
	bool WritesCommonDataBlock,
	bool WritesName,
	bool WritesAbnormalEffects,
	bool WritesSlotTimers,
	IReadOnlyList<PlayerGroupMemberEffectInfo>? AbnormalEffects = null)
{
	public static PlayerGroupMemberInfoPacketPlan FromMember(
		int groupId,
		PlayerGroupMember member,
		PlayerGroupEvent requestedEvent,
		int slot = 0)
	{
		// Java parity: network/aion/serverpackets/SM_GROUP_MEMBER_INFO.writeImpl header and event branch selection.
		var effectiveEvent = requestedEvent == PlayerGroupEvent.Enter && !member.IsOnline
			? PlayerGroupEvent.EnterOffline
			: requestedEvent;
		var writesName = effectiveEvent is PlayerGroupEvent.EnterOffline
			or PlayerGroupEvent.Join
			or PlayerGroupEvent.Enter
			or PlayerGroupEvent.Update;
		var writesEffects = effectiveEvent is PlayerGroupEvent.Enter
			or PlayerGroupEvent.Update
			or PlayerGroupEvent.UpdateEffects;

		return new PlayerGroupMemberInfoPacketPlan(
			groupId,
			member.ObjectId,
			requestedEvent,
			effectiveEvent,
			slot,
			member.IsOnline,
			PlayerGroupMemberInfoPrefixSnapshot.FromMember(
				member,
				effectiveEvent,
				PlayerGroupMemberInfoResourceMaximums.FromStatsInfo(member.Player)),
			WritesLifeStatsBlock: true,
			WritesPositionBlock: true,
			WritesCommonDataBlock: true,
			writesName,
			writesEffects,
			WritesSlotTimers: writesEffects);
	}
}

public sealed record PlayerGroupMemberEffectInfo(
	int EffectorObjectId,
	int SkillId,
	int SkillLevel,
	int TargetSlotOrdinal,
	int RemainingTimeToDisplayMillis);

public sealed record PlayerGroupMemberInfoResourceMaximums(int MaxHp, int MaxMp, int MaxFp)
{
	public static PlayerGroupMemberInfoResourceMaximums FromStatsInfo(Player player)
	{
		// Java parity: model/stats/container/CreatureLifeStats.getMaxHp/getMaxMp and PlayerLifeStats.getMaxFp read current game stats.
		var maxStats = SmStatsInfo.CalculateCurrentResourceMaxStats(player);
		return new PlayerGroupMemberInfoResourceMaximums(maxStats.MaxHp, maxStats.MaxMp, maxStats.MaxFp);
	}
}

public sealed record PlayerGroupMemberInfoPrefixSnapshot(
	int? MaxHp,
	int CurrentHp,
	int? MaxMp,
	int CurrentMp,
	int? MaxFp,
	int CurrentFp,
	int Unknown3Point5,
	int MapId,
	int MapInstanceId,
	float X,
	float Y,
	float Z,
	int ClassId,
	int GenderId,
	int Level,
	int EventId,
	int AlwaysOne,
	int FlyState,
	int MentorFlag,
	string Name)
{
	public bool HasKnownLifeStatMaximums => MaxHp.HasValue && MaxMp.HasValue && MaxFp.HasValue;

	public static PlayerGroupMemberInfoPrefixSnapshot FromMember(
		PlayerGroupMember member,
		PlayerGroupEvent effectiveEvent,
		PlayerGroupMemberInfoResourceMaximums? resourceMaximums = null)
	{
		// Java parity: network/aion/serverpackets/SM_GROUP_MEMBER_INFO.writeImpl fixed prefix after group/member ids.
		var player = member.Player;
		var lifeStats = player.LifeStats;
		var position = player.Position;
		var isOnline = member.IsOnline;
		var maxHp = isOnline ? resourceMaximums?.MaxHp : 0;
		var maxMp = isOnline ? resourceMaximums?.MaxMp : 0;
		var maxFp = isOnline ? resourceMaximums?.MaxFp : 0;

		return new PlayerGroupMemberInfoPrefixSnapshot(
			MaxHp: maxHp,
			CurrentHp: isOnline ? GetCurrentHp(lifeStats, maxHp) : 0,
			MaxMp: maxMp,
			CurrentMp: isOnline ? GetCurrentMp(lifeStats, maxMp) : 0,
			MaxFp: maxFp,
			CurrentFp: isOnline ? GetCurrentFp(lifeStats) : 0,
			Unknown3Point5: 0,
			MapId: position.WorldId,
			MapInstanceId: position.WorldId + position.InstanceId - 1,
			X: position.X,
			Y: position.Y,
			Z: position.Z,
			ClassId: ToJavaClassId(player.PlayerClass),
			GenderId: ToJavaGenderId(player.Gender),
			Level: player.Level,
			EventId: (int)effectiveEvent,
			AlwaysOne: 1,
			FlyState: (int)player.FlyState,
			MentorFlag: player.IsMentor ? 1 : 0,
			Name: player.Name);
	}

	public PlayerGroupMemberInfoPrefixSnapshot WithKnownMaximums(int maxHp, int maxMp, int maxFp)
	{
		return this with
		{
			MaxHp = Math.Max(0, maxHp),
			MaxMp = Math.Max(0, maxMp),
			MaxFp = Math.Max(0, maxFp),
			CurrentHp = Math.Clamp(CurrentHp, 0, Math.Max(0, maxHp)),
			CurrentMp = Math.Clamp(CurrentMp, 0, Math.Max(0, maxMp)),
			CurrentFp = Math.Max(0, CurrentFp),
		};
	}

	private static int GetCurrentHp(PlayerLifeStats? lifeStats, int? maxHp)
	{
		if (lifeStats == null)
			return maxHp ?? 0;
		return maxHp.HasValue ? lifeStats.GetCurrentHp(maxHp.Value) : Math.Max(0, lifeStats.CurrentHp);
	}

	private static int GetCurrentMp(PlayerLifeStats? lifeStats, int? maxMp)
	{
		if (lifeStats == null)
			return maxMp ?? 0;
		return maxMp.HasValue ? lifeStats.GetCurrentMp(maxMp.Value) : Math.Max(0, lifeStats.CurrentMp);
	}

	private static int GetCurrentFp(PlayerLifeStats? lifeStats)
	{
		return lifeStats?.GetCurrentFp() ?? 0;
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

	private static int ToJavaGenderId(string gender)
	{
		// Java parity: model/Gender.getGenderId.
		return string.Equals(gender, "FEMALE", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
	}
}
