using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmChatInit : GameServerPacket
{
	public const int PacketOpCode = 230;
	private readonly byte[] _token;

	public SmChatInit(byte[] token)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_CHAT_INIT(byte[] token).
		_token = token;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_CHAT_INIT.writeImpl.
		buffer.WriteD(_token.Length);
		buffer.WriteB(_token);
	}
}
