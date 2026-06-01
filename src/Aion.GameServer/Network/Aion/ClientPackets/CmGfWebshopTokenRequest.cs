using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmGfWebshopTokenRequest : GameClientPacket
{
	public CmGfWebshopTokenRequest(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_GF_WEBSHOP_TOKEN_REQUEST.readImpl reads no payload.
	}
}
