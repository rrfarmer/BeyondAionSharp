using Aion.GameServer.Model.GameObjects;
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
	PlayerGroupMemberInfoPacketPlan? PacketPlan = null);

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
	bool WritesSlotTimers)
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
			PlayerGroupMemberInfoPrefixSnapshot.FromMember(member, effectiveEvent),
			WritesLifeStatsBlock: true,
			WritesPositionBlock: true,
			WritesCommonDataBlock: true,
			writesName,
			writesEffects,
			WritesSlotTimers: writesEffects);
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
	public bool HasKnownOnlineMaximums => MaxHp.HasValue && MaxMp.HasValue && MaxFp.HasValue;

	public static PlayerGroupMemberInfoPrefixSnapshot FromMember(PlayerGroupMember member, PlayerGroupEvent effectiveEvent)
	{
		// Java parity: network/aion/serverpackets/SM_GROUP_MEMBER_INFO.writeImpl fixed prefix after group/member ids.
		var player = member.Player;
		var lifeStats = player.LifeStats;
		var position = player.Position;
		var isOnline = member.IsOnline;

		return new PlayerGroupMemberInfoPrefixSnapshot(
			MaxHp: null,
			CurrentHp: isOnline ? lifeStats?.CurrentHp ?? 0 : 0,
			MaxMp: null,
			CurrentMp: isOnline ? lifeStats?.CurrentMp ?? 0 : 0,
			MaxFp: null,
			CurrentFp: isOnline ? lifeStats?.GetCurrentFp() ?? 0 : 0,
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
