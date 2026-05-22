using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmCompositeStones : GameClientPacket
{
	public CmCompositeStones(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int ToolItemObjectId { get; private set; }

	public int FirstItemObjectId { get; private set; }

	public int SecondItemObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_COMPOSITE_STONES.readImpl.
		ToolItemObjectId = buffer.ReadD();
		FirstItemObjectId = buffer.ReadD();
		SecondItemObjectId = buffer.ReadD();
	}
}
