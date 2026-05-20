using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmEnterWorldCheck : GameServerPacket
{
	public const int PacketOpCode = 13;

	private readonly EnterWorldCheckMessage _message;

	public SmEnterWorldCheck(EnterWorldCheckMessage message = EnterWorldCheckMessage.Ok)
		: base(PacketOpCode)
	{
		_message = message;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_ENTER_WORLD_CHECK.writeImpl.
		buffer.WriteC((byte)_message);
		buffer.WriteC(0);
		buffer.WriteC(0);
	}
}
