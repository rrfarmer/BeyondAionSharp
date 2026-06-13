using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class WorldNpcQuestDropService
{
	private static readonly QuestDropTable EmptyQuestDrops = new([]);
	private readonly GameServerRuntimeContext? _runtimeContext;
	private readonly QuestDropTable? _questDrops;
	private readonly Func<float> _chanceRoll;

	public WorldNpcQuestDropService(GameServerRuntimeContext runtimeContext)
		: this(runtimeContext, null, null)
	{
	}

	public WorldNpcQuestDropService(QuestDropTable questDrops, Func<float>? chanceRoll = null)
		: this(null, questDrops, chanceRoll)
	{
	}

	private WorldNpcQuestDropService(
		GameServerRuntimeContext? runtimeContext,
		QuestDropTable? questDrops,
		Func<float>? chanceRoll)
	{
		_runtimeContext = runtimeContext;
		_questDrops = questDrops;
		_chanceRoll = chanceRoll ?? (() => Random.Shared.NextSingle() * 100f);
	}

	public WorldNpcQuestDropResult CreateDrops(
		IWorldNpcObject? npc,
		Player? looter,
		IReadOnlyList<Player>? groupMembers = null,
		int startIndex = 1)
	{
		// Java parity: services/QuestService.getQuestDrop.
		if (npc == null || looter == null)
			return WorldNpcQuestDropResult.Empty(startIndex);

		var configuredDrops = GetQuestDrops().GetQuestDrops(npc.TemplateId);
		if (configuredDrops.Count == 0)
			return WorldNpcQuestDropResult.Empty(startIndex);

		var drops = new List<WorldNpcDropItem>();
		var allowedLooters = new HashSet<int>();
		var index = startIndex;
		foreach (var drop in configuredDrops)
		{
			if (_chanceRoll() >= drop.Chance)
				continue;

			if (groupMembers != null && looter.TeamMembership == PlayerTeamMembership.Group)
			{
				index = AddTeamQuestDrop(drops, allowedLooters, index, npc.ObjectId, drop, groupMembers, drop.IsDropEachMemberGroup);
				continue;
			}

			if (groupMembers != null && looter.TeamMembership == PlayerTeamMembership.Alliance)
			{
				index = AddTeamQuestDrop(drops, allowedLooters, index, npc.ObjectId, drop, groupMembers, drop.IsDropEachMemberAlliance);
				continue;
			}

			if (!IsQuestDrop(looter, drop))
				continue;

			allowedLooters.Add(looter.ObjectId);
			drops.Add(CreateQuestDropItem(drop, index++, npc.ObjectId, new HashSet<int> { looter.ObjectId }));
		}

		return new WorldNpcQuestDropResult(drops, allowedLooters, index);
	}

	private int AddTeamQuestDrop(
		List<WorldNpcDropItem> drops,
		HashSet<int> allowedLooters,
		int index,
		int npcObjectId,
		QuestDropSummary drop,
		IReadOnlyList<Player> groupMembers,
		bool dropEachMember)
	{
		var eligibleMembers = groupMembers.Where(member => IsQuestDrop(member, drop)).ToArray();
		if (eligibleMembers.Length == 0)
			return index;

		foreach (var member in eligibleMembers)
			allowedLooters.Add(member.ObjectId);

		if (dropEachMember)
		{
			foreach (var member in eligibleMembers)
				drops.Add(CreateQuestDropItem(drop, index++, npcObjectId, new HashSet<int> { member.ObjectId }));
		}
		else
		{
			drops.Add(CreateQuestDropItem(
				drop,
				index++,
				npcObjectId,
				eligibleMembers.Select(member => member.ObjectId).ToHashSet()));
		}

		return index;
	}

	private QuestDropTable GetQuestDrops()
	{
		return _questDrops ?? _runtimeContext?.DataManager?.StaticData.QuestDrops ?? EmptyQuestDrops;
	}

	private static WorldNpcDropItem CreateQuestDropItem(
		QuestDropSummary drop,
		int index,
		int npcObjectId,
		IReadOnlySet<int> playerObjectIds)
	{
		// Java parity: services/QuestService.regQuestDropItem.
		return new WorldNpcDropItem(
			index,
			drop.ItemId,
			Count: 1,
			PlayerObjectIds: playerObjectIds,
			NpcObjectId: npcObjectId);
	}

	private static bool IsQuestDrop(Player player, QuestDropSummary drop)
	{
		var quest = player.Quests.FirstOrDefault(state => state.QuestId == drop.QuestId);
		if (quest == null || !string.Equals(quest.Status, "START", StringComparison.Ordinal))
			return false;
		if (drop.CollectingStep != 0 && drop.CollectingStep != quest.GetQuestVarById(0))
			return false;
		if (string.Equals(drop.Target, "ALLIANCE", StringComparison.OrdinalIgnoreCase)
			&& player.TeamMembership != PlayerTeamMembership.Alliance)
		{
			return false;
		}

		if (string.Equals(drop.MentorType, "MENTE", StringComparison.OrdinalIgnoreCase)
			&& player.TeamMembership != PlayerTeamMembership.Group)
		{
			return false;
		}

		if (drop.CollectItems.Count == 0)
			return true;

		return drop.CollectItems.Any(
			collectItem => collectItem.ItemId == drop.ItemId
				&& GetInventoryCount(player, collectItem.ItemId) < collectItem.Count);
	}

	private static long GetInventoryCount(Player player, int itemId)
	{
		return player.GetInventory().GetItems().Where(item => item.GetItemId() == itemId).Sum(item => item.GetItemCount());
	}
}

public sealed record WorldNpcQuestDropResult(
	IReadOnlyList<WorldNpcDropItem> Drops,
	IReadOnlySet<int> AllowedLooterObjectIds,
	int NextIndex)
{
	public static WorldNpcQuestDropResult Empty(int nextIndex)
	{
		return new WorldNpcQuestDropResult(Array.Empty<WorldNpcDropItem>(), new HashSet<int>(), nextIndex);
	}
}
