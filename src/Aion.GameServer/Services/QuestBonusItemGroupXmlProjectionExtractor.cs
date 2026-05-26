using System.Globalization;
using System.Xml.Linq;

namespace Aion.GameServer.Services;

public enum QuestBonusItemShape
{
	ItemRaceEntry,
	FullRewardItem,
	CraftItem,
	CraftRecipe,
	FoodItem,
	MedicineItem,
}

public sealed record QuestBonusItemGroupProjection(
	string ElementName,
	string BonusType,
	float Chance,
	QuestBonusItemShape ItemShape,
	IReadOnlyList<QuestBonusItemProjection>? Items = null)
{
	public IReadOnlyList<QuestBonusItemProjection> Items { get; init; } = Items ?? [];
}

public sealed record QuestBonusItemProjection(
	int ItemId,
	string? Race = null,
	int? Level = null,
	long? Count = null,
	float? Chance = null,
	int? Skill = null,
	int? MinLevel = null,
	int? MaxLevel = null);

public sealed class QuestBonusItemGroupXmlProjectionExtractor
{
	private static readonly IReadOnlyDictionary<string, QuestBonusItemGroupDefinition> SupportedGroups =
		new Dictionary<string, QuestBonusItemGroupDefinition>(StringComparer.Ordinal)
		{
			["craft_materials"] = new("TASK", QuestBonusItemShape.CraftItem),
			["craft_shop"] = new("TASK", QuestBonusItemShape.CraftItem),
			["craft_bundles"] = new("TASK", QuestBonusItemShape.CraftRecipe),
			["craft_recipes"] = new("TASK", QuestBonusItemShape.CraftRecipe),
			["manastones_common"] = new("MANASTONE", QuestBonusItemShape.ItemRaceEntry),
			["manastones_rare"] = new("MANASTONE", QuestBonusItemShape.ItemRaceEntry),
			["medals"] = new("MEDAL", QuestBonusItemShape.FullRewardItem),
			["food"] = new("FOOD", QuestBonusItemShape.FoodItem),
			["medicine_common"] = new("MEDICINE", QuestBonusItemShape.MedicineItem),
			["medicine_rare"] = new("MEDICINE", QuestBonusItemShape.MedicineItem),
			["medicine_legendary"] = new("MEDICINE", QuestBonusItemShape.MedicineItem),
			["events"] = new("EVENTS", QuestBonusItemShape.FullRewardItem),
		};

	public IReadOnlyList<QuestBonusItemGroupProjection> ExtractSupportedGroups(string xmlContent)
	{
		using var reader = new StringReader(xmlContent);
		return ExtractSupportedGroups(XDocument.Load(reader, LoadOptions.None));
	}

	public IReadOnlyList<QuestBonusItemGroupProjection> ExtractSupportedGroups(Stream stream)
	{
		return ExtractSupportedGroups(XDocument.Load(stream, LoadOptions.None));
	}

	public IReadOnlyList<QuestBonusItemGroupProjection> ExtractSupportedGroups(XDocument document)
	{
		// Java parity breadcrumb: services/reward/BonusService#getBonusGroups and
		// dataholders/ItemGroupsData. Only the Java-live quest bonus group families
		// are projected here; BOSS/GATHER/ENCHANT data exists but remains disabled.
		return document
			.Root?
			.Elements()
			.Where(element => SupportedGroups.ContainsKey(element.Name.LocalName))
			.Select(CreateProjection)
			.ToArray()
			?? [];
	}

	private static QuestBonusItemGroupProjection CreateProjection(XElement group)
	{
		var definition = SupportedGroups[group.Name.LocalName];
		return new QuestBonusItemGroupProjection(
			group.Name.LocalName,
			ReadStringAttribute(group, "bonusType", definition.BonusType),
			ReadFloatAttribute(group, "chance", defaultValue: 100f),
			definition.ItemShape,
			group
				.Elements()
				.Where(element => element.Name.LocalName == "item")
				.Select(ReadItem)
				.ToArray());
	}

	private static QuestBonusItemProjection ReadItem(XElement item)
	{
		return new QuestBonusItemProjection(
			ReadRequiredIntAttribute(item, "id"),
			Race: ReadOptionalStringAttribute(item, "race"),
			Level: ReadOptionalIntAttribute(item, "level"),
			Count: ReadOptionalLongAttribute(item, "count"),
			Chance: ReadOptionalFloatAttribute(item, "chance"),
			Skill: ReadOptionalIntAttribute(item, "skill"),
			MinLevel: ReadOptionalIntAttribute(item, "minLevel"),
			MaxLevel: ReadOptionalIntAttribute(item, "maxLevel"));
	}

	private static string ReadStringAttribute(XElement element, string attributeName, string defaultValue) =>
		element.Attribute(attributeName)?.Value ?? defaultValue;

	private static string? ReadOptionalStringAttribute(XElement element, string attributeName) =>
		element.Attribute(attributeName)?.Value;

	private static int ReadRequiredIntAttribute(XElement element, string attributeName)
	{
		var value = element.Attribute(attributeName)?.Value;
		if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
			throw new FormatException($"Missing or invalid item group attribute '{attributeName}'.");

		return parsed;
	}

	private static int? ReadOptionalIntAttribute(XElement element, string attributeName)
	{
		var value = element.Attribute(attributeName)?.Value;
		return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
			? parsed
			: null;
	}

	private static long? ReadOptionalLongAttribute(XElement element, string attributeName)
	{
		var value = element.Attribute(attributeName)?.Value;
		return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
			? parsed
			: null;
	}

	private static float ReadFloatAttribute(XElement element, string attributeName, float defaultValue)
	{
		var value = element.Attribute(attributeName)?.Value;
		return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
			? parsed
			: defaultValue;
	}

	private static float? ReadOptionalFloatAttribute(XElement element, string attributeName)
	{
		var value = element.Attribute(attributeName)?.Value;
		return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
			? parsed
			: null;
	}

	private sealed record QuestBonusItemGroupDefinition(string BonusType, QuestBonusItemShape ItemShape);
}
