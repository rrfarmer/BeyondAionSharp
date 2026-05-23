using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class WorldNpcGlobalDropService
{
	private static readonly GlobalDropTable EmptyGlobalDrops = new([]);
	private static readonly ItemTemplateTable EmptyItemTemplates = new([]);
	private static readonly GlobalNpcExclusionTable EmptyExclusions = GlobalNpcExclusionTable.Empty;
	private readonly GameServerRuntimeContext? _runtimeContext;
	private readonly GlobalDropTable? _globalDrops;
	private readonly ItemTemplateTable? _itemTemplates;
	private readonly GlobalNpcExclusionTable? _globalNpcExclusions;
	private readonly Func<float> _chanceRoll;
	private readonly Func<int, int, int> _countRoll;
	private readonly Func<float, float> _weightedRoll;

	public WorldNpcGlobalDropService(GameServerRuntimeContext runtimeContext)
		: this(runtimeContext, null, null, null, null, null, null)
	{
	}

	public WorldNpcGlobalDropService(
		GlobalDropTable globalDrops,
		ItemTemplateTable itemTemplates,
		GlobalNpcExclusionTable? globalNpcExclusions = null,
		Func<float>? chanceRoll = null,
		Func<int, int, int>? countRoll = null,
		Func<float, float>? weightedRoll = null)
		: this(null, globalDrops, itemTemplates, globalNpcExclusions, chanceRoll, countRoll, weightedRoll)
	{
	}

	private WorldNpcGlobalDropService(
		GameServerRuntimeContext? runtimeContext,
		GlobalDropTable? globalDrops,
		ItemTemplateTable? itemTemplates,
		GlobalNpcExclusionTable? globalNpcExclusions,
		Func<float>? chanceRoll,
		Func<int, int, int>? countRoll,
		Func<float, float>? weightedRoll)
	{
		_runtimeContext = runtimeContext;
		_globalDrops = globalDrops;
		_itemTemplates = itemTemplates;
		_globalNpcExclusions = globalNpcExclusions;
		_chanceRoll = chanceRoll ?? (() => Random.Shared.NextSingle() * 100f);
		_countRoll = countRoll ?? ((minInclusive, maxInclusive) => minInclusive == maxInclusive ? minInclusive : Random.Shared.Next(minInclusive, maxInclusive + 1));
		_weightedRoll = weightedRoll ?? (exclusiveMax => Random.Shared.NextSingle() * exclusiveMax);
	}

	public WorldNpcGlobalDropResult CreateDrops(
		IWorldNpcObject? npc,
		Player? looter,
		WorldNpcDropModifiers dropModifiers,
		IReadOnlyList<Player>? groupMembers = null,
		int startIndex = 1)
	{
		// Java parity: services/drop/DropRegistrationService.addGlobalDrops default GLOBAL_DROP_DATA slice.
		if (npc == null || looter == null)
			return WorldNpcGlobalDropResult.Empty(startIndex);
		if (string.Equals(npc.AiName, "quest_use_item", StringComparison.OrdinalIgnoreCase))
			return WorldNpcGlobalDropResult.Empty(startIndex);
		if (HasGlobalNpcExclusion(npc))
			return WorldNpcGlobalDropResult.Empty(startIndex);

		return CreateDropsFromRules(GetGlobalDrops().Rules, npc, looter, dropModifiers, groupMembers, startIndex);
	}

	public WorldNpcGlobalDropResult CreateEventDrops(
		IReadOnlyList<GlobalDropRuleSummary> eventRules,
		IWorldNpcObject? npc,
		Player? looter,
		WorldNpcDropModifiers dropModifiers,
		IReadOnlyList<Player>? groupMembers = null,
		int startIndex = 1)
	{
		// Java parity: services/drop/DropRegistrationService.registerDrop event active-drop branch after default global drops.
		if (npc == null || looter == null || eventRules.Count == 0)
			return WorldNpcGlobalDropResult.Empty(startIndex);
		if (string.Equals(npc.AiName, "quest_use_item", StringComparison.OrdinalIgnoreCase))
			return WorldNpcGlobalDropResult.Empty(startIndex);
		if (HasGlobalNpcExclusion(npc) && !dropModifiers.IsDropNpcChest)
			return WorldNpcGlobalDropResult.Empty(startIndex);

		return CreateDropsFromRules(eventRules, npc, looter, dropModifiers, groupMembers, startIndex);
	}

	private WorldNpcGlobalDropResult CreateDropsFromRules(
		IReadOnlyList<GlobalDropRuleSummary> rules,
		IWorldNpcObject npc,
		Player looter,
		WorldNpcDropModifiers dropModifiers,
		IReadOnlyList<Player>? groupMembers,
		int startIndex)
	{
		var isAllowedDefaultGlobalDropNpc = IsAllowedDefaultGlobalDropNpc(npc, dropModifiers.IsDropNpcChest);
		var drops = new List<WorldNpcDropItem>();
		var index = startIndex;
		foreach (var rule in rules)
		{
			if (!isAllowedDefaultGlobalDropNpc && !rule.HasNpcRestriction)
				continue;

			var chance = CalculateEffectiveChance(rule, npc, dropModifiers);
			if (_chanceRoll() >= chance)
				continue;

			index = AddDropItems(index, drops, rule, npc, looter, groupMembers, dropModifiers);
		}

		return new WorldNpcGlobalDropResult(drops, index);
	}

	public IReadOnlyList<GlobalDropRuleSummary> GetApplicableRules(
		IWorldNpcObject npc,
		WorldNpcDropModifiers dropModifiers,
		bool isAllowedDefaultGlobalDropNpc)
	{
		// Java parity: services/drop/DropRegistrationService.addGlobalDrops rule prefilter.
		return GetGlobalDrops().Rules
			.Where(rule => (isAllowedDefaultGlobalDropNpc || rule.HasNpcRestriction) && IsRuleRestrictedToNpc(rule, npc, dropModifiers))
			.ToArray();
	}

	public bool HasGlobalNpcExclusion(IWorldNpcObject npc)
	{
		// Java parity: services/drop/DropRegistrationService.hasGlobalNpcExclusions.
		var exclusions = GetGlobalNpcExclusions();
		if (exclusions.IsEmpty)
			return false;

		return exclusions.NpcIds.Contains(npc.TemplateId)
			|| exclusions.NpcNames.Contains(npc.Template.Name)
			|| exclusions.NpcTemplateTypes.Contains(npc.Template.Type)
			|| exclusions.NpcTribes.Contains(npc.Template.Tribe)
			|| exclusions.NpcAbyssTypes.Contains(npc.Template.AbyssType);
	}

	public bool IsAllowedDefaultGlobalDropNpc(IWorldNpcObject npc, bool isChest)
	{
		// Java parity: services/drop/DropRegistrationService.isAllowedDefaultGlobalDropNpc, narrowed until siege/base spawn models exist.
		if (npc.Template.Level < 2
			&& !isChest
			&& npc.Position.WorldId is not 210010000 and not 220010000)
		{
			return false;
		}

		if (isChest)
			return false;

		var abyssType = npc.Template.AbyssType;
		return string.IsNullOrWhiteSpace(abyssType)
			|| string.Equals(abyssType, "NONE", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(abyssType, "DEFENDER", StringComparison.OrdinalIgnoreCase);
	}

	public float CalculateEffectiveChance(
		GlobalDropRuleSummary rule,
		IWorldNpcObject npc,
		WorldNpcDropModifiers dropModifiers)
	{
		// Java parity: services/drop/DropRegistrationService.calculateEffectiveChance.
		var chance = rule.Chance;
		if (rule.DynamicChance)
			chance *= GetRankModifier(npc.Template.Rank) * GetRatingModifier(npc.Template.Rating);
		return dropModifiers.CalculateDropChance(chance, rule.UseLevelBasedChanceReduction);
	}

	public IReadOnlyList<GlobalDropItemSummary> CollectAllowedDrops(
		GlobalDropRuleSummary rule,
		IWorldNpcObject npc,
		WorldNpcDropModifiers dropModifiers)
	{
		// Java parity: services/drop/DropRegistrationService.collectAllowedDrops.
		if (!IsRuleRestrictedToNpc(rule, npc, dropModifiers))
			return Array.Empty<GlobalDropItemSummary>();

		var items = GetItemTemplates();
		return rule.Items
			.Where(
				item =>
				{
					var itemTemplate = items.GetItemTemplate(item.ItemId);
					if (itemTemplate == null)
						return false;
					if (!string.Equals(itemTemplate.Race, "PC_ALL", StringComparison.OrdinalIgnoreCase)
						&& !string.Equals(itemTemplate.Race, dropModifiers.DropRace, StringComparison.OrdinalIgnoreCase))
					{
						return false;
					}

					var levelDiff = npc.Template.Level - itemTemplate.Level;
					return levelDiff >= rule.MinDiff && levelDiff <= rule.MaxDiff;
				})
			.ToArray();
	}

	public IReadOnlyList<GlobalDropItemSummary> CollectDrops(
		GlobalDropRuleSummary rule,
		IWorldNpcObject npc,
		WorldNpcDropModifiers dropModifiers)
	{
		// Java parity: services/drop/DropRegistrationService.collectDrops, including max_drop weighted selection.
		var maxDrops = dropModifiers.MaxDropsPerGroup ?? rule.MaxDropRule;
		var drops = CollectAllowedDrops(rule, npc, dropModifiers).ToList();
		if (maxDrops <= 0)
			return Array.Empty<GlobalDropItemSummary>();
		if (drops.Count <= maxDrops)
			return drops;

		var selected = new List<GlobalDropItemSummary>();
		for (var i = 0; i < maxDrops && drops.Count > 0; i++)
		{
			var item = SelectWeightedDrop(drops);
			if (item != null)
				selected.Add(item);
		}

		return selected;
	}

	private bool IsRuleRestrictedToNpc(
		GlobalDropRuleSummary rule,
		IWorldNpcObject npc,
		WorldNpcDropModifiers dropModifiers)
	{
		return CheckRestrictionRace(rule, dropModifiers.DropRace)
			&& CheckStringRestriction(rule.WorldTypes, string.Empty)
			&& CheckIntRestriction(rule.MapIds, npc.Position.WorldId)
			&& CheckStringRestriction(rule.Ratings, npc.Template.Rating)
			&& CheckStringRestriction(rule.Races, npc.Template.Race)
			&& CheckStringRestriction(rule.Tribes, npc.Template.Tribe)
			&& CheckIntRestriction(rule.NpcIds, npc.TemplateId)
			&& CheckNpcNameRestriction(rule.NpcNames, npc.Template.Name)
			&& CheckStringRestriction(rule.NpcGroups, npc.Template.GroupDrop)
			&& CheckStringRestriction(rule.Zones, string.Empty)
			&& !rule.ExcludedNpcIds.Contains(npc.TemplateId);
	}

	private static bool CheckRestrictionRace(GlobalDropRuleSummary rule, string dropRace)
	{
		if (string.IsNullOrWhiteSpace(rule.RestrictionRace))
			return true;

		if (string.Equals(dropRace, "ASMODIANS", StringComparison.OrdinalIgnoreCase)
			&& string.Equals(rule.RestrictionRace, "ELYOS", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (string.Equals(dropRace, "ELYOS", StringComparison.OrdinalIgnoreCase)
			&& string.Equals(rule.RestrictionRace, "ASMODIANS", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return true;
	}

	private static bool CheckNpcNameRestriction(IReadOnlyList<GlobalDropNpcNameSummary> names, string npcName)
	{
		if (names.Count == 0)
			return true;

		return names.Any(
			name => name.Function.ToUpperInvariant() switch
			{
				"CONTAINS" => npcName.Contains(name.Value, StringComparison.OrdinalIgnoreCase),
				"END_WITH" => npcName.EndsWith(name.Value, StringComparison.OrdinalIgnoreCase),
				"START_WITH" => npcName.StartsWith(name.Value, StringComparison.OrdinalIgnoreCase),
				"EQUALS" => string.Equals(npcName, name.Value, StringComparison.OrdinalIgnoreCase),
				_ => false,
			});
	}

	private static bool CheckStringRestriction(IReadOnlySet<string> allowedValues, string value)
	{
		return allowedValues.Count == 0 || allowedValues.Contains(value);
	}

	private static bool CheckIntRestriction(IReadOnlySet<int> allowedValues, int value)
	{
		return allowedValues.Count == 0 || allowedValues.Contains(value);
	}

	private GlobalDropTable GetGlobalDrops()
	{
		return _globalDrops ?? _runtimeContext?.DataManager?.StaticData.GlobalDrops ?? EmptyGlobalDrops;
	}

	private ItemTemplateTable GetItemTemplates()
	{
		return _itemTemplates ?? _runtimeContext?.DataManager?.StaticData.ItemTemplates ?? EmptyItemTemplates;
	}

	private GlobalNpcExclusionTable GetGlobalNpcExclusions()
	{
		return _globalNpcExclusions ?? _runtimeContext?.DataManager?.StaticData.GlobalNpcExclusions ?? EmptyExclusions;
	}

	private int AddDropItems(
		int index,
		List<WorldNpcDropItem> droppedItems,
		GlobalDropRuleSummary rule,
		IWorldNpcObject npc,
		Player looter,
		IReadOnlyList<Player>? groupMembers,
		WorldNpcDropModifiers dropModifiers)
	{
		// Java parity: services/drop/DropRegistrationService.addDropItems.
		var drops = CollectDrops(rule, npc, dropModifiers);
		if (drops.Count == 0)
			return index;

		if (rule.MemberLimit > 1 && looter.IsInTeam && groupMembers is { Count: > 0 })
		{
			var distributedItems = 0;
			foreach (var member in groupMembers)
			{
				foreach (var drop in drops)
				{
					droppedItems.Add(CreateDropItem(
						index++,
						npc.ObjectId,
						drop,
						GetItemCount(drop, npc),
						new HashSet<int> { member.ObjectId },
						isDistributeItem: true));
				}

				if (++distributedItems >= rule.MemberLimit)
					break;
			}

			return index;
		}

		foreach (var drop in drops)
		{
			droppedItems.Add(CreateDropItem(
				index++,
				npc.ObjectId,
				drop,
				GetItemCount(drop, npc),
				playerObjectIds: null,
				isDistributeItem: false));
		}

		return index;
	}

	private WorldNpcDropItem CreateDropItem(
		int index,
		int npcObjectId,
		GlobalDropItemSummary drop,
		long count,
		IReadOnlySet<int>? playerObjectIds,
		bool isDistributeItem)
	{
		// Java parity: services/drop/DropRegistrationService.regDropItem and member-limit distributed DropItem construction.
		return new WorldNpcDropItem(
			index,
			drop.ItemId,
			count,
			playerObjectIds,
			NpcObjectId: npcObjectId,
			IsDistributeItem: isDistributeItem);
	}

	private long GetItemCount(GlobalDropItemSummary item, IWorldNpcObject npc)
	{
		// Java parity: services/drop/DropRegistrationService.getItemCount.
		long count = _countRoll(item.MinCount, item.MaxCount);
		if (item.ItemId == InventoryItemFactory.KinahItemId)
		{
			var rankRating = GetRankModifier(npc.Template.Rank) * GetRatingModifier(npc.Template.Rating);
			count = (long)(count * npc.Template.Level * Math.Pow(rankRating, 6));
		}

		return count;
	}

	private GlobalDropItemSummary? SelectWeightedDrop(List<GlobalDropItemSummary> drops)
	{
		// Java parity: model/Chance.selectElement(remove=true).
		var sumOfChances = drops.Sum(drop => drop.Chance);
		if (sumOfChances <= 0)
			return null;

		var randomChance = _weightedRoll(sumOfChances);
		float luck = 0;
		for (var i = 0; i < drops.Count; i++)
		{
			var drop = drops[i];
			luck += drop.Chance;
			if (randomChance > luck)
				continue;

			drops.RemoveAt(i);
			return drop;
		}

		return null;
	}

	private static float GetRankModifier(string rank)
	{
		return rank.ToUpperInvariant() switch
		{
			"NOVICE" => 0.9f,
			"DISCIPLINED" => 1f,
			"SEASONED" => 1.05f,
			"EXPERT" => 1.1f,
			"VETERAN" => 1.15f,
			"MASTER" => 1.2f,
			_ => 1f,
		};
	}

	private static float GetRatingModifier(string rating)
	{
		return rating.ToUpperInvariant() switch
		{
			"JUNK" => 0.5f,
			"NORMAL" => 1f,
			"ELITE" => 1.3f,
			"HERO" => 1.8f,
			"LEGENDARY" => 2f,
			_ => 1f,
		};
	}
}

public sealed record WorldNpcGlobalDropResult(
	IReadOnlyList<WorldNpcDropItem> Drops,
	int NextIndex)
{
	public static WorldNpcGlobalDropResult Empty(int nextIndex)
	{
		return new WorldNpcGlobalDropResult(Array.Empty<WorldNpcDropItem>(), nextIndex);
	}
}
