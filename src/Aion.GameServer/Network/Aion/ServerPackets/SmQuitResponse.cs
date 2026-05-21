using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmQuitResponse : GameServerPacket
{
	public const int PacketOpCode = 98;
	private readonly bool _editMode;

	public SmQuitResponse(bool editMode = false)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_QUIT_RESPONSE.
		_editMode = editMode;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_editMode ? 2 : 1);
		buffer.WriteC(0);
		buffer.WriteD(-1);
	}
}
