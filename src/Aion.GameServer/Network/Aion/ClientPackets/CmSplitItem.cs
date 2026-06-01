using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmSplitItem : GameClientPacket
{
	public CmSplitItem(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int SourceItemObjectId { get; private set; }

	public byte SourceStorageType { get; private set; }

	public long ItemAmount { get; private set; }

	public int DestinationItemObjectId { get; private set; }

	public byte DestinationStorageType { get; private set; }

	public short SlotNumber { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_SPLIT_ITEM.readImpl.
		SourceItemObjectId = buffer.ReadD();
		ItemAmount = buffer.ReadQ();
		SourceStorageType = buffer.ReadC();
		DestinationItemObjectId = buffer.ReadD();
		DestinationStorageType = buffer.ReadC();
		SlotNumber = buffer.ReadSignedH();
	}
}
