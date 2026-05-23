using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcGlobalDropServiceTests
{
	[Fact]
	public void GetApplicableRules_AppliesJavaRuleRestrictions()
	{
		var unrestricted = CreateRule("default");
		var elyosOnly = CreateRule("elyos", restrictionRace: "ELYOS");
		var wrongMap = CreateRule("wrong-map", mapIds: [220010000]);
		var excludedNpc = CreateRule("excluded", excludedNpcIds: [210001]);
		var npcName = CreateRule(
			"name",
			npcNames: [new GlobalDropNpcNameSummary("CONTAINS", "spirit")]);
		var service = new WorldNpcGlobalDropService(
			new GlobalDropTable([unrestricted, elyosOnly, wrongMap, excludedNpc, npcName]),
			new ItemTemplateTable([]));
		var npc = CreateNpc(210001, "wind_spirit", level: 10, rank: "NOVICE", rating: "NORMAL", race: "MAGICALMONSTER");
		var modifiers = new WorldNpcDropModifiers("ELYOS");

		var defaultRules = service.GetApplicableRules(npc, modifiers, isAllowedDefaultGlobalDropNpc: true);
		var restrictedRules = service.GetApplicableRules(npc, modifiers, isAllowedDefaultGlobalDropNpc: false);

		Assert.Equal(["default", "elyos", "name"], defaultRules.Select(rule => rule.RuleName).ToArray());
		Assert.Equal(["name"], restrictedRules.Select(rule => rule.RuleName).ToArray());
	}

	[Fact]
	public void CollectAllowedDrops_FiltersByRuleRestrictionsRaceAndLevelDiff()
	{
		var service = new WorldNpcGlobalDropService(
			new GlobalDropTable([]),
			new ItemTemplateTable(
			[
				CreateItem(1001, level: 9, race: "PC_ALL"),
				CreateItem(1002, level: 10, race: "ASMODIANS"),
				CreateItem(1003, level: 20, race: "ELYOS"),
			]));
		var rule = CreateRule(
			"items",
			minDiff: -2,
			maxDiff: 2,
			items:
			[
				new GlobalDropItemSummary(1001, 1, 1, 100f),
				new GlobalDropItemSummary(1002, 1, 1, 100f),
				new GlobalDropItemSummary(1003, 1, 1, 100f),
			]);
		var npc = CreateNpc(210001, "loot_npc", level: 10);

		var drops = service.CollectAllowedDrops(rule, npc, new WorldNpcDropModifiers("ELYOS"));

		var drop = Assert.Single(drops);
		Assert.Equal(1001, drop.ItemId);
	}

	[Fact]
	public void CalculateEffectiveChance_AppliesDynamicRankRatingAndDropModifiers()
	{
		var service = new WorldNpcGlobalDropService(new GlobalDropTable([]), new ItemTemplateTable([]));
		var rule = CreateRule("dynamic", chance: 10f, dynamicChance: true, useLevelReduction: true);
		var npc = CreateNpc(210001, "elite_npc", level: 10, rank: "VETERAN", rating: "ELITE");
		var modifiers = new WorldNpcDropModifiers("ELYOS", BoostDropRate: 2f, ReductionDropRate: 0.5f);

		var chance = service.CalculateEffectiveChance(rule, npc, modifiers);

		Assert.Equal(14.95f, chance, precision: 4);
	}

	[Fact]
	public void CollectDrops_SelectsWeightedMaxDrop()
	{
		var service = new WorldNpcGlobalDropService(
			new GlobalDropTable([]),
			new ItemTemplateTable(
			[
				CreateItem(1001, level: 10, race: "PC_ALL"),
				CreateItem(1002, level: 10, race: "PC_ALL"),
			]),
			weightedRoll: _ => 1.5f);
		var rule = CreateRule(
			"weighted",
			maxDropRule: 1,
			items:
			[
				new GlobalDropItemSummary(1001, 1, 1, 1f),
				new GlobalDropItemSummary(1002, 1, 1, 9f),
			]);
		var npc = CreateNpc(210001, "loot_npc", level: 10);

		var drops = service.CollectDrops(rule, npc, new WorldNpcDropModifiers("ELYOS"));

		var drop = Assert.Single(drops);
		Assert.Equal(1002, drop.ItemId);
	}

	[Fact]
	public void CreateDrops_RollsRuleChanceCountsAndKinahScaling()
	{
		var service = new WorldNpcGlobalDropService(
			new GlobalDropTable(
			[
				CreateRule(
					"global",
					chance: 100f,
					maxDropRule: 2,
					items:
					[
						new GlobalDropItemSummary(InventoryItemFactory.KinahItemId, 3, 3, 100f),
						new GlobalDropItemSummary(1001, 2, 2, 100f),
					]),
			]),
			new ItemTemplateTable(
			[
				CreateItem(InventoryItemFactory.KinahItemId, level: 10, race: "PC_ALL"),
				CreateItem(1001, level: 10, race: "PC_ALL"),
			]),
			chanceRoll: () => 0f);
		var npc = CreateNpc(210001, "loot_npc", level: 10, rank: "DISCIPLINED", rating: "NORMAL");
		var looter = new Player { ObjectId = 1001, Race = "ELYOS", Level = 10 };

		var result = service.CreateDrops(npc, looter, new WorldNpcDropModifiers("ELYOS"), startIndex: 5);

		Assert.Equal(7, result.NextIndex);
		Assert.Collection(
			result.Drops,
			drop =>
			{
				Assert.Equal(5, drop.Index);
				Assert.Equal(InventoryItemFactory.KinahItemId, drop.ItemId);
				Assert.Equal(30, drop.Count);
				Assert.Equal(npc.ObjectId, drop.NpcObjectId);
				Assert.True(drop.CanViewDropItem(looter.ObjectId));
			},
			drop =>
			{
				Assert.Equal(6, drop.Index);
				Assert.Equal(1001, drop.ItemId);
				Assert.Equal(2, drop.Count);
			});
	}

	[Fact]
	public void CreateDrops_DistributesMemberLimitedTeamDrops()
	{
		var service = new WorldNpcGlobalDropService(
			new GlobalDropTable(
			[
				CreateRule(
					"member-limited",
					memberLimit: 2,
					items: [new GlobalDropItemSummary(1001, 1, 1, 100f)]),
			]),
			new ItemTemplateTable([CreateItem(1001, level: 10, race: "PC_ALL")]),
			chanceRoll: () => 0f);
		var npc = CreateNpc(210001, "loot_npc", level: 10);
		var looter = new Player { ObjectId = 1001, Race = "ELYOS", Level = 10, TeamMembership = PlayerTeamMembership.Group };
		var members = new[]
		{
			looter,
			new Player { ObjectId = 1002, Race = "ELYOS", Level = 10, TeamMembership = PlayerTeamMembership.Group },
			new Player { ObjectId = 1003, Race = "ELYOS", Level = 10, TeamMembership = PlayerTeamMembership.Group },
		};

		var result = service.CreateDrops(npc, looter, new WorldNpcDropModifiers("ELYOS"), members);

		Assert.Equal(3, result.NextIndex);
		Assert.Equal([1001, 1002], result.Drops.Select(drop => Assert.Single(drop.PlayerObjectIds!)).ToArray());
		Assert.All(result.Drops, drop => Assert.True(drop.IsDistributeItem));
	}

	[Fact]
	public void HasGlobalNpcExclusion_UsesNpcIdentityTypeAndTribe()
	{
		var service = new WorldNpcGlobalDropService(
			new GlobalDropTable([]),
			new ItemTemplateTable([]),
			new GlobalNpcExclusionTable(
				new HashSet<int> { 210001 },
				new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "blocked_name" },
				new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SUMMON_PET" },
				new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "PET" },
				new HashSet<string>(StringComparer.OrdinalIgnoreCase)));

		Assert.True(service.HasGlobalNpcExclusion(CreateNpc(210001, "normal")));
		Assert.True(service.HasGlobalNpcExclusion(CreateNpc(210002, "blocked_name")));
		Assert.True(service.HasGlobalNpcExclusion(CreateNpc(210003, "normal", type: "SUMMON_PET")));
		Assert.True(service.HasGlobalNpcExclusion(CreateNpc(210004, "normal", tribe: "PET")));
		Assert.False(service.HasGlobalNpcExclusion(CreateNpc(210005, "normal")));
	}

	[Fact]
	public void IsAllowedDefaultGlobalDropNpc_AppliesChestAndMissingStatsLevelGuard()
	{
		var service = new WorldNpcGlobalDropService(new GlobalDropTable([]), new ItemTemplateTable([]));

		Assert.False(service.IsAllowedDefaultGlobalDropNpc(CreateNpc(210001, "chest", level: 10), isChest: true));
		Assert.False(service.IsAllowedDefaultGlobalDropNpc(CreateNpc(210002, "low", level: 1, worldId: 210020000), isChest: false));
		Assert.True(service.IsAllowedDefaultGlobalDropNpc(CreateNpc(210003, "poeta-low", level: 1, worldId: 210010000), isChest: false));
		Assert.True(service.IsAllowedDefaultGlobalDropNpc(CreateNpc(210004, "normal", level: 10), isChest: false));
	}

	private static GlobalDropRuleSummary CreateRule(
		string name,
		float chance = 100f,
		bool dynamicChance = false,
		int minDiff = -99,
		int maxDiff = 99,
		int memberLimit = 1,
		int maxDropRule = 1,
		string restrictionRace = "",
		bool useLevelReduction = false,
		IReadOnlyList<GlobalDropItemSummary>? items = null,
		IEnumerable<int>? mapIds = null,
		IEnumerable<int>? excludedNpcIds = null,
		IReadOnlyList<GlobalDropNpcNameSummary>? npcNames = null)
	{
		return new GlobalDropRuleSummary(
			name,
			chance,
			dynamicChance,
			minDiff,
			maxDiff,
			restrictionRace,
			useLevelReduction,
			memberLimit,
			maxDropRule,
			items ?? Array.Empty<GlobalDropItemSummary>(),
			WorldTypes: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
			Races: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
			Ratings: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
			MapIds: mapIds?.ToHashSet() ?? new HashSet<int>(),
			Tribes: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
			NpcIds: new HashSet<int>(),
			NpcNames: npcNames ?? Array.Empty<GlobalDropNpcNameSummary>(),
			NpcGroups: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
			ExcludedNpcIds: excludedNpcIds?.ToHashSet() ?? new HashSet<int>(),
			Zones: new HashSet<string>(StringComparer.OrdinalIgnoreCase));
	}

	private static ItemTemplateSummary CreateItem(int itemId, int level, string race)
	{
		return new ItemTemplateSummary(
			itemId,
			$"item-{itemId}",
			DescriptionId: 0,
			Mask: 0,
			Level: level,
			ItemGroup: "MISC",
			ItemType: "NORMAL",
			Quality: "COMMON",
			Race: race,
			MaxStackCount: 100,
			Price: 0,
			ValidEquipmentSlots: 0);
	}

	private static WorldNpc CreateNpc(
		int templateId,
		string name,
		int level = 10,
		int worldId = 210010000,
		string rank = "NOVICE",
		string rating = "NORMAL",
		string race = "ELYOS",
		string tribe = "GENERAL",
		string type = "GENERAL")
	{
		return new WorldNpc(
			ObjectId: templateId + 1000000,
			TemplateId: templateId,
			Template: new NpcTemplateSummary(templateId, name, 0, level, rank, rating, race, tribe, type),
			Position: new WorldPosition(worldId, 1, 2, 3, 0));
	}
}
