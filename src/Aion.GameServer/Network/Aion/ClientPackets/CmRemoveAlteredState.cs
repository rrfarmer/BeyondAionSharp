using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmRemoveAlteredState : GameClientPacket
{
	public CmRemoveAlteredState(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int SkillId { get; private set; }

	public byte Unknown1 { get; private set; }

	public byte Unknown2 { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_REMOVE_ALTERED_STATE.readImpl.
		SkillId = buffer.ReadH();
		Unknown1 = buffer.ReadC();
		Unknown2 = buffer.ReadC();
	}
}
