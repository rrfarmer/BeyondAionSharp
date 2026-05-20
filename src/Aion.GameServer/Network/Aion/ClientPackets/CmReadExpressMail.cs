using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmReadExpressMail : GameClientPacket
{
	public CmReadExpressMail(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int Action { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_READ_EXPRESS_MAIL.readImpl.
		Action = buffer.ReadC();
	}
}
