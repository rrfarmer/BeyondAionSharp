using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmAbyssRankingLegions : GameClientPacket
{
	public CmAbyssRankingLegions(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte RaceId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_ABYSS_RANKING_LEGIONS.readImpl.
		RaceId = buffer.ReadC();
	}
}
