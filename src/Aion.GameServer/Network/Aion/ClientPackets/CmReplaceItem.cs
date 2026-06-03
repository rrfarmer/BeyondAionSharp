using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmReplaceItem : GameClientPacket
{
	public CmReplaceItem(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	// Java parity: CM_REPLACE_ITEM.readImpl: sourceStorageType + sourceItemObjId + replaceStorageType + replaceItemObjId.
	public byte SourceStorageType { get; private set; }
	public int SourceItemObjectId { get; private set; }
	public byte ReplaceStorageType { get; private set; }
	public int ReplaceItemObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_REPLACE_ITEM.readImpl.
		SourceStorageType = buffer.ReadC();
		SourceItemObjectId = buffer.ReadD();
		ReplaceStorageType = buffer.ReadC();
		ReplaceItemObjectId = buffer.ReadD();
	}
}
