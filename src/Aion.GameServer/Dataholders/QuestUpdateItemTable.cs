namespace Aion.GameServer.Dataholders;

public sealed class QuestUpdateItemTable
{
	private readonly IReadOnlySet<int> _itemIds;

	public QuestUpdateItemTable(IReadOnlyList<int> itemIds)
	{
		ItemIds = itemIds;
		_itemIds = new HashSet<int>(itemIds);
	}

	public IReadOnlyList<int> ItemIds { get; }

	public int Count => ItemIds.Count;

	public bool ContainsItemId(int itemId)
	{
		return _itemIds.Contains(itemId);
	}
}
