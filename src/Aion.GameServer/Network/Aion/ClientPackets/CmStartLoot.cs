using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmStartLoot : GameClientPacket
{
	public CmStartLoot(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int TargetObjectId { get; private set; }

	public byte Action { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_START_LOOT.readImpl.
		TargetObjectId = buffer.ReadD();
		Action = buffer.ReadC();
	}
}
