using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmSummonPanelRemove : GameServerPacket
{
	public const int PacketOpCode = 73;
	private readonly int _skillId;

	public SmSummonPanelRemove(int skillId) : base(PacketOpCode)
	{
		_skillId = skillId;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_SUMMON_PANEL_REMOVE.writeImpl
		// writes skill id as H and a one-byte nonzero-skill flag.
		buffer.WriteH(_skillId);
		buffer.WriteC(_skillId != 0 ? 1 : 0);
	}
}
