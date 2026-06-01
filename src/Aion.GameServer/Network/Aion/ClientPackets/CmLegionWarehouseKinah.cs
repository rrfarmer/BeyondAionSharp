using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmLegionWarehouseKinah : GameClientPacket
{
	public CmLegionWarehouseKinah(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public long Amount { get; private set; }
	public byte ActionType { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_LEGION_WH_KINAH.readImpl.
		Amount = buffer.ReadQ();
		ActionType = buffer.ReadC();
	}
}
