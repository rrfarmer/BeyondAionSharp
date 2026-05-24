using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmExchangeRequest : GameClientPacket
{
	public CmExchangeRequest(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int TargetObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_EXCHANGE_REQUEST.readImpl.
		TargetObjectId = buffer.ReadD();
	}
}
