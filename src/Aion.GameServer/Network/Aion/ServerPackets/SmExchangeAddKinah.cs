using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmExchangeAddKinah : GameServerPacket
{
	public const int PacketOpCode = 77;
	public const int ActionSelf = 0;
	public const int ActionOther = 1;

	private readonly long _kinahCount;
	private readonly int _action;

	public SmExchangeAddKinah(long kinahCount, int action) : base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_EXCHANGE_ADD_KINAH.writeImpl.
		// action: 0 = self, 1 = other.
		_kinahCount = kinahCount;
		_action = action;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteC(_action);
		buffer.WriteQ(_kinahCount);
	}
}
