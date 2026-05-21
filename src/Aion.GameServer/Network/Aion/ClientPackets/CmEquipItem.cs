using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmEquipItem : GameClientPacket
{
	public CmEquipItem(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte Action { get; private set; }

	public long Slot { get; private set; }

	public int ItemObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_EQUIP_ITEM.readImpl.
		Action = buffer.ReadC();
		Slot = buffer.ReadQ();
		ItemObjectId = buffer.ReadD();
	}
}
