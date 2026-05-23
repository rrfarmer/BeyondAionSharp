using Aion.Commons.Network;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmGroupInfo : GameServerPacket
{
	public const int PacketOpCode = 90;
	private readonly PlayerGroupInfoPacketPlan _plan;

	public SmGroupInfo(PlayerGroupInfoPacketPlan plan)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_GROUP_INFO(PlayerGroup group).
		_plan = plan;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_GROUP_INFO.writeImpl.
		buffer.WriteD(_plan.TeamId);
		buffer.WriteD(_plan.LeaderObjectId);
		buffer.WriteD(_plan.ActivePlayerMapId);
		buffer.WriteD((int)_plan.LootRules.LootRule);
		buffer.WriteD(_plan.LootRules.Misc);
		buffer.WriteD(_plan.LootRules.CommonItemAbove);
		buffer.WriteD(_plan.LootRules.SuperiorItemAbove);
		buffer.WriteD(_plan.LootRules.HeroicItemAbove);
		buffer.WriteD(_plan.LootRules.FabledItemAbove);
		buffer.WriteD(_plan.LootRules.EternalItemAbove);
		buffer.WriteD(_plan.LootRules.MythicItemAbove);
		buffer.WriteD(_plan.ConstantGroupInfoMarker);
		buffer.WriteC(_plan.UnknownByte);
		buffer.WriteD(_plan.TeamType);
		buffer.WriteD(_plan.TeamSubType);
		buffer.WriteD(_plan.MessageId);
		buffer.WriteS(_plan.Name);
	}
}
