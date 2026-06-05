using System.Xml.Linq;

namespace Aion.GameServer.Dataholders;

public sealed class NearbyQuestTemplateXmlExtractor
{
	public IReadOnlyList<NearbyQuestTemplateSummary> Extract(string xmlContent)
	{
		using var reader = new StringReader(xmlContent);
		return Extract(XDocument.Load(reader, LoadOptions.None));
	}

	public IReadOnlyList<NearbyQuestTemplateSummary> Extract(Stream stream)
	{
		return Extract(XDocument.Load(stream, LoadOptions.None));
	}

	private static IReadOnlyList<NearbyQuestTemplateSummary> Extract(XDocument document)
	{
		// Java parity breadcrumb: dataholders/QuestsData.afterUnmarshal indexes model/templates/QuestTemplate objects.
		return document
			.Descendants()
			.Where(element => element.Name.LocalName == "quest" && element.Parent?.Name.LocalName == "quests")
			.Select(ToSummary)
			.ToArray();
	}

	private static NearbyQuestTemplateSummary ToSummary(XElement quest)
	{
		var classPermitted = ReadWhitespaceList(quest.Elements().FirstOrDefault(element => element.Name.LocalName == "class_permitted")?.Value);
		var startConditions = ReadStartConditions(quest);
		var inventoryItems = ReadInventoryItems(quest);
		var questWorkItems = ReadQuestWorkItems(quest);
		var repeatCycle = ReadWhitespaceList(ReadStringAttribute(quest, "repeat_cycle"))
			.Select(value => value.ToUpperInvariant())
			.ToArray();
		return new NearbyQuestTemplateSummary(
			QuestId: ReadRequiredIntAttribute(quest, "id"),
			MinLevelPermitted: ReadIntAttribute(quest, "minlevel_permitted"),
			MaxLevelPermitted: ReadIntAttribute(quest, "maxlevel_permitted"),
			RacePermitted: ReadStringAttribute(quest, "race_permitted"),
			ClassPermitted: classPermitted,
			GenderPermitted: ReadChildText(quest, "gender_permitted"),
			RequiredRank: ReadIntAttribute(quest, "rank"),
			MaxRepeatCount: ReadIntAttribute(quest, "max_repeat_count", defaultValue: 1),
			IsTimeBased: repeatCycle.Length != 0,
			IsTimer: ReadBoolAttribute(quest, "timer"),
			RepeatCycle: repeatCycle,
			HasXmlStartConditions: startConditions.Count != 0,
			HasInventoryItems: inventoryItems.Count != 0,
			CombineSkill: ReadIntAttribute(quest, "combineskill"),
			CombineSkillPoint: ReadIntAttribute(quest, "combine_skillpoint"),
			QuestCategory: ReadStringAttribute(quest, "category", defaultValue: "QUEST"),
			NpcFactionId: ReadIntAttribute(quest, "npcfaction_id"),
			IsMentorQuest: !string.Equals(ReadStringAttribute(quest, "mentor_type", defaultValue: "NONE"), "NONE", StringComparison.Ordinal),
			InventoryItems: inventoryItems,
			XmlStartConditions: startConditions,
			HasUnsupportedXmlStartConditionElements: startConditions.Any(condition => condition.HasUnsupportedElements),
			CanReport: ReadBoolAttribute(quest, "can_report"),
			RewardRepeatCount: ReadIntAttribute(quest, "reward_repeat_count"),
			HasRewards: HasChild(quest, "rewards"),
			HasExtendedRewards: HasChild(quest, "extended_rewards"),
			HasBonus: HasChild(quest, "bonus"),
			HasQuestWorkItems: questWorkItems.Count != 0,
			// Java parity: QuestTemplate.cannotShare (@XmlAttribute cannot_share, default false) and
			// QuestTemplate.target (@XmlAttribute target, QuestTarget enum, default NONE). Both back CM_QUEST_SHARE.
			CannotShare: ReadBoolAttribute(quest, "cannot_share"),
			// Java parity: QuestTemplate.cannotGiveup (@XmlAttribute cannot_giveup, default false)
			// backs QuestService.abandonQuest.
			CannotGiveup: ReadBoolAttribute(quest, "cannot_giveup"),
			QuestWorkItems: questWorkItems,
			Target: ReadStringAttribute(quest, "target", defaultValue: "NONE"),
			Name: ReadStringAttribute(quest, "name"),
			IsNoCount: IsNoCount(quest));
	}

	private static bool IsNoCount(XElement quest)
	{
		// Java parity: model/templates/QuestTemplate.isNoCount returns true for NON_COUNT and EVENT categories.
		var category = ReadStringAttribute(quest, "category", defaultValue: "QUEST");
		return string.Equals(category, "NON_COUNT", StringComparison.Ordinal)
			|| string.Equals(category, "EVENT", StringComparison.Ordinal);
	}

