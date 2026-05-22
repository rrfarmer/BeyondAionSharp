using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmReleaseObject : GameClientPacket
{
	public CmReleaseObject(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int TargetObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_RELEASE_OBJECT.readImpl.
		TargetObjectId = buffer.ReadD();
	}
}
