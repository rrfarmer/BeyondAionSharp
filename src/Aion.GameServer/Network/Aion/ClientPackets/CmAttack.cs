using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmAttack : GameClientPacket
{
	public CmAttack(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int TargetObjectId { get; private set; }

	public byte AttackNo { get; private set; }

	public int Time { get; private set; }

	public byte Type { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_ATTACK.readImpl.
		TargetObjectId = buffer.ReadD();
		AttackNo = buffer.ReadC();
		Time = buffer.ReadH();
		Type = buffer.ReadC();
	}
}
