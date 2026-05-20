using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmBrokerService : GameServerPacket
{
	public const int PacketOpCode = 146;

	private readonly long _settledKinah;

	public SmBrokerService(long settledKinah)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_BROKER_SERVICE(boolean showSettledIcon, long settledKinah).
		_settledKinah = settledKinah;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_BROKER_SERVICE.writeShowSettledIcon.
		buffer.WriteC(5);
		buffer.WriteQ(_settledKinah);
		buffer.WriteD(0);
		buffer.WriteH(0);
		buffer.WriteH(1);
		buffer.WriteC(0);
	}
}
