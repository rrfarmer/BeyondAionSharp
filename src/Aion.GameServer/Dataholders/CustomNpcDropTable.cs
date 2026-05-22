using System.Globalization;
using System.Xml;

namespace Aion.GameServer.Dataholders;

public sealed class CustomNpcDropTable
{
	private readonly IReadOnlyDictionary<int, CustomNpcDropSummary> _dropsByNpcId;

	public CustomNpcDropTable(IEnumerable<CustomNpcDropSummary> drops)
	{
		// Java parity: dataholders/CustomDrop.afterUnmarshal indexes one NpcDrop by npc id.
		_dropsByNpcId = drops
			.GroupBy(drop => drop.NpcId)
			.ToDictionary(group => group.Key, group => group.First());
	}

	public int Count => _dropsByNpcId.Count;

	public IReadOnlyCollection<CustomNpcDropSummary> Drops => _dropsByNpcId.Values.ToArray();

	public CustomNpcDropSummary? GetNpcDrop(int npcId)
	{
		return _dropsByNpcId.TryGetValue(npcId, out var drop) ? drop : null;
	}

	public static async Task<CustomNpcDropTable> LoadFromImportedFilesAsync(
		IReadOnlyList<string> importedFiles,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dataholders/DataManager.CUSTOM_NPC_DROP loads data/static_data/custom_drop/custom_drop.xml.
		var customDropFile = importedFiles.FirstOrDefault(file => Path.GetFileName(file).Equals("custom_drop.xml", StringComparison.OrdinalIgnoreCase));
		if (customDropFile == null)
			return new CustomNpcDropTable(Array.Empty<CustomNpcDropSummary>());

		var drops = new List<CustomNpcDropSummary>();
		var settings = new XmlReaderSettings
		{
			Async = true,
			DtdProcessing = DtdProcessing.Prohibit,
			IgnoreComments = true,
			IgnoreProcessingInstructions = true,
		};

		CustomNpcDropBuilder? currentNpcDrop = null;
		CustomDropGroupBuilder? currentGroup = null;

		using var reader = XmlReader.Create(customDropFile, settings);
		while (await reader.ReadAsync())
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (reader.NodeType == XmlNodeType.EndElement)
			{
				if (reader.LocalName == "drop_group" && currentNpcDrop != null && currentGroup != null)
				{
					currentNpcDrop.Groups.Add(currentGroup.ToSummary());
					currentGroup = null;
				}
				else if (reader.LocalName == "npc_drop" && currentNpcDrop != null)
				{
					drops.Add(currentNpcDrop.ToSummary());
					currentNpcDrop = null;
				}

				continue;
			}

			if (reader.NodeType != XmlNodeType.Element)
				continue;

			if (reader.LocalName == "npc_drop")
			{
				currentNpcDrop = new CustomNpcDropBuilder(ReadRequiredIntAttribute(reader, "npc_id"));
				if (reader.IsEmptyElement)
				{
					drops.Add(currentNpcDrop.ToSummary());
					currentNpcDrop = null;
				}

				continue;
			}

			if (reader.LocalName == "drop_group" && currentNpcDrop != null)
			{
				currentGroup = new CustomDropGroupBuilder(
					reader.GetAttribute("name") ?? string.Empty,
					reader.GetAttribute("race") ?? "PC_ALL",
					ReadBoolAttribute(reader, "level_based_chance_reduction"),
					ReadOptionalIntAttribute(reader, "max_items", 1));
				if (reader.IsEmptyElement)
				{
					currentNpcDrop.Groups.Add(currentGroup.ToSummary());
					currentGroup = null;
				}

				continue;
			}

			if (reader.LocalName == "drop" && currentGroup != null)
			{
				currentGroup.Drops.Add(ReadDrop(reader));
			}
		}

		return new CustomNpcDropTable(drops);
	}

	private static CustomDropSummary ReadDrop(XmlReader reader)
	{
		var itemId = ReadRequiredIntAttribute(reader, "item_id");
		var minAmount = ReadOptionalIntAttribute(reader, "min_amount", 1);
		var maxAmount = ReadOptionalIntAttribute(reader, "max_amount", 0);
		var chance = ReadOptionalFloatAttribute(reader, "chance", 100f);
		if (chance <= 0)
			throw new FormatException($"chance ({chance}) for drop {itemId} must be greater than zero");
		if (minAmount <= 0)
			throw new FormatException($"minAmount ({minAmount}) for drop {itemId} must be greater than zero");
		if (maxAmount == 0)
			maxAmount = minAmount;
		else if (maxAmount < minAmount)
			throw new FormatException($"maxAmount ({maxAmount}) for drop {itemId} must be greater than minAmount ({minAmount})");

		return new CustomDropSummary(
			itemId,
			minAmount,
			maxAmount,
			chance,
			ReadBoolAttribute(reader, "each_member"));
	}

	private static int ReadRequiredIntAttribute(XmlReader reader, string attributeName)
	{
		var value = reader.GetAttribute(attributeName);
		if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
			throw new FormatException($"Element <{reader.LocalName}> is missing required integer attribute '{attributeName}'.");

		return parsed;
	}

	private static int ReadOptionalIntAttribute(XmlReader reader, string attributeName, int defaultValue)
	{
		return int.TryParse(reader.GetAttribute(attributeName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
			? parsed
			: defaultValue;
	}

	private static float ReadOptionalFloatAttribute(XmlReader reader, string attributeName, float defaultValue)
	{
		return float.TryParse(reader.GetAttribute(attributeName), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
			? parsed
			: defaultValue;
	}

	private static bool ReadBoolAttribute(XmlReader reader, string attributeName)
	{
		return bool.TryParse(reader.GetAttribute(attributeName), out var parsed) && parsed;
	}

	private sealed class CustomNpcDropBuilder
	{
		public CustomNpcDropBuilder(int npcId)
		{
			NpcId = npcId;
		}

		private int NpcId { get; }

		public List<CustomDropGroupSummary> Groups { get; } = [];

		public CustomNpcDropSummary ToSummary()
		{
			return new CustomNpcDropSummary(NpcId, Groups.ToArray());
		}
	}

	private sealed class CustomDropGroupBuilder
	{
		public CustomDropGroupBuilder(string name, string race, bool useLevelBasedChanceReduction, int maxItems)
		{
			Name = name;
			Race = race;
			UseLevelBasedChanceReduction = useLevelBasedChanceReduction;
			MaxItems = maxItems <= 0 ? 1 : maxItems;
		}

		private string Name { get; }

		private string Race { get; }

		private bool UseLevelBasedChanceReduction { get; }

		private int MaxItems { get; }

		public List<CustomDropSummary> Drops { get; } = [];

		public CustomDropGroupSummary ToSummary()
		{
			return new CustomDropGroupSummary(
				Name,
				Race,
				UseLevelBasedChanceReduction,
				MaxItems,
				Drops.ToArray());
		}
	}
}

public sealed record CustomNpcDropSummary(
	int NpcId,
	IReadOnlyList<CustomDropGroupSummary> Groups);

public sealed record CustomDropGroupSummary(
	string Name,
	string Race,
	bool UseLevelBasedChanceReduction,
	int MaxItems,
	IReadOnlyList<CustomDropSummary> Drops);

public sealed record CustomDropSummary(
	int ItemId,
	int MinAmount,
	int MaxAmount,
	float Chance,
	bool EachMember);
