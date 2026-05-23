using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed record PlayerGroupInfoPacketPlan(
	int TeamId,
	int LeaderObjectId,
	int ActivePlayerMapId,
	PlayerGroupLootRules LootRules,
	int ConstantGroupInfoMarker,
	int UnknownByte,
	int TeamType,
	int TeamSubType,
	int MessageId,
	string Name)
{
	public static PlayerGroupInfoPacketPlan FromDescriptor(PlayerGroupDescriptor descriptor, int activePlayerMapId)
	{
		// Java parity: network/aion/serverpackets/SM_GROUP_INFO.writeImpl field order, represented as non-sending intent.
		var (teamType, teamSubType) = descriptor.TeamType.ToJavaPacketFields();
		return new PlayerGroupInfoPacketPlan(
			descriptor.TeamId,
			descriptor.LeaderObjectId,
			activePlayerMapId,
			descriptor.LootRules,
			ConstantGroupInfoMarker: 0x02,
			UnknownByte: 0x00,
			teamType,
			teamSubType,
			MessageId: 0x00,
			Name: string.Empty);
	}
}
