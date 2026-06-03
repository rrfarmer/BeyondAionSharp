using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmExchangeAddKinah : GameClientPacket
{
	public CmExchangeAddKinah(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	// Java parity: CM_EXCHANGE_ADD_KINAH.readImpl: kinahCount = readQ().
	public long KinahCount { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_EXCHANGE_ADD_KINAH.readImpl.
		KinahCount = buffer.ReadQ();
	}
}
