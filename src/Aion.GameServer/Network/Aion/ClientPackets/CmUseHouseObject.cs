using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmUseHouseObject : GameClientPacket
{
	public CmUseHouseObject(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int ObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_USE_HOUSE_OBJECT.readImpl.
		ObjectId = buffer.ReadD();
	}
}
