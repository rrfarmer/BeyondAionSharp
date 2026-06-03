using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmLegionModifyEmblem : GameClientPacket
{
	public CmLegionModifyEmblem(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int LegionId { get; private set; }
	public byte EmblemId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_LEGION_MODIFY_EMBLEM.readImpl.
		LegionId = buffer.ReadD();
		EmblemId = buffer.ReadC();
	}
}
