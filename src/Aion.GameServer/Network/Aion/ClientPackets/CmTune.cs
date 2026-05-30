using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmTune : GameClientPacket
{
	public CmTune(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int ItemObjectId { get; private set; }

	public int TuningScrollObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_TUNE.readImpl.
		ItemObjectId = buffer.ReadD();
		TuningScrollObjectId = buffer.ReadD();
	}
}
