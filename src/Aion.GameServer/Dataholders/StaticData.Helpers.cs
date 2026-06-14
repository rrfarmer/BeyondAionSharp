using System.Collections.ObjectModel;
using Aion.GameServer.Model.Vortex;
using System.Globalization;
using System.Xml;
using Aion.GameServer.Model.Templates.Pet;

namespace Aion.GameServer.Dataholders;

public sealed partial class StaticData
{
	private static QuestNpcStartTable LoadQuestNpcStarts(
		string cacheFilePath,
		string? questHandlerDirectory,
		CancellationToken cancellationToken)
	{
		var table = new QuestNpcStartTable();
		var questScriptDirectory = Path.GetDirectoryName(cacheFilePath);
		var result = new QuestNpcStartRegistrationSourceLoader()
			.Load(questScriptDirectory, questHandlerDirectory, cancellationToken);
		foreach (var source in result.Sources)
		{
			if (source.EventKind == QuestNpcRegistrationEventKind.OnTalkEvent)
				table.RegisterOnTalkEvent(source);
			else
				table.RegisterOnQuestStart(source);
		}

		return table;
	}

	private static IReadOnlyList<GlobalDropRuleSummary> ProcessGlobalDropRules(
		IReadOnlyList<GlobalDropRuleSummary> rules,
		IReadOnlyList<NpcTemplateSummary> npcTemplates)
	{
		// Java parity: dataholders/GlobalDropData.processRules expands gd_npc_names into gd_npc ids once NPC templates are loaded.
		var processedRules = new List<GlobalDropRuleSummary>(rules.Count);
		foreach (var rule in rules)
		{
			if (rule.NpcNames.Count == 0)
			{
				processedRules.Add(rule);
				continue;
			}

			var allowedNpcIds = rule.NpcIds.ToHashSet();
			foreach (var npcName in rule.NpcNames)
			{
				foreach (var npc in npcTemplates.Where(npc => MatchesGlobalDropNpcName(npcName, npc.Name)))
					allowedNpcIds.Add(npc.TemplateId);
			}

			processedRules.Add(
				allowedNpcIds.Count == 0
					? rule
					: rule with
					{
						NpcIds = allowedNpcIds,
						NpcNames = Array.Empty<GlobalDropNpcNameSummary>(),
					});
		}

		return processedRules.AsReadOnly();
	}

	private static bool TryGetTradeListTemplateKind(string localName, out TradeListTemplateKind kind)
	{
		switch (localName)
		{
			case "tradelist_template":
				kind = TradeListTemplateKind.TradeList;
				return true;
			case "trade_in_list_template":
				kind = TradeListTemplateKind.TradeInList;
				return true;
			case "purchase_template":
				kind = TradeListTemplateKind.PurchaseList;
				return true;
			default:
				kind = default;
				return false;
		}
	}

	private static void AddTradeListTemplate(
		TradeListTemplateSummary template,
		TradeListTemplateKind kind,
		ICollection<TradeListTemplateSummary> tradeLists,
		ICollection<TradeListTemplateSummary> tradeInLists,
		ICollection<TradeListTemplateSummary> purchaseLists)
	{
		switch (kind)
		{
			case TradeListTemplateKind.TradeList:
				tradeLists.Add(template);
				break;
			case TradeListTemplateKind.TradeInList:
				tradeInLists.Add(template);
				break;
			case TradeListTemplateKind.PurchaseList:
				purchaseLists.Add(template);
				break;
		}
	}

	private static void AddGoodsListSummary(
		GoodsListSummary summary,
		GoodsListKind kind,
		ICollection<GoodsListSummary> goodsLists,
		ICollection<GoodsListSummary> goodsInLists,
		ICollection<GoodsListSummary> goodsPurchaseLists)
	{
		switch (kind)
		{
			case GoodsListKind.List:
				goodsLists.Add(summary);
				break;
			case GoodsListKind.InList:
				goodsInLists.Add(summary);
				break;
			case GoodsListKind.PurchaseList:
				goodsPurchaseLists.Add(summary);
				break;
		}
	}

