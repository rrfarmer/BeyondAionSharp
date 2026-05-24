using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmExchangeConfirmation : GameServerPacket
{
	public const int PacketOpCode = 78;
	public const int Canceled = 1;
	public const int Confirmed = 2;
	public const int Locked = 3;

	private readonly int _action;

	public SmExchangeConfirmation(int action)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_EXCHANGE_CONFIRMATION(int action).
		_action = action;
	}

	public int Action => _action;

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteC(_action);
	}
}
