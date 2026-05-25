using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmQuestAction : GameServerPacket
{
	public const int PacketOpCode = 124;

	private const int UpdateAction = 2;

	private readonly PlayerQuestState _questState;
	private readonly bool _suppressForExtraCategory;

	private SmQuestAction(PlayerQuestState questState, bool suppressForExtraCategory)
		: base(PacketOpCode)
	{
		_questState = questState;
		_suppressForExtraCategory = suppressForExtraCategory;
	}

	public static SmQuestAction Update(PlayerQuestState questState, bool suppressForExtraCategory = false)
	{
		return new SmQuestAction(questState, suppressForExtraCategory);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity breadcrumb: network/aion/serverpackets/SM_QUEST_ACTION.writeImpl.
		// Java returns before writing payload when QuestTemplate.extraCategory != NONE.
		if (_suppressForExtraCategory)
			return;

		buffer.WriteC(UpdateAction);
		buffer.WriteD(_questState.QuestId);
		buffer.WriteC(_questState.GetStatusValue());
		buffer.WriteC(0);
		buffer.WriteD(_questState.GetClientQuestVars());
		buffer.WriteH(0);
	}
}
