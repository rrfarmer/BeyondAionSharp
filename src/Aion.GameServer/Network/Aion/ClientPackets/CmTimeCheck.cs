using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmTimeCheck : GameClientPacket
{
	public CmTimeCheck(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int NanoTime { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_TIME_CHECK.readImpl.
		NanoTime = buffer.ReadD();
	}
}
