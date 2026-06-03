using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmGather : GameClientPacket
{
	public CmGather(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	// Java parity: CM_GATHER.readImpl: actionId = readD(); -1=cancel, 0=start, 128=start via /attack chat.
	public int ActionId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_GATHER.readImpl.
		ActionId = buffer.ReadD();
	}
}
