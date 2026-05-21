using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmMoveInAir : GameClientPacket
{
	public CmMoveInAir(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int WorldId { get; private set; }

	public float X { get; private set; }

	public float Y { get; private set; }

	public float Z { get; private set; }

	public byte Heading { get; private set; }

	public int Distance { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_MOVE_IN_AIR.readImpl.
		WorldId = buffer.ReadD();
		X = buffer.ReadF();
		Y = buffer.ReadF();
		Z = buffer.ReadF();
		Heading = buffer.ReadC();
		Distance = buffer.ReadD();
	}
}
