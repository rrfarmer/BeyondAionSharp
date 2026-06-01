using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmToggleSkillDeactivate : GameClientPacket
{
	public CmToggleSkillDeactivate(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int SkillId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_TOGGLE_SKILL_DEACTIVATE.readImpl.
		SkillId = buffer.ReadH();
		_ = buffer.ReadSignedH();
		_ = buffer.ReadSignedH();
	}
}
