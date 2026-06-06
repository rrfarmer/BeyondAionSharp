using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmLegionHistory : GameClientPacket
{
	public CmLegionHistory(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	// Java parity: CM_LEGION_HISTORY.readImpl: page (int) + Type.values()[readUC()] (0=LEGION, 1=REWARD, 2=WAREHOUSE).
	public int Page { get; private set; }
	public byte Type { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_LEGION_HISTORY.readImpl.
		Page = buffer.ReadD();
		Type = buffer.ReadC();
	}
}
