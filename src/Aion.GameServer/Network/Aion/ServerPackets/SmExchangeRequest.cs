using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmExchangeRequest : GameServerPacket
{
	public const int PacketOpCode = 74;

	private readonly string _receiver;

	public SmExchangeRequest(string receiver)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_EXCHANGE_REQUEST(String receiver).
		_receiver = receiver;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteS(_receiver);
	}
}