	private static IReadOnlyList<NearbyQuestInventoryItem> ReadQuestWorkItems(XElement quest)
	{
		// Java parity breadcrumb: model/templates/quest/QuestWorkItems contains quest_work_item rows.
		return quest
			.Elements()
			.Where(element => element.Name.LocalName == "quest_work_items")
			.SelectMany(element => element.Elements().Where(child => child.Name.LocalName == "quest_work_item"))
			.Select(element => new NearbyQuestInventoryItem(
				ReadRequiredIntAttribute(element, "item_id"),
				ReadNullableIntAttribute(element, "count")))
			.ToArray();
	}

	private static IReadOnlyList<NearbyQuestInventoryItem> ReadInventoryItems(XElement quest)
	{
		// Java parity breadcrumb: model/templates/quest/InventoryItems contains inventory_item rows.
		return quest
			.Elements()
			.Where(element => element.Name.LocalName == "inventory_items")
			.SelectMany(element => element.Elements().Where(child => child.Name.LocalName == "inventory_item"))
			.Select(element => new NearbyQuestInventoryItem(
				ReadRequiredIntAttribute(element, "item_id"),
				ReadNullableIntAttribute(element, "count")))
			.ToArray();
	}

	private static IReadOnlyList<NearbyQuestXmlStartCondition> ReadStartConditions(XElement quest)
	{
		// Java parity breadcrumb: model/templates/quest/XMLStartCondition JAXB fields under <start_conditions>.
		return quest
			.Elements()
			.Where(element => element.Name.LocalName == "start_conditions")
			.Select(ReadStartCondition)
			.ToArray();
	}

	private static NearbyQuestXmlStartCondition ReadStartCondition(XElement startCondition)
	{
		var unsupported = false;
		var finished = new List<NearbyQuestFinishedCondition>();
		var unfinished = new HashSet<int>();
		var noAcquired = new HashSet<int>();
		var acquired = new HashSet<int>();
		var equipped = new HashSet<int>();
		var requiredTitle = 0;

		foreach (var child in startCondition.Elements())
		{
			switch (child.Name.LocalName)
			{
				case "finished":
					finished.Add(new NearbyQuestFinishedCondition(
						ReadRequiredIntAttribute(child, "quest_id"),
						ReadIntAttribute(child, "reward", defaultValue: -1)));
					break;
				case "unfinished":
					AddWhitespaceInts(child.Value, unfinished);
					break;
				case "noacquired":
					AddWhitespaceInts(child.Value, noAcquired);
					break;
				case "acquired":
					AddWhitespaceInts(child.Value, acquired);
					break;
				case "equipped":
					AddWhitespaceInts(child.Value, equipped);
					break;
				case "required_title":
					requiredTitle = ReadIntText(child);
					break;
				default:
					unsupported = true;
					break;
			}
		}

		return new NearbyQuestXmlStartCondition(
			finished,
			unfinished,
			noAcquired,
			acquired,
			equipped,
			requiredTitle,
			unsupported);
	}

	private static string ReadStringAttribute(XElement element, string attributeName, string defaultValue = "")
	{
		return element.Attribute(attributeName)?.Value ?? defaultValue;
	}

	private static int ReadRequiredIntAttribute(XElement element, string attributeName)
	{
		var value = element.Attribute(attributeName)?.Value;
		if (!int.TryParse(value, out var parsed))
			throw new FormatException($"Missing or invalid quest attribute '{attributeName}'.");

		return parsed;
	}

	private static int ReadIntAttribute(XElement element, string attributeName, int defaultValue = 0)
	{
		var value = element.Attribute(attributeName)?.Value;
		return int.TryParse(value, out var parsed) ? parsed : defaultValue;
	}

	private static int? ReadNullableIntAttribute(XElement element, string attributeName)
	{
		var value = element.Attribute(attributeName)?.Value;
		return int.TryParse(value, out var parsed) ? parsed : null;
	}

	private static bool ReadBoolAttribute(XElement element, string attributeName)
	{
		var value = element.Attribute(attributeName)?.Value;
		return bool.TryParse(value, out var parsed) && parsed;
	}

	private static int ReadIntText(XElement element)
	{
		return int.TryParse(element.Value.Trim(), out var parsed) ? parsed : 0;
	}

	private static string ReadChildText(XElement element, string childName)
	{
		return element.Elements().FirstOrDefault(child => child.Name.LocalName == childName)?.Value.Trim() ?? string.Empty;
	}

	private static bool HasChild(XElement element, string childName)
	{
		return element.Elements().Any(child => child.Name.LocalName == childName);
	}

	private static IReadOnlySet<string> ReadWhitespaceList(string? value)
	{
		return string.IsNullOrWhiteSpace(value)
			? new HashSet<string>(StringComparer.Ordinal)
			: value
				.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.ToHashSet(StringComparer.Ordinal);
	}

	private static void AddWhitespaceInts(string? value, HashSet<int> destination)
	{
		if (string.IsNullOrWhiteSpace(value))
			return;

		foreach (var item in value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (int.TryParse(item, out var parsed))
				destination.Add(parsed);
		}
	}
}