	private static bool MatchesGlobalDropNpcName(GlobalDropNpcNameSummary ruleName, string npcName)
	{
		var value = ruleName.Value.ToLowerInvariant();
		return ruleName.Function.ToUpperInvariant() switch
		{
			"CONTAINS" => npcName.Contains(value, StringComparison.Ordinal),
			"END_WITH" => npcName.EndsWith(value, StringComparison.Ordinal),
			"START_WITH" => npcName.StartsWith(value, StringComparison.Ordinal),
			"EQUALS" => string.Equals(npcName, ruleName.Value, StringComparison.OrdinalIgnoreCase),
			_ => false,
		};
	}

	private enum TradeListTemplateKind
	{
		TradeList,
		TradeInList,
		PurchaseList,
	}

	private enum GoodsListKind
	{
		List,
		InList,
		PurchaseList,
	}


	private static int ReadRequiredIntAttribute(XmlReader reader, string attributeName)
	{
		var value = reader.GetAttribute(attributeName);
		if (!int.TryParse(value, out var parsed))
			throw new FormatException($"Element <{reader.LocalName}> is missing required integer attribute '{attributeName}'.");

		return parsed;
	}

	private static int ReadIntAttribute(XmlReader reader, string attributeName)
	{
		return int.TryParse(reader.GetAttribute(attributeName), out var parsed) ? parsed : 0;
	}

	private static int ReadOptionalIntAttribute(XmlReader reader, string attributeName, int defaultValue)
	{
		return int.TryParse(reader.GetAttribute(attributeName), out var parsed) ? parsed : defaultValue;
	}

	private static PetFunctionType ReadPetFunctionTypeAttribute(string? value)
	{
		// Java parity: model/templates/pet/PetFunctionType JAXB enum names.
		return value switch
		{
			"WAREHOUSE" => PetFunctionType.WAREHOUSE,
			"FOOD" => PetFunctionType.FOOD,
			"DOPING" => PetFunctionType.DOPING,
			"LOOT" => PetFunctionType.LOOT,
			"BUFF" => PetFunctionType.BUFF,
			"MERCHANT" => PetFunctionType.MERCHANT,
			"NONE" => PetFunctionType.NONE,
			"APPEARANCE" => PetFunctionType.APPEARANCE,
			"BAG" => PetFunctionType.BAG,
			"WING" => PetFunctionType.WING,
			_ => throw new FormatException($"Unexpected PetFunctionType value '{value}'."),
		};
	}

