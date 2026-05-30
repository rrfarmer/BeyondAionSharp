using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmTuneResult : GameClientPacket
{
	public CmTuneResult(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int ItemObjectId { get; private set; }

	public bool HasAccepted { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_TUNE_RESULT.readImpl.
		ItemObjectId = buffer.ReadD();
		HasAccepted = buffer.ReadC() == 1;
	}
}
