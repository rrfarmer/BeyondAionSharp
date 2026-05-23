using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PlayerAllianceAssignType
{
	Promote,
	DemoteCaptainToViceCaptain,
	Demote,
}

public enum PlayerAllianceRolePlanStatus
{
	Planned,
	EventPlayerMissing,
	EventPlayerOffline,
	PromoteLimitReached,
}

public enum PlayerAllianceTeamType
{
	Alliance,
	AutoAlliance,
	AllianceDefence,
	AllianceOffence,
}

public static class PlayerAllianceTeamTypeExtensions
{
	public static (int Type, int SubType) ToJavaPacketFields(this PlayerAllianceTeamType teamType)
	{
		// Java parity: model/team/TeamType.getType/getSubType for alliance variants.
		return teamType switch
		{
			PlayerAllianceTeamType.Alliance => (0x3F, 0),
			PlayerAllianceTeamType.AutoAlliance => (0x36, 1),
			PlayerAllianceTeamType.AllianceDefence => (0x3F, 4),
			PlayerAllianceTeamType.AllianceOffence => (0x02, 3),
			_ => (0x3F, 0),
		};
	}
}

public sealed record PlayerAllianceViceCaptainAssignmentPlan(
	int AllianceId,
	int LeaderObjectId,
	int EventPlayerObjectId,
	PlayerAllianceAssignType AssignType,
	PlayerAllianceRolePlanStatus Status,
	IReadOnlyList<int> ViceCaptainObjectIdsAfterEvent,
	IReadOnlyList<PlayerAllianceInfoIntent> AllianceInfoIntents,
	PlayerAllianceSystemMessageIntent? SystemMessageIntent = null,
	bool WouldBroadcastLeague = false);

public sealed record PlayerAllianceLeaderChangePlan(
	int AllianceId,
	int OldLeaderObjectId,
	int NewLeaderObjectId,
	bool EventPlayerWasSpecified,
	IReadOnlyList<int> ViceCaptainObjectIdsAfterEvent,
	IReadOnlyList<PlayerAllianceInfoIntent> AllianceInfoIntents,
	IReadOnlyList<PlayerAllianceSystemMessageIntent> SystemMessageIntents,
	bool WouldBroadcastLeague = false);

public sealed record PlayerAllianceInfoIntent(
	int RecipientObjectId,
	PlayerAllianceInfoPacketPlan PacketPlan)
{
	public SmAllianceInfo CreatePacket()
	{
		// Java parity: model/team/alliance/events/AssignViceCaptainEvent sends SM_ALLIANCE_INFO after role updates.
		return new SmAllianceInfo(PacketPlan);
	}
}

public sealed record PlayerAllianceSystemMessageIntent(
	int RecipientObjectId,
	SmSystemMessage Message);

public sealed record PlayerAllianceInfoPacketPlan(
	int AllianceGroupSize,
	int AllianceId,
	int LeaderObjectId,
	int ActivePlayerMapId,
	IReadOnlyList<int> ViceCaptainObjectIds,
	IReadOnlyList<int> PaddedViceCaptainObjectIds,
	PlayerGroupLootRules LootRules,
	int ConstantGroupInfoMarker,
	int UnknownByte,
	int TeamType,
	int TeamSubType,
	int LeagueId,
	IReadOnlyList<PlayerAllianceInfoGroupPlaceholder> GroupPlaceholders,
	int MessageId,
	string Message)
{
	public const int ViceCaptainPromoteMessageId = 1300984;
	public const int ViceCaptainDemoteMessageId = 1300985;

	public static PlayerAllianceInfoPacketPlan FromSnapshot(
		int allianceId,
		int leaderObjectId,
		int allianceGroupSize,
		int activePlayerMapId,
		IReadOnlyList<int> viceCaptainObjectIds,
		PlayerGroupLootRules lootRules,
		PlayerAllianceTeamType teamType,
		int messageId,
		string message,
		int leagueId = 0)
	{
		// Java parity: network/aion/serverpackets/SM_ALLIANCE_INFO.writeImpl field order, represented as a non-sending plan.
		var (type, subType) = teamType.ToJavaPacketFields();
		var cappedViceCaptainIds = viceCaptainObjectIds.Take(4).ToArray();
		var paddedViceCaptainIds = cappedViceCaptainIds
			.Concat(Enumerable.Repeat(0, Math.Max(0, 4 - cappedViceCaptainIds.Length)))
			.ToArray();

		return new PlayerAllianceInfoPacketPlan(
			allianceGroupSize,
			allianceId,
			leaderObjectId,
			activePlayerMapId,
			cappedViceCaptainIds,
			paddedViceCaptainIds,
			lootRules,
			ConstantGroupInfoMarker: 0x02,
			UnknownByte: 0x00,
			type,
			subType,
			leagueId,
			GroupPlaceholders:
			[
				new PlayerAllianceInfoGroupPlaceholder(0, 1000),
				new PlayerAllianceInfoGroupPlaceholder(1, 1001),
				new PlayerAllianceInfoGroupPlaceholder(2, 1002),
				new PlayerAllianceInfoGroupPlaceholder(3, 1003),
			],
			messageId,
			messageId != 0 ? message : string.Empty);
	}
}

public sealed record PlayerAllianceInfoGroupPlaceholder(int GroupNumber, int GroupId);
