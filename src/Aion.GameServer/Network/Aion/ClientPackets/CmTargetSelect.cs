using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmTargetSelect : GameClientPacket
{
	public CmTargetSelect(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int TargetObjectId { get; private set; }

	public bool SelectTargetOfTarget { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_TARGET_SELECT.readImpl.
		TargetObjectId = buffer.ReadD();
		SelectTargetOfTarget = buffer.ReadC() == 1;
	}
}
