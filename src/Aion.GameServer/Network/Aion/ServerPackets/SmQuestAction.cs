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
	public const int ShareActionId = 5;

	private readonly int _actionId;
	private readonly int _questId;
	private readonly PlayerQuestState? _questState;
	private readonly int _sharerId;
	private readonly bool _shareInAlliance;
	private readonly bool _suppressForExtraCategory;

	private SmQuestAction(
		int actionId,
		int questId,
		PlayerQuestState? questState,
		int sharerId,
		bool shareInAlliance,
		bool suppressForExtraCategory)
		: base(PacketOpCode)
	{
		_actionId = actionId;
		_questId = questId;
		_questState = questState;
		_sharerId = sharerId;
		_shareInAlliance = shareInAlliance;
		_suppressForExtraCategory = suppressForExtraCategory;
	}

	/// <summary>Java parity: SM_QUEST_ACTION(ActionType.ADD, qs).</summary>
	public static SmQuestAction Add(PlayerQuestState questState, bool suppressForExtraCategory = false)
	{
		return new SmQuestAction(AddActionId, questState.QuestId, questState, 0, false, suppressForExtraCategory);
	}

	/// <summary>Java parity: SM_QUEST_ACTION(ActionType.UPDATE, qs).</summary>
	public static SmQuestAction Update(PlayerQuestState questState, bool suppressForExtraCategory = false)
	{
		return new SmQuestAction(UpdateActionId, questState.QuestId, questState, 0, false, suppressForExtraCategory);
	}

	/// <summary>
	/// Java parity: SM_QUEST_ACTION(int questId, int sharerId, boolean shareInAlliance) → ActionType.SHARE.
	/// Shows the share-quest question window on the receiver. shareInAlliance writes 0 (group) or 1 (alliance).
	/// </summary>
	public static SmQuestAction Share(int questId, int sharerId, bool shareInAlliance, bool suppressForExtraCategory = false)
	{
		return new SmQuestAction(ShareActionId, questId, null, sharerId, shareInAlliance, suppressForExtraCategory);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity breadcrumb: network/aion/serverpackets/SM_QUEST_ACTION.writeImpl.
		// Java returns before writing payload when QuestTemplate.extraCategory != NONE.
		if (_suppressForExtraCategory)
			return;

		buffer.WriteC(_actionId);
		buffer.WriteD(_questId);
		switch (_actionId)
		{
			case AddActionId:
			case UpdateActionId:
				buffer.WriteC(_questState!.GetStatusValue());
				buffer.WriteC(0);
				buffer.WriteD(_questState.GetClientQuestVars());
				buffer.WriteH(0);
				break;
			case ShareActionId:
				// Java: case SHARE: writeD(sharerId); writeD(shareInAlliance ? 1 : 0).
				buffer.WriteD(_sharerId);
				buffer.WriteD(_shareInAlliance ? 1 : 0);
				break;
		}
	}
}
