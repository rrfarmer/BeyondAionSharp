using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmMayLoginIntoGame : GameServerPacket
{
	public const int PacketOpCode = 137;

	public SmMayLoginIntoGame()
		: base(PacketOpCode)
	{
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_MAY_LOGIN_INTO_GAME.writeImpl.
		buffer.WriteD(0);
	}
}
