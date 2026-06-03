using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmHouseOpenDoor : GameClientPacket
{
	public CmHouseOpenDoor(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	// Java parity: CM_HOUSE_OPEN_DOOR.readImpl: address + leave flag.
	public int Address { get; private set; }
	public bool Leave { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_HOUSE_OPEN_DOOR.readImpl.
		Address = buffer.ReadD();
		Leave = buffer.ReadC() != 0;
	}
}
