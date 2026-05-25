namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerQuestState(
	int QuestId,
	string Status,
	int QuestVars,
	int Flags,
	int CompleteCount,
	int? RewardGroup = null)
{
	public bool IsComplete => string.Equals(Status, "COMPLETE", StringComparison.Ordinal);

	public bool IsCompletedAtLeastOnce => CompleteCount > 0;

	// Java parity: questEngine/model/QuestStatus.value.
	public int GetStatusValue()
	{
		return Status switch
		{
			"START" => 3,
			"REWARD" => 4,
			"COMPLETE" => 5,
			"LOCKED" => 6,
			_ => 0,
		};
	}

	// Java parity: network/aion/serverpackets/SM_QUEST_LIST quest var packing.
	public int GetClientQuestVars()
	{
		return QuestVars | (Flags << 24);
	}

	public int GetQuestVarById(int id)
	{
		// Java parity: questEngine/model/QuestVars stores six 6-bit quest vars in quest_vars.
		return id is < 0 or > 5 ? 0 : (QuestVars >> (id * 6)) & 0x3F;
	}

	public int GetClientCompleteCount()
	{
		return Math.Min(CompleteCount, 255);
	}

	// Java parity: network/aion/serverpackets/SM_QUEST_COMPLETED_LIST writes QuestState.canRepeat() ? 0 : 1.
	public int GetCompletedQuestRepeatFlag()
	{
		// The Java value depends on QuestTemplate repeat metadata and next_repeat_time.
		// Those quest templates are not ported yet, so match the non-repeat default until the quest engine lands.
		return 1;
	}
}
