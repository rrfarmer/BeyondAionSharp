using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class DecomposeService
{
	public const int UsageDelayMilliseconds = 3000;

	private static readonly IReadOnlyDictionary<string, int[]> ChunkEarthByRace = new Dictionary<string, int[]>(StringComparer.Ordinal)
	{
		["ASMODIANS"] =
		[
			152000051, 152000052, 152000053, 152000054, 152000055, 152000056, 152000057, 152000058, 152000059, 152000061, 152000062, 152000063,
			152000101, 152000102, 152000104, 152000107, 152000113, 152000201, 152000202, 152000204, 152000207, 152000214, 152000451, 152000453,
			152000455, 152000457, 152000459, 152000461, 152000463, 152000465, 152000468, 152000470, 152000551, 152000552, 152000553, 152000554,
			152000556, 152000651, 152000652, 152000653, 152000654, 152000656, 152000751, 152000752, 152000753, 152000754, 152000755, 152000756,
			152000757, 152000758, 152000759, 152000760, 152000762, 152000763, 152000851, 152000852, 152000853, 152000854, 152000855, 152000856,
			152000857, 152000858, 152000860, 152000861, 152001051, 152001052, 152001053, 152001055, 152001056,
		],
		["ELYOS"] =
		[
			152000001, 152000002, 152000003, 152000004, 152000005, 152000006, 152000007, 152000008, 152000009, 152000010, 152000011, 152000012,
			152000101, 152000102, 152000104, 152000107, 152000113, 152000201, 152000202, 152000204, 152000207, 152000214, 152000401, 152000403,
			152000405, 152000407, 152000409, 152000411, 152000413, 152000415, 152000417, 152000419, 152000501, 152000502, 152000503, 152000504,
			152000505, 152000601, 152000602, 152000603, 152000604, 152000605, 152000701, 152000702, 152000703, 152000704, 152000705, 152000706,
			152000707, 152000708, 152000709, 152000710, 152000711, 152000712, 152000801, 152000802, 152000803, 152000804, 152000805, 152000806,
			152000807, 152000808, 152000809, 152000810, 152001001, 152001002, 152001003, 152001004, 152001005,
		],
	};

	private static readonly IReadOnlyDictionary<string, int[]> ChunkSandByRace = new Dictionary<string, int[]>(StringComparer.Ordinal)
	{
		["ASMODIANS"] =
		[
			152000452, 152000454, 152000301, 152000302, 152000303, 152000456, 152000458, 152000103, 152000203, 152000304, 152000305, 152000306,
			152000460, 152000462, 152000105, 152000205, 152000307, 152000309, 152000311, 152000464, 152000466, 152000108, 152000208, 152000313,
			152000315, 152000317, 152000469, 152000471, 152000114, 152000215, 152000320, 152000322, 152000324,
		],
		["ELYOS"] =
		[
			152000402, 152000404, 152000301, 152000302, 152000303, 152000406, 152000408, 152000103, 152000203, 152000304, 152000305, 152000306,
			152000410, 152000412, 152000105, 152000205, 152000307, 152000309, 152000311, 152000414, 152000416, 152000108, 152000208, 152000313,
			152000315, 152000317, 152000418, 152000420, 152000114, 152000215, 152000320, 152000322, 152000324,
		],
	};

	private static readonly IReadOnlyDictionary<string, int[]> PremiumOphidanRecipeByRace = new Dictionary<string, int[]>(StringComparer.Ordinal)
	{
		["ASMODIANS"] =
		[
			152230698, 152230699, 152230700, 152230701, 152230702, 152230703, 152230704, 152230759, 152230760, 152230761, 152230762, 152230763,
			152230764, 152230839, 152230840, 152230841, 152230842, 152230843, 152230844, 152230845, 152231021, 152231022, 152231023, 152231107,
			152231108, 152231253, 152231254, 152231255, 152231256, 152231257, 152231258, 152231313, 152231314, 152231315, 152231316, 152231317,
			152231318, 152231385, 152231386, 152231387, 152231388, 152231389, 152231390, 152231403, 152231404, 152231405, 152231406, 152231407,
			152231408, 152231421, 152231422, 152231423, 152231424, 152231425, 152231426, 152231439, 152231440, 152231441, 152231442, 152231443,
			152231444, 152231566,
		],
		["ELYOS"] =
		[
			152220709, 152220710, 152220711, 152220712, 152220713, 152220714, 152220715, 152220770, 152220771, 152220772, 152220773, 152220774,
			152220775, 152220850, 152220851, 152220852, 152220853, 152220854, 152220855, 152220856, 152221032, 152221033, 152221034, 152221118,
			152221119, 152221264, 152221265, 152221266, 152221267, 152221268, 152221269, 152221324, 152221325, 152221326, 152221327, 152221328,
			152221329, 152221396, 152221397, 152221398, 152221399, 152221400, 152221401, 152221414, 152221415, 152221416, 152221417, 152221418,
			152221419, 152221432, 152221433, 152221434, 152221435, 152221436, 152221437, 152221450, 152221451, 152221452, 152221453, 152221454,
			152221455, 152221576,
		],
	};

	private static readonly int[] ChunkRock =
	[
		152000104, 152000107, 152000113, 152000204, 152000207, 152000214, 152000307, 152000309, 152000311, 152000313, 152000315, 152000317,
		152000320, 152000322, 152000324,
	];

	private static readonly int[] ChunkGemstone = [152000112, 152000116, 152000212, 152000213, 152000217, 152000326, 152000327, 152000328];
	private static readonly int[] Scrolls = [164000073, 164000134, 164000076, 164000079, 164000122, 164000131, 164000118];
	private static readonly int[] Potion = [162000045, 162000079, 162000016, 162000021, 162000027, 162000023];
	private static readonly int[] LesserPotions = [162000003, 162000008, 162000042, 162000022, 162000013, 162000018, 162000047];
	private static readonly int[] Potion50 = [162000075, 162000076, 162000077, 162000078, 162000079, 162000080, 162000081];
	private static readonly int[] IllusionGodstones =
	[
		168000161, 168000162, 168000163, 168000164, 168000165, 168000166, 168000167, 168000168, 168000169, 168000170, 168000171, 168000172,
		168000173, 168000174, 168000175, 168000176, 168000177,
	];

	public static DecomposeCanActResult CanAct(Player player, InventoryItem sourceItem, StaticData staticData)
	{
		// Java parity: model/templates/item/actions/DecomposeAction.canAct.
		var itemGroups = staticData.DecomposableItems.GetInfoByItemId(sourceItem.ItemId);
		if (itemGroups == null || itemGroups.Count == 0)
		{
			return staticData.DecomposableItems.GetSelectableItems(sourceItem.ItemId) != null
				? DecomposeCanActResult.Success(selectable: true)
				: DecomposeCanActResult.Failed(DecomposeFailure.CannotDecompose);
		}

		return InventoryCapacity.HasFreeCubeSlot(player)
			? DecomposeCanActResult.Success(selectable: false)
			: DecomposeCanActResult.Failed(DecomposeFailure.InventoryFull);
	}

	public static IReadOnlyList<ResultedItemSummary>? GetSelectableItems(Player player, DecomposableItemTable decomposableItems, int itemId)
	{
		var selectableItems = decomposableItems.GetSelectableItems(itemId);
		return selectableItems == null
			? null
			: selectableItems.Where(item => IsObtainableFor(player, item)).ToArray();
	}

	public static DecomposeRewardPlan CreateSelectableRewardPlan(
		Player player,
		DecomposableItemTable decomposableItems,
		int itemId,
		int index,
		Func<int, int, int>? rollInclusive = null)
	{
		// Java parity: network/aion/clientpackets/CM_SELECT_DECOMPOSABLE.runImpl.
		var selectableItems = GetSelectableItems(player, decomposableItems, itemId);
		if (selectableItems == null || index < 0 || index >= selectableItems.Count)
			return DecomposeRewardPlan.Failed(DecomposeFailure.CannotDecompose);

		var selectedItem = selectableItems[index];
		return DecomposeRewardPlan.Success(
			[new DecomposeReward(selectedItem.ItemId, RollInclusive(selectedItem.MinCount, selectedItem.MaxCount, rollInclusive))]);
	}

	public static DecomposeRewardPlan CreateNormalRewardPlan(
		Player player,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate,
		StaticData staticData,
		Func<double>? rollChance = null,
		Func<int, int, int>? rollInclusive = null)
	{
		// Java parity: model/templates/item/actions/DecomposeAction.act reward selection.
		var canAct = CanAct(player, sourceItem, staticData);
		if (!canAct.Succeeded)
			return DecomposeRewardPlan.Failed(canAct.Failure);

		var itemGroups = staticData.DecomposableItems.GetInfoByItemId(sourceItem.ItemId);
		if (itemGroups == null)
			return DecomposeRewardPlan.Failed(DecomposeFailure.CannotDecompose);

		var playerLevel = Math.Max(1, staticData.PlayerExperienceTable.GetLevelForExp(player.Exp));
		var levelSuitableItems = itemGroups
			.Where(collection => collection.MinLevel <= playerLevel && collection.MaxLevel >= playerLevel)
			.ToArray();
		var selectedCollection = SelectCollection(levelSuitableItems, rollChance);
		if (selectedCollection == null)
			return DecomposeRewardPlan.Failed(DecomposeFailure.Failed);

		var fixedRewards = selectedCollection.Items
			.Where(item => IsObtainableFor(player, item))
			.Select(item => new DecomposeReward(item.ItemId, RollInclusive(item.MinCount, item.MaxCount, rollInclusive)))
			.ToList();
		if (selectedCollection.RandomItems.Count == 0 && fixedRewards.Count == 0)
			return DecomposeRewardPlan.Failed(DecomposeFailure.Failed);

		foreach (var randomItem in selectedCollection.RandomItems)
		{
			var randomItemId = ResolveRandomRewardItemId(randomItem.Type, player, playerLevel, sourceTemplate, staticData.ItemTemplates, rollInclusive);
			if (randomItemId == 0)
				continue;

			fixedRewards.Add(new DecomposeReward(randomItemId, RollInclusive(randomItem.MinCount, randomItem.MaxCount, rollInclusive)));
		}

		return DecomposeRewardPlan.Success(fixedRewards);
	}

	public static bool IsObtainableFor(Player player, ResultedItemSummary item)
	{
		// Java parity: model/templates/rewards/ResultedItem.isObtainableFor.
		var raceMatches = string.Equals(item.Race, "PC_ALL", StringComparison.Ordinal)
			|| string.Equals(item.Race, player.Race, StringComparison.Ordinal);
		var classMatches = item.PlayerClasses.Count == 0 || item.PlayerClasses.Contains(player.PlayerClass);
		return raceMatches && classMatches;
	}

	private static ExtractedItemsCollectionSummary? SelectCollection(
		IReadOnlyList<ExtractedItemsCollectionSummary> collections,
		Func<double>? rollChance)
	{
		if (collections.Count == 0)
			return null;
		var totalChance = collections.Sum(collection => Math.Max(0d, collection.Chance));
		if (totalChance <= 0)
			return collections[0];

		var roll = Math.Clamp(rollChance?.Invoke() ?? Random.Shared.NextDouble(), 0d, 0.999999999999d) * totalChance;
		var accumulated = 0d;
		foreach (var collection in collections)
		{
			accumulated += Math.Max(0d, collection.Chance);
			if (roll < accumulated)
				return collection;
		}

		return collections[^1];
	}

	private static int ResolveRandomRewardItemId(
		string randomType,
		Player player,
		int playerLevel,
		ItemTemplateSummary sourceTemplate,
		ItemTemplateTable itemTemplates,
		Func<int, int, int>? rollInclusive)
	{
		return randomType switch
		{
			"ENCHANTMENT" => ResolveValidatedRange(
				166000191 + (int)MathF.Floor(sourceTemplate.Level / 100f + 0.5f),
				166000191 + (int)MathF.Floor(sourceTemplate.Level / 100f + 0.5f) + 3,
				itemTemplates,
				rollInclusive),
			"MANASTONE" => ResolveManastoneReward(GetManastoneLevel(sourceTemplate.Level, playerLevel), itemTemplates, quality: null, rollInclusive),
			var type when type.StartsWith("MANASTONE_", StringComparison.Ordinal) => ResolveManastoneReward(
				GetRandomTypeLevel(type),
				itemTemplates,
				GetManastoneQuality(type),
				rollInclusive),
			var type when type.StartsWith("SPECIAL_MANASTONE_", StringComparison.Ordinal) => ResolveSpecialManastoneReward(
				GetRandomTypeLevel(type),
				itemTemplates,
				GetSpecialManastoneQuality(type),
				rollInclusive),
			"CHUNK_EARTH" => ResolveRaceTableReward(ChunkEarthByRace, player.Race, rollInclusive),
			"CHUNK_SAND" => ResolveRaceTableReward(ChunkSandByRace, player.Race, rollInclusive),
			"PREMIUM_OPHIDAN_RECIPE" => ResolveRaceTableReward(PremiumOphidanRecipeByRace, player.Race, rollInclusive),
			"CHUNK_ROCK" => RollFrom(ChunkRock, rollInclusive),
			"CHUNK_GEMSTONE" => RollFrom(ChunkGemstone, rollInclusive),
			"SCROLLS" => RollFrom(Scrolls, rollInclusive),
			"POTION" => RollFrom(Potion, rollInclusive),
			"LESSER_POTIONS" => RollFrom(LesserPotions, rollInclusive),
			"POTION_50" => RollFrom(Potion50, rollInclusive),
			"ILLUSION_GODSTONE" => RollFrom(IllusionGodstones, rollInclusive),
			"ANCIENTITEMS" => ResolveValidatedRange(186000051, 186000066, itemTemplates, rollInclusive),
			"ANCIENT_CROWN" => ResolveValidatedRange(186000051, 186000054, itemTemplates, rollInclusive),
			"ANCIENT_GOBLET" => ResolveValidatedRange(186000055, 186000058, itemTemplates, rollInclusive),
			"ANCIENT_SEAL" => ResolveValidatedRange(186000059, 186000062, itemTemplates, rollInclusive),
			"ANCIENT_ICON" => ResolveValidatedRange(186000063, 186000066, itemTemplates, rollInclusive),
			_ => 0,
		};
	}

	private static int ResolveValidatedRange(int min, int max, ItemTemplateTable itemTemplates, Func<int, int, int>? rollInclusive)
	{
		for (var i = 0; i < 50; i++)
		{
			var itemId = RollInclusive(min, max, rollInclusive);
			if (itemTemplates.GetItemTemplate(itemId) != null)
				return itemId;
		}

		return 0;
	}

	private static int ResolveManastoneReward(
		int itemLevel,
		ItemTemplateTable itemTemplates,
		string? quality,
		Func<int, int, int>? rollInclusive)
	{
		var stones = itemTemplates.Templates
			.Where(template => template.ItemGroup == "MANASTONE"
				&& template.Level == itemLevel
				&& !template.Name.Contains(" MP ", StringComparison.Ordinal)
				&& (quality == null
					? template.Quality != "LEGEND"
					: template.Quality == quality))
			.ToArray();
		return RollTemplateId(stones, rollInclusive);
	}

	private static int ResolveSpecialManastoneReward(
		int itemLevel,
		ItemTemplateTable itemTemplates,
		string quality,
		Func<int, int, int>? rollInclusive)
	{
		var stones = itemTemplates.Templates
			.Where(template => template.ItemGroup == "SPECIAL_MANASTONE"
				&& template.Level == itemLevel
				&& template.Quality == quality
				&& !template.Name.Contains(" MP ", StringComparison.Ordinal))
			.ToArray();
		return RollTemplateId(stones, rollInclusive);
	}

	private static int RollTemplateId(IReadOnlyList<ItemTemplateSummary> templates, Func<int, int, int>? rollInclusive)
	{
		return templates.Count == 0 ? 0 : templates[RollInclusive(0, templates.Count - 1, rollInclusive)].TemplateId;
	}

	private static int ResolveRaceTableReward(
		IReadOnlyDictionary<string, int[]> rewardsByRace,
		string race,
		Func<int, int, int>? rollInclusive)
	{
		return rewardsByRace.TryGetValue(race, out var rewards) ? RollFrom(rewards, rollInclusive) : 0;
	}

	private static int RollFrom(IReadOnlyList<int> itemIds, Func<int, int, int>? rollInclusive)
	{
		return itemIds.Count == 0 ? 0 : itemIds[RollInclusive(0, itemIds.Count - 1, rollInclusive)];
	}

	private static int RollInclusive(int min, int max, Func<int, int, int>? rollInclusive)
	{
		if (max <= min)
			return min;

		return rollInclusive?.Invoke(min, max) ?? Random.Shared.Next(min, max + 1);
	}

	private static int GetManastoneLevel(int sourceItemLevel, int playerLevel)
	{
		if (sourceItemLevel % 10 == 0)
			return sourceItemLevel;

		var level = sourceItemLevel == 1 ? playerLevel : sourceItemLevel;
		return (int)Math.Ceiling(level / 10f) * 10;
	}

	private static int GetRandomTypeLevel(string randomType)
	{
		var lastUnderscore = randomType.LastIndexOf('_');
		return lastUnderscore >= 0 && int.TryParse(randomType[(lastUnderscore + 1)..], out var level) ? level : 70;
	}

	private static string? GetManastoneQuality(string randomType)
	{
		if (randomType.Contains("_RARE_", StringComparison.Ordinal))
			return "RARE";
		if (randomType.Contains("_LEGEND_", StringComparison.Ordinal))
			return "LEGEND";
		return "COMMON";
	}

	private static string GetSpecialManastoneQuality(string randomType)
	{
		if (randomType.Contains("_RARE_", StringComparison.Ordinal))
			return "RARE";
		if (randomType.Contains("_LEGEND_", StringComparison.Ordinal))
			return "LEGEND";
		if (randomType.Contains("_UNIQUE_", StringComparison.Ordinal))
			return "UNIQUE";
		if (randomType.Contains("_EPIC_", StringComparison.Ordinal))
			return "EPIC";
		return "COMMON";
	}
}

public sealed record DecomposeCanActResult(DecomposeFailure Failure, bool IsSelectable)
{
	public bool Succeeded => Failure == DecomposeFailure.None;

	public static DecomposeCanActResult Success(bool selectable)
	{
		return new DecomposeCanActResult(DecomposeFailure.None, selectable);
	}

	public static DecomposeCanActResult Failed(DecomposeFailure failure)
	{
		return new DecomposeCanActResult(failure, IsSelectable: false);
	}
}

public sealed record DecomposeRewardPlan(DecomposeFailure Failure, IReadOnlyList<DecomposeReward> Rewards)
{
	public bool Succeeded => Failure == DecomposeFailure.None;

	public static DecomposeRewardPlan Success(IReadOnlyList<DecomposeReward> rewards)
	{
		return new DecomposeRewardPlan(DecomposeFailure.None, rewards);
	}

	public static DecomposeRewardPlan Failed(DecomposeFailure failure)
	{
		return new DecomposeRewardPlan(failure, Array.Empty<DecomposeReward>());
	}
}

public sealed record DecomposeReward(int ItemId, int Count);

public enum DecomposeFailure
{
	None,
	CannotDecompose,
	InventoryFull,
	Failed,
}
