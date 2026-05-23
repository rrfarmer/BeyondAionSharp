using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class QuestDropTable
{
	private readonly IReadOnlyDictionary<int, IReadOnlyList<QuestDropSummary>> _dropsByNpcId;

	public QuestDropTable(IReadOnlyList<QuestDropSummary> questDrops)
	{
		QuestDrops = questDrops;
		_dropsByNpcId = new ReadOnlyDictionary<int, IReadOnlyList<QuestDropSummary>>(
			questDrops
				.GroupBy(drop => drop.NpcId)
				.ToDictionary(
					group => group.Key,
					group => (IReadOnlyList<QuestDropSummary>)group.ToArray()));
	}

	public IReadOnlyList<QuestDropSummary> QuestDrops { get; }

	public int Count => QuestDrops.Count;

	public IReadOnlyList<QuestDropSummary> GetQuestDrops(int npcId)
	{
		return _dropsByNpcId.TryGetValue(npcId, out var drops) ? drops : Array.Empty<QuestDropSummary>();
	}
}

public sealed record QuestDropSummary(
	int QuestId,
	int NpcId,
	int ItemId,
	int Chance,
	int DropEachMember,
	int CollectingStep,
	string Target,
	string MentorType,
	IReadOnlyList<QuestCollectItemSummary> CollectItems)
{
	public bool IsDropEachMemberGroup => DropEachMember == 1;

	public bool IsDropEachMemberAlliance => DropEachMember is 1 or 2;
}

public sealed record QuestCollectItemSummary(int ItemId, long Count);
