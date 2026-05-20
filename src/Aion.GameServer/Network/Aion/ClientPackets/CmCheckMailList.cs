using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmCheckMailList : GameClientPacket
{
	public CmCheckMailList(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public bool ExpressOnly { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_CHECK_MAIL_LIST.readImpl.
		ExpressOnly = buffer.ReadC() == 1;
	}
}
