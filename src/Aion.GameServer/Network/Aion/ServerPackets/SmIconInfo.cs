using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmIconInfo : GameServerPacket
{
	public const int PacketOpCode = 175; // Java parity: ServerPacketsOpcodes addPacketOpcode(175, SM_ICON_INFO.class).

	private readonly int _buffId;
	private readonly bool _display;

	public SmIconInfo(int buffId, bool display)
		: base(PacketOpCode)
	{
		_buffId = buffId;
		_display = display;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_ICON_INFO.writeImpl.
		buffer.WriteD(0);
		buffer.WriteD(_buffId);
		buffer.WriteC(_display ? 1 : 0);
	}
}
