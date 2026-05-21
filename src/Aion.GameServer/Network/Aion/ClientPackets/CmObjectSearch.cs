using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmObjectSearch : GameClientPacket
{
	public CmObjectSearch(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int NpcId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_OBJECT_SEARCH.readImpl.
		NpcId = buffer.ReadD();
	}
}
