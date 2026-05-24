using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmSummonCastSpell : GameClientPacket
{
	public CmSummonCastSpell(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int SummonObjectId { get; private set; }

	public int SkillId { get; private set; }

	public int SkillLevel { get; private set; }

	public int TargetObjectId { get; private set; }

	public int Unknown { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_SUMMON_CASTSPELL.readImpl.
		SummonObjectId = buffer.ReadD();
		SkillId = buffer.ReadH();
		SkillLevel = buffer.ReadC();
		TargetObjectId = buffer.ReadD();
		Unknown = buffer.ReadD();
	}
}
