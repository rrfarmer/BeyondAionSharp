using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmChargeItem : GameClientPacket
{
	public CmChargeItem(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int TargetNpcObjectId { get; private set; }

	public int ChargeLevel { get; private set; }

	public IReadOnlyList<int> ItemObjectIds { get; private set; } = Array.Empty<int>();

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_CHARGE_ITEM.readImpl.
		TargetNpcObjectId = buffer.ReadD();
		ChargeLevel = buffer.ReadC();
		var itemsSize = buffer.ReadH();
		var itemObjectIds = new List<int>(itemsSize);
		for (var i = 0; i < itemsSize; i++)
			itemObjectIds.Add(buffer.ReadD());
		ItemObjectIds = itemObjectIds;
	}
}
