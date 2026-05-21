using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmInstanceInfo : GameClientPacket
{
	public CmInstanceInfo(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int Unknown { get; private set; }

	public byte UpdateType { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_INSTANCE_INFO.readImpl.
		Unknown = buffer.ReadD();
		UpdateType = buffer.ReadC();
	}
}
