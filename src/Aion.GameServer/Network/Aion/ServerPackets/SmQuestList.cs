using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmQuestList : GameServerPacket
{
	public const int PacketOpCode = 71;

	private readonly IReadOnlyList<PlayerQuestState> _questStates;

	public SmQuestList(IReadOnlyList<PlayerQuestState> questStates)
		: base(PacketOpCode)
	{
		_questStates = questStates;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_QUEST_LIST.writeImpl.
		buffer.WriteH(1);
		buffer.WriteH((-_questStates.Count) & 0xffff);
		foreach (var questState in _questStates)
		{
			buffer.WriteD(questState.QuestId);
			buffer.WriteC(questState.GetStatusValue());
			buffer.WriteD(questState.GetClientQuestVars());
			buffer.WriteC(questState.GetClientCompleteCount());
		}
	}
}
