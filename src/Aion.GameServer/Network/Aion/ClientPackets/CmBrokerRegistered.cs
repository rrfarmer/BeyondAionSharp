using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmBrokerRegistered : GameClientPacket
{
	public CmBrokerRegistered(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int BrokerObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_BROKER_REGISTERED.readImpl.
		BrokerObjectId = buffer.ReadD();
	}
}
