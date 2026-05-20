using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmTitleInfo : GameServerPacket
{
	public const int PacketOpCode = 176;

	private readonly int _titleId;

	public SmTitleInfo(int titleId)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_TITLE_INFO(int titleId).
		_titleId = titleId;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_TITLE_INFO.writeImpl action 1.
		buffer.WriteC(1);
		buffer.WriteH(_titleId);
	}
}
