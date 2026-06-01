using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmDisconnect : GameClientPacket
{
	public CmDisconnect(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte Unknown { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_DISCONNECT.readImpl reads and ignores one byte.
		Unknown = buffer.ReadC();
	}
}
