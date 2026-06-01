using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmViewPlayerDetails : GameClientPacket
{
	public CmViewPlayerDetails(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int TargetObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_VIEW_PLAYER_DETAILS.readImpl.
		TargetObjectId = buffer.ReadD();
	}
}
