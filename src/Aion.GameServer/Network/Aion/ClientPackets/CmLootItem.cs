using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmLootItem : GameClientPacket
{
	public CmLootItem(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int TargetObjectId { get; private set; }

	public int Index { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_LOOT_ITEM.readImpl.
		TargetObjectId = buffer.ReadD();
		Index = buffer.ReadC();
	}
}
