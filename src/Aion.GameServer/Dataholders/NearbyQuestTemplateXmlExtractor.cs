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
			.Where(element => element.Name.LocalName == "quest")
			.Select(ToSummary)
			.ToArray();
	}

	private static NearbyQuestTemplateSummary ToSummary(XElement quest)
	{
		var classPermitted = ReadWhitespaceList(quest.Elements().FirstOrDefault(element => element.Name.LocalName == "class_permitted")?.Value);
		return new NearbyQuestTemplateSummary(
			QuestId: ReadRequiredIntAttribute(quest, "id"),
			MinLevelPermitted: ReadIntAttribute(quest, "minlevel_permitted"),
			MaxLevelPermitted: ReadIntAttribute(quest, "maxlevel_permitted"),
			RacePermitted: ReadStringAttribute(quest, "race_permitted"),
			ClassPermitted: classPermitted,
			GenderPermitted: ReadChildText(quest, "gender_permitted"),
			RequiredRank: ReadIntAttribute(quest, "rank"),
			MaxRepeatCount: ReadIntAttribute(quest, "max_repeat_count", defaultValue: 1),
			IsTimeBased: !string.IsNullOrWhiteSpace(ReadStringAttribute(quest, "repeat_cycle")),
			HasXmlStartConditions: quest.Elements().Any(element => element.Name.LocalName == "start_conditions"),
			HasInventoryItems: quest.Elements().Any(element => element.Name.LocalName == "inventory_items"),
			CombineSkill: ReadIntAttribute(quest, "combineskill"),
			NpcFactionId: ReadIntAttribute(quest, "npcfaction_id"));
	}

	private static string ReadStringAttribute(XElement element, string attributeName)
	{
		return element.Attribute(attributeName)?.Value ?? string.Empty;
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

	private static string ReadChildText(XElement element, string childName)
	{
		return element.Elements().FirstOrDefault(child => child.Name.LocalName == childName)?.Value.Trim() ?? string.Empty;
	}

	private static IReadOnlySet<string> ReadWhitespaceList(string? value)
	{
		return string.IsNullOrWhiteSpace(value)
			? new HashSet<string>(StringComparer.Ordinal)
			: value
				.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.ToHashSet(StringComparer.Ordinal);
	}
}
