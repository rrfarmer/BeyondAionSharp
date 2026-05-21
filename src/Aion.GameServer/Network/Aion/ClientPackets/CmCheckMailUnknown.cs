using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmCheckMailUnknown : GameClientPacket
{
	public CmCheckMailUnknown(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_CHECK_MAIL_UNK.readImpl has no payload.
	}
}