	private static float ReadOptionalFloatAttribute(XmlReader reader, string attributeName, float defaultValue)
	{
		return float.TryParse(reader.GetAttribute(attributeName), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
			? parsed
			: defaultValue;
	}

	private static int? ReadNullableIntAttribute(XmlReader reader, string attributeName)
	{
		return int.TryParse(reader.GetAttribute(attributeName), out var parsed) ? parsed : null;
	}

	private static float? ReadNullableFloatAttribute(XmlReader reader, string attributeName)
	{
		return float.TryParse(reader.GetAttribute(attributeName), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
			? parsed
			: null;
	}

	private static long? ReadNullableLongAttribute(XmlReader reader, string attributeName)
	{
		return long.TryParse(reader.GetAttribute(attributeName), out var parsed) ? parsed : null;
	}

	private static bool ReadBoolAttribute(XmlReader reader, string attributeName)
	{
		return bool.TryParse(reader.GetAttribute(attributeName), out var parsed) && parsed;
	}

	private static bool ReadRequiredBoolAttribute(XmlReader reader, string attributeName)
	{
		var value = reader.GetAttribute(attributeName)
			?? throw new FormatException($"Element <{reader.LocalName}> is missing required attribute '{attributeName}'.");
		return bool.Parse(value);
	}

	private static bool ReadXmlBoolAttribute(XmlReader reader, string attributeName)
	{
		var value = reader.GetAttribute(attributeName)
			?? throw new FormatException($"Element <{reader.LocalName}> is missing required attribute '{attributeName}'.");
		return value switch
		{
			"1" => true,
			"0" => false,
			_ => bool.Parse(value),
		};
	}

	private static bool ReadOptionalBoolAttribute(XmlReader reader, string attributeName, bool defaultValue)
	{
		return bool.TryParse(reader.GetAttribute(attributeName), out var parsed) ? parsed : defaultValue;
	}

	private static VortexStateType ReadVortexStateTypeAttribute(XmlReader reader, string attributeName)
	{
		var value = reader.GetAttribute(attributeName);
		return value switch
		{
			"INVASION" => VortexStateType.INVASION,
			"PEACE" => VortexStateType.PEACE,
			_ => throw new FormatException($"Element <{reader.LocalName}> has unexpected VortexStateType '{value}'."),
		};
	}

	private static ItemActionUseTargetType ParseItemActionUseTargetType(string value)
	{
		// Java parity: model/templates/item/actions/UseTarget.fromValue.
		return value switch
		{
			"ACCESSORY" => ItemActionUseTargetType.Accessory,
			"ARMOR" => ItemActionUseTargetType.Armor,
			"EQUIPMENT" => ItemActionUseTargetType.Equipment,
			"WEAPON" => ItemActionUseTargetType.Weapon,
			"WING" => ItemActionUseTargetType.Wing,
			"OTHER" => ItemActionUseTargetType.Other,
			"ALL" => ItemActionUseTargetType.All,
			_ => throw new FormatException($"Unexpected UseTarget value '{value}'."),
		};
	}

	private static NpcSubDialogType? ReadNpcSubDialogType(string? value)
	{
		return value switch
		{
			null or "" => null,
			"FORT_CAPTURE" => NpcSubDialogType.FortCapture,
			"SKILL_ID" => NpcSubDialogType.SkillId,
			"ITEM_ID" => NpcSubDialogType.ItemId,
			"RETURN" => NpcSubDialogType.Return,
			"PCBANG" => NpcSubDialogType.PcBang,
			"PAID_USER" => NpcSubDialogType.PaidUser,
			"NEWBIE" => NpcSubDialogType.Newbie,
			"ABYSSRANK" => NpcSubDialogType.AbyssRank,
			"ABYSSRANKING" => NpcSubDialogType.AbyssRanking,
			"LEVEL" => NpcSubDialogType.Level,
			"LEVEL_LOW" => NpcSubDialogType.LevelLow,
			"LEVEL_HIGH" => NpcSubDialogType.LevelHigh,
			"LEGION_DOMINION_NPC" => NpcSubDialogType.LegionDominionNpc,
			"TARGET_LEGION_DOMINION" => NpcSubDialogType.TargetLegionDominion,
			"PACK_3" => NpcSubDialogType.Pack3,
			"PACK_4" => NpcSubDialogType.Pack4,
			"CASH" => NpcSubDialogType.Cash,
			_ => null,
		};
	}

	private static DateTime? ReadDateTimeAttribute(XmlReader reader, string attributeName)
	{
		return DateTime.TryParse(
			reader.GetAttribute(attributeName),
			CultureInfo.InvariantCulture,
			DateTimeStyles.None,
			out var parsed)
			? parsed
			: null;
	}

	private static DateTime ReadRequiredDateTimeAttribute(XmlReader reader, string attributeName)
	{
		return ReadDateTimeAttribute(reader, attributeName)
			?? throw new FormatException($"Element <{reader.LocalName}> is missing required DateTime attribute '{attributeName}'.");
	}

	private static bool IsStatModifierElement(string elementName)
	{
		return elementName is "add" or "sub" or "rate" or "set" or "abs";
	}

	private static bool IsDropBoostStatEffectElement(string elementName)
	{
		return elementName is "boostdroprate" or "drboost";
	}

	private static SkillStatChangeConditionSummary ReadSkillStatChangeCondition(XmlReader reader)
	{
		var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
		if (reader.HasAttributes)
		{
			while (reader.MoveToNextAttribute())
				attributes[reader.Name] = reader.Value;
			reader.MoveToElement();
		}

		return new SkillStatChangeConditionSummary(
			reader.LocalName,
			new ReadOnlyDictionary<string, string>(attributes));
	}

	private static long ReadLongAttribute(XmlReader reader, string attributeName)
	{
		return long.TryParse(reader.GetAttribute(attributeName), out var parsed) ? parsed : 0;
	}

	private static int GetHouseTypeId(string size)
	{
		// Java parity: model/templates/housing/HouseType enum ids.
		return size.ToUpperInvariant() switch
		{
			"STUDIO" => 0,
			"HOUSE" => 1,
			"MANSION" => 2,
			"ESTATE" => 3,
			"PALACE" => 4,
			_ => 0,
		};
	}

	private static int GetDefaultBuildingId(
		int landId,
		IReadOnlyDictionary<int, int> defaultBuildingIds,
		IReadOnlyDictionary<int, int> firstBuildingIds)
	{
		// Java parity: model/templates/housing/HousingLand.getDefaultBuilding defaults to the first listed building.
		return defaultBuildingIds.GetValueOrDefault(landId, firstBuildingIds.GetValueOrDefault(landId));
	}

	private static IReadOnlyDictionary<string, int> ReadLevelRestrictions(string? restrict)
	{
		// Java parity: model/templates/item/ItemTemplate.levelRestrictions ordinal order from PlayerClass.
		if (string.IsNullOrWhiteSpace(restrict))
			return new Dictionary<string, int>(StringComparer.Ordinal);

		var restrictions = restrict.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var levelRestrictions = new Dictionary<string, int>(StringComparer.Ordinal);
		for (var i = 0; i < restrictions.Length && i < PlayerClasses.Length; i++)
		{
			if (int.TryParse(restrictions[i], out var requiredLevel) && requiredLevel > 0)
				levelRestrictions[PlayerClasses[i]] = requiredLevel;
		}

		return levelRestrictions;
	}

	private static IReadOnlySet<string> ReadPlayerClasses(string? playerClasses)
	{
		// Java parity: model/templates/rewards/ResultedItem.player_classes.
		return string.IsNullOrWhiteSpace(playerClasses)
			? new HashSet<string>(StringComparer.Ordinal)
			: playerClasses.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
	}

	private static IReadOnlyList<int> ReadIntListAttribute(XmlReader reader, string attributeName)
	{
		var value = reader.GetAttribute(attributeName);
		if (string.IsNullOrWhiteSpace(value))
			return Array.Empty<int>();

		return value
			.Split(' ', StringSplitOptions.RemoveEmptyEntries)
			.Select(part => int.Parse(part, CultureInfo.InvariantCulture))
			.ToArray();
	}

	private static IReadOnlyList<int> ReadXmlIntListAttribute(XmlReader reader, string attributeName)
	{
		var value = reader.GetAttribute(attributeName);
		if (string.IsNullOrWhiteSpace(value))
			return Array.Empty<int>();

		return value
			.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(part => int.Parse(part, CultureInfo.InvariantCulture))
			.ToArray();
	}

	private static IReadOnlySet<int> ParseIntSet(string value)
	{
		return string.IsNullOrWhiteSpace(value)
			? new HashSet<int>()
			: value
				.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(part => int.Parse(part, CultureInfo.InvariantCulture))
				.ToHashSet();
	}

	private static IReadOnlySet<string> ParseStringSet(string value)
	{
		return string.IsNullOrWhiteSpace(value)
			? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			: value
				.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	private static bool IsInsideElement(IReadOnlyDictionary<int, string> elementPath, int depth, string elementName)
	{
		return elementPath.Any(pair => pair.Key < depth && pair.Value == elementName);
	}

	private static float ReadFloatAttribute(XmlReader reader, string attributeName)
	{
		return float.TryParse(reader.GetAttribute(attributeName), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
	}

	private static long GetItemGroupSlots(string? itemGroup)
	{
		// Java parity: model/templates/item/ItemTemplate.item_group -> model/items/ItemSlot mask.
		return itemGroup?.ToUpperInvariant() switch
		{
			"NOWEAPON" or "SWORD" or "GREATSWORD" or "DAGGER" or "MACE" or "ORB" or "SPELLBOOK" or "POLEARM" or "STAFF" or "BOW"
				or "HARP" or "GUN" or "CANNON" or "KEYBLADE" or "TOOLRODS" or "TOOLPICKS" => MainHand | SubHand,
			"NPC_MACE" or "TOOLHOES" => MainHand,
			"SHIELD" or "CL_SHIELD" => SubHand,
			"TORSO" or "RB_TORSO" or "CL_TORSO" or "LT_TORSO" or "CH_TORSO" or "PL_TORSO" => Torso,
			"GLOVE" or "RB_GLOVE" or "CL_GLOVE" or "LT_GLOVE" or "CH_GLOVE" or "PL_GLOVE" => Gloves,
			"SHOULDER" or "RB_SHOULDER" or "CL_SHOULDER" or "LT_SHOULDER" or "CH_SHOULDER" or "PL_SHOULDER" => Shoulder,
			"PANTS" or "RB_PANTS" or "CL_PANTS" or "LT_PANTS" or "CH_PANTS" or "PL_PANTS" => Pants,
			"SHOES" or "RB_SHOES" or "CL_SHOES" or "LT_SHOES" or "CH_SHOES" or "PL_SHOES" => Boots,
			"EARRING" => EarringsLeft | EarringsRight,
			"RING" => RingLeft | RingRight,
			"NECKLACE" => Necklace,
			"BELT" => Waist,
			"WING" => Wings,
			"PLUME" => Plume,
			"HEAD" or "LT_HEADS" or "CL_HEADS" => Helmet,
			"CL_MULTISLOT" => Torso | Pants,
			"POWER_SHARDS" => PowerShardLeft | PowerShardRight,
			"STIGMA" => RegularStigmas | AdvancedStigmas,
			_ => 0,
		};
	}

	private const long MainHand = 1L;
	private const long SubHand = 1L << 1;
	private const long Helmet = 1L << 2;
	private const long Torso = 1L << 3;
	private const long Gloves = 1L << 4;
	private const long Boots = 1L << 5;
	private const long EarringsLeft = 1L << 6;
	private const long EarringsRight = 1L << 7;
	private const long RingLeft = 1L << 8;
	private const long RingRight = 1L << 9;
	private const long Necklace = 1L << 10;
	private const long Shoulder = 1L << 11;
	private const long Pants = 1L << 12;
	private const long PowerShardRight = 1L << 13;
	private const long PowerShardLeft = 1L << 14;
	private const long Wings = 1L << 15;
	private const long Waist = 1L << 16;
	private const long Plume = 1L << 19;
	private const long RegularStigmas = (1L << 30) | (1L << 31) | (1L << 32);
	private const long AdvancedStigmas = (1L << 33) | (1L << 34) | (1L << 35);
	private static readonly string[] PlayerClasses =
	[
		"WARRIOR",
		"GLADIATOR",
		"TEMPLAR",
		"SCOUT",
		"ASSASSIN",
		"RANGER",
		"MAGE",
		"SORCERER",
		"SPIRIT_MASTER",
		"PRIEST",
		"CLERIC",
		"CHANTER",
		"ENGINEER",
		"RIDER",
		"GUNNER",
		"ARTIST",
		"BARD",
	];

	private static async Task<IReadOnlyList<long>> LoadExperienceTableFromImportedFilesAsync(
		IReadOnlyList<string> importedFiles,
		CancellationToken cancellationToken)
	{
		// Java parity: data/static_data/player_experience_table.xml fallback when merged text nodes are absent.
		var experienceFile = importedFiles.FirstOrDefault(file => Path.GetFileName(file).Equals("player_experience_table.xml", StringComparison.OrdinalIgnoreCase));
		if (experienceFile == null)
			return Array.Empty<long>();

		var experience = new List<long>();
		var settings = new XmlReaderSettings
		{
			Async = true,
			DtdProcessing = DtdProcessing.Prohibit,
			IgnoreComments = true,
			IgnoreProcessingInstructions = true,
		};

		using var reader = XmlReader.Create(experienceFile, settings);
		while (await reader.ReadAsync())
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "exp")
				continue;

			var value = await ReadElementTextAsync(reader, cancellationToken);
			if (long.TryParse(value, out var parsedExperience))
				experience.Add(parsedExperience);
		}

		return experience;
	}

	private static async Task<string> ReadElementTextAsync(XmlReader reader, CancellationToken cancellationToken)
	{
		if (reader.IsEmptyElement)
			return string.Empty;

		var depth = reader.Depth;
		while (await reader.ReadAsync())
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA)
				return reader.Value;
			if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
				return string.Empty;
		}

		return string.Empty;
	}
}
