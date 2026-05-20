namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerQuestState(
	int QuestId,
	string Status,
	int QuestVars,
	int Flags,
	int CompleteCount)
{
	public bool IsComplete => string.Equals(Status, "COMPLETE", StringComparison.Ordinal);

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

	public int GetClientCompleteCount()
	{
		return Math.Min(CompleteCount, 255);
	}
}
