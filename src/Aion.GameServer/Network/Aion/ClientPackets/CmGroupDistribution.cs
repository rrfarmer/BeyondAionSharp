using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmGroupDistribution : GameClientPacket
{
	public CmGroupDistribution(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public long Amount { get; private set; }

	public byte PartyType { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_GROUP_DISTRIBUTION.readImpl.
		Amount = buffer.ReadQ();
		PartyType = buffer.ReadC();
	}
}
