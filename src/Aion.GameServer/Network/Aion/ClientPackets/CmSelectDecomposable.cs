using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmSelectDecomposable : GameClientPacket
{
	public CmSelectDecomposable(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int ObjectId { get; private set; }

	public int Unknown { get; private set; }

	public int Index { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_SELECT_DECOMPOSABLE.readImpl.
		ObjectId = buffer.ReadD();
		Unknown = buffer.ReadD();
		Index = buffer.ReadC();
	}
}
