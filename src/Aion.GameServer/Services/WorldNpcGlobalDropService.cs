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

	public WorldNpcGlobalDropService(GameServerRuntimeContext runtimeContext)
		: this(runtimeContext, null, null, null)
	{
	}

	public WorldNpcGlobalDropService(
		GlobalDropTable globalDrops,
		ItemTemplateTable itemTemplates,
		GlobalNpcExclusionTable? globalNpcExclusions = null)
		: this(null, globalDrops, itemTemplates, globalNpcExclusions)
	{
	}

	private WorldNpcGlobalDropService(
		GameServerRuntimeContext? runtimeContext,
		GlobalDropTable? globalDrops,
		ItemTemplateTable? itemTemplates,
		GlobalNpcExclusionTable? globalNpcExclusions)
	{
		_runtimeContext = runtimeContext;
		_globalDrops = globalDrops;
		_itemTemplates = itemTemplates;
		_globalNpcExclusions = globalNpcExclusions;
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
			|| exclusions.NpcTribes.Contains(npc.Template.Tribe);
	}

	public bool IsAllowedDefaultGlobalDropNpc(IWorldNpcObject npc, bool isChest)
	{
		// Java parity: services/drop/DropRegistrationService.isAllowedDefaultGlobalDropNpc, narrowed until siege/base/abyss spawn models exist.
		if (npc.Template.Level < 2
			&& !isChest
			&& npc.Position.WorldId is not 210010000 and not 220010000)
		{
			return false;
		}

		return !isChest;
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
			&& CheckStringRestriction(rule.NpcGroups, string.Empty)
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
