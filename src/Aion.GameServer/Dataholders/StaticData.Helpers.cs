using System.Collections.ObjectModel;
using System.Globalization;
using System.Xml;

namespace Aion.GameServer.Dataholders;

public sealed partial class StaticData
{
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

	private static long ReadLongAttribute(XmlReader reader, string attributeName)
	{
		return long.TryParse(reader.GetAttribute(attributeName), out var parsed) ? parsed : 0;
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
