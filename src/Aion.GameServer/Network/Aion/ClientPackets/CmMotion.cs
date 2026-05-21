using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmMotion : GameClientPacket
{
	public CmMotion(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte Unknown { get; private set; }

	public int MotionId { get; private set; }

	public byte MotionType { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_MOTION.readImpl.
		Unknown = buffer.ReadC();
		MotionId = buffer.ReadH();
		MotionType = buffer.ReadC();
	}
}
