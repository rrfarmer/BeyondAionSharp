using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmQuestAction : GameServerPacket
{
	public const int PacketOpCode = 124;

	// Java parity: SM_QUEST_ACTION.ActionType ids.
	public const int AddActionId = 1;
	public const int UpdateActionId = 2;
	public const int AbandonActionId = 3;

	private readonly int _actionId;
	private readonly PlayerQuestState _questState;
	private readonly bool _suppressForExtraCategory;

	private SmQuestAction(int actionId, PlayerQuestState questState, bool suppressForExtraCategory)
		: base(PacketOpCode)
	{
		_actionId = actionId;
		_questState = questState;
		_suppressForExtraCategory = suppressForExtraCategory;
	}

	/// <summary>Java parity: SM_QUEST_ACTION(ActionType.ADD, qs).</summary>
	public static SmQuestAction Add(PlayerQuestState questState, bool suppressForExtraCategory = false)
	{
		return new SmQuestAction(AddActionId, questState, suppressForExtraCategory);
	}

	/// <summary>Java parity: SM_QUEST_ACTION(ActionType.UPDATE, qs).</summary>
	public static SmQuestAction Update(PlayerQuestState questState, bool suppressForExtraCategory = false)
	{
		return new SmQuestAction(UpdateActionId, questState, suppressForExtraCategory);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity breadcrumb: network/aion/serverpackets/SM_QUEST_ACTION.writeImpl.
		// Java returns before writing payload when QuestTemplate.extraCategory != NONE.
		if (_suppressForExtraCategory)
			return;

		buffer.WriteC(_actionId);
		buffer.WriteD(_questState.QuestId);
		buffer.WriteC(_questState.GetStatusValue());
		buffer.WriteC(0);
		buffer.WriteD(_questState.GetClientQuestVars());
		buffer.WriteH(0);
	}
}
