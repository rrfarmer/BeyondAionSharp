using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmHouseEdit : GameServerPacket
{
	public const int PacketOpCode = 82;

	private readonly int _action;

	public SmHouseEdit(int action)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_HOUSE_EDIT simple mode actions.
		_action = action;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteC(_action);
	}
}
