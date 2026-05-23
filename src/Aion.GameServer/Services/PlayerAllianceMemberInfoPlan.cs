using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed record PlayerAllianceMemberInfoUpdatePlan(
	int AllianceId,
	int SubjectObjectId,
	PlayerAllianceEvent Event,
	int Slot,
	IReadOnlyList<PlayerAllianceMemberInfoIntent> MemberInfoIntents);

public sealed record PlayerAllianceMemberInfoIntent(
	int RecipientObjectId,
	int SubjectObjectId,
	PlayerAllianceEvent Event,
	PlayerAllianceMemberInfoPacketPlan? PacketPlan = null)
{
	public SmAllianceMemberInfo? CreatePacket()
	{
		// Java parity: model/team/alliance/events/PlayerAllianceUpdateEvent sends SM_ALLIANCE_MEMBER_INFO when packet metadata is available.
		return PacketPlan == null ? null : new SmAllianceMemberInfo(PacketPlan);
	}
}

public sealed record PlayerAllianceMemberInfoPacketPlan(
	int AllianceId,
	int MemberObjectId,
	PlayerAllianceEvent RequestedEvent,
	PlayerAllianceEvent EffectiveEvent,
	int Slot,
	bool IsOnline,
	PlayerAllianceMemberInfoPrefixSnapshot PrefixSnapshot,
	bool WritesName,
	bool WritesAbnormalEffects,
	bool WritesSlotTimers,
	IReadOnlyList<PlayerGroupMemberEffectInfo>? AbnormalEffects = null)
{
	public static PlayerAllianceMemberInfoPacketPlan FromPlayer(
		int allianceId,
		Player player,
		PlayerAllianceEvent requestedEvent,
		int slot = 0)
	{
		// Java parity: network/aion/serverpackets/SM_ALLIANCE_MEMBER_INFO.writeImpl header and ENTER_OFFLINE rewrite.
		var effectiveEvent = requestedEvent == PlayerAllianceEvent.Enter && !player.IsOnline
			? PlayerAllianceEvent.EnterOffline
			: requestedEvent;
		var effectiveEventId = (int)effectiveEvent;
		var writesName = effectiveEventId is 5 or 7 or 13;
		var writesEffects = effectiveEvent == PlayerAllianceEvent.UpdateEffects
			|| writesName && player.IsOnline;

		return new PlayerAllianceMemberInfoPacketPlan(
			allianceId,
			player.ObjectId,
			requestedEvent,
			effectiveEvent,
			slot,
			player.IsOnline,
			PlayerAllianceMemberInfoPrefixSnapshot.FromPlayer(player, effectiveEvent),
			writesName,
			writesEffects,
			WritesSlotTimers: writesEffects);
	}
}

public sealed record PlayerAllianceMemberInfoPrefixSnapshot(
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
	int AllianceUnknown,
	string Name)
{
	public static PlayerAllianceMemberInfoPrefixSnapshot FromPlayer(Player player, PlayerAllianceEvent effectiveEvent)
	{
		// Java parity: SM_ALLIANCE_MEMBER_INFO.writeImpl fixed prefix; the final prefix byte is always 0 for alliance packets.
		var position = player.Position;
		var maxStats = player.IsOnline ? SmStatsInfo.CalculateCurrentResourceMaxStats(player) : null;

		return new PlayerAllianceMemberInfoPrefixSnapshot(
			MaxHp: player.IsOnline ? maxStats?.MaxHp : 0,
			CurrentHp: player.IsOnline ? GetCurrentHp(player.LifeStats, maxStats?.MaxHp) : 0,
			MaxMp: player.IsOnline ? maxStats?.MaxMp : 0,
			CurrentMp: player.IsOnline ? GetCurrentMp(player.LifeStats, maxStats?.MaxMp) : 0,
			MaxFp: player.IsOnline ? maxStats?.MaxFp : 0,
			CurrentFp: player.IsOnline ? player.LifeStats?.GetCurrentFp() ?? 0 : 0,
			Unknown3Point5: 0,
			MapId: position.WorldId,
			MapInstanceId: position.WorldId + position.InstanceId - 1,
			X: position.X,
			Y: position.Y,
			Z: position.Z,
			ClassId: ToJavaClassId(player.PlayerClass),
			GenderId: string.Equals(player.Gender, "FEMALE", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
			Level: player.Level,
			EventId: (int)effectiveEvent,
			AlwaysOne: 1,
			FlyState: (int)player.FlyState,
			AllianceUnknown: 0,
			Name: player.Name);
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
