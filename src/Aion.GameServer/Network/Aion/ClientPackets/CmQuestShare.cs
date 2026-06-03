using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmQuestShare : GameClientPacket
{
	public CmQuestShare(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int QuestId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_QUEST_SHARE.readImpl.
		QuestId = buffer.ReadD();
	}
}
