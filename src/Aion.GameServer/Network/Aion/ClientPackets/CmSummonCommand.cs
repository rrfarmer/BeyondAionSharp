using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmSummonCommand : GameClientPacket
{
	public CmSummonCommand(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int Mode { get; private set; }

	public int Unknown1 { get; private set; }

	public int Unknown2 { get; private set; }

	public int TargetObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_SUMMON_COMMAND.readImpl.
		Mode = buffer.ReadC();
		Unknown1 = buffer.ReadD();
		Unknown2 = buffer.ReadD();
		TargetObjectId = buffer.ReadD();
	}
}
