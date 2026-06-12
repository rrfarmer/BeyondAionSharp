using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmQuestCompletedList : GameServerPacket
{
	public const int PacketOpCode = 123;
	private const int StaticBodySize = 4;
	private const int DynamicBodyPartSize = 6;

	private readonly int _updateMode;
	private readonly IReadOnlyList<PlayerQuestState> _questStates;

	public SmQuestCompletedList(int updateMode, IReadOnlyList<PlayerQuestState> questStates)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_QUEST_COMPLETED_LIST(int updateMode, List<QuestState>).
		_updateMode = updateMode;
		_questStates = questStates;
	}

	public static IReadOnlyList<SmQuestCompletedList> CreateLoginPackets(IReadOnlyList<PlayerQuestState> questStates)
	{
		// Java parity: questEngine/global::Aion.GameServer.QuestEngine.QuestEngine.sendCompletedQuests DynamicServerPacketBodySplitList usage.
		var completedQuests = questStates.Where(quest => quest.IsCompletedAtLeastOnce).ToArray();
		if (completedQuests.Length == 0)
			return [new SmQuestCompletedList(updateMode: 0, Array.Empty<PlayerQuestState>())];

		var maxEntries = Math.Max(1, (MaxUsablePacketBodySize - StaticBodySize) / DynamicBodyPartSize);
		var packets = new List<SmQuestCompletedList>();
		for (var index = 0; index < completedQuests.Length; index += maxEntries)
		{
			var part = completedQuests.Skip(index).Take(maxEntries).ToArray();
			packets.Add(new SmQuestCompletedList(packets.Count == 0 ? 0 : 1, part));
		}

		return packets;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_QUEST_COMPLETED_LIST.writeImpl.
		buffer.WriteC(1);
		buffer.WriteC(_updateMode);
		buffer.WriteH((-_questStates.Count) & 0xffff);
		foreach (var questState in _questStates)
		{
			buffer.WriteD(questState.QuestId);
			buffer.WriteC(questState.GetClientCompleteCount());
			buffer.WriteC(questState.GetCompletedQuestRepeatFlag());
		}
	}
}
