using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmOpenStaticDoor : GameClientPacket
{
	public CmOpenStaticDoor(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int DoorId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_OPEN_STATICDOOR.readImpl.
		DoorId = buffer.ReadD();
	}
}
