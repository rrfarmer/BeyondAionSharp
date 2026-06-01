using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmMoveItem : GameClientPacket
{
	public CmMoveItem(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int ItemObjectId { get; private set; }

	public byte Source { get; private set; }

	public byte Destination { get; private set; }

	public short Slot { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_MOVE_ITEM.readImpl.
		ItemObjectId = buffer.ReadD();
		Source = buffer.ReadC();
		Destination = buffer.ReadC();
		Slot = buffer.ReadSignedH();
	}
}
