using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmBindPointTeleport : GameClientPacket
{
	public CmBindPointTeleport(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte Action { get; private set; }

	public int LocId { get; private set; }

	public long Kinah { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_BIND_POINT_TELEPORT.readImpl.
		Action = buffer.ReadC();
		if (Action == 1)
		{
			LocId = buffer.ReadD();
			Kinah = buffer.ReadQ();
		}
	}
}
