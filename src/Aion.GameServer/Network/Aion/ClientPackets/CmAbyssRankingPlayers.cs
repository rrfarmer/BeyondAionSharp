using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmAbyssRankingPlayers : GameClientPacket
{
	public CmAbyssRankingPlayers(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte RaceId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_ABYSS_RANKING_PLAYERS.readImpl.
		RaceId = buffer.ReadC();
	}
}
