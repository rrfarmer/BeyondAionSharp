using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmSummonAttack : GameClientPacket
{
	public CmSummonAttack(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int SummonObjectId { get; private set; }

	public int TargetObjectId { get; private set; }

	public byte Unknown1 { get; private set; }

	public int Time { get; private set; }

	public byte Unknown3 { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_SUMMON_ATTACK.readImpl.
		SummonObjectId = buffer.ReadD();
		TargetObjectId = buffer.ReadD();
		Unknown1 = buffer.ReadC();
		Time = buffer.ReadH();
		Unknown3 = buffer.ReadC();
	}
}
