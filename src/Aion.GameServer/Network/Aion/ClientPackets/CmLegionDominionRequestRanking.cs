using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmLegionDominionRequestRanking : GameClientPacket
{
	public CmLegionDominionRequestRanking(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int StonespearId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_LEGION_DOMINION_REQUEST_RANKING.readImpl.
		StonespearId = buffer.ReadD();
	}
}
