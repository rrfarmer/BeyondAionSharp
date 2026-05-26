using System.Xml.Linq;

namespace Aion.GameServer.Services;

public sealed class QuestFinishRewardTemplateXmlProjectionExtractor
{
	public IReadOnlyDictionary<int, QuestFinishRewardTemplateProjection> ExtractDefaultRegularNonItemProjections(string xmlContent)
	{
		using var reader = new StringReader(xmlContent);
		return ExtractDefaultRegularNonItemProjections(XDocument.Load(reader, LoadOptions.None));
	}

	public IReadOnlyDictionary<int, QuestFinishRewardTemplateProjection> ExtractDefaultRegularNonItemProjections(Stream stream)
	{
		return ExtractDefaultRegularNonItemProjections(XDocument.Load(stream, LoadOptions.None));
	}

	public IReadOnlyDictionary<int, QuestFinishRewardTemplateProjection> ExtractDefaultRegularNonItemProjections(XDocument document)
	{
		// Java parity breadcrumb: model/templates/QuestTemplate#getRewards plus
		// services/QuestService.giveReward(QuestEnv, Rewards). This first projection
		// intentionally covers only the default regular reward group non-item fields.
		return document
			.Descendants()
			.Where(element => element.Name.LocalName == "quest")
			.ToDictionary(ReadQuestId, quest => CreateProjection(quest, rewardGroupIndex: 0));
	}

	public QuestFinishRewardTemplateProjection CreateProjection(XElement quest, int rewardGroupIndex)
	{
		var rewards = quest.Elements().Where(element => element.Name.LocalName == "rewards").ToArray();
		var selectedRewards = rewardGroupIndex >= 0 && rewardGroupIndex < rewards.Length
			? rewards[rewardGroupIndex]
			: null;
		var nonItemProjection = selectedRewards is null
			? null
			: CreateNonItemProjection(selectedRewards);

		return new QuestFinishRewardTemplateProjection(
			RewardGroupCount: rewards.Length == 0 ? null : rewards.Length,
			HasNonItemRewards: nonItemProjection is not null && HasAnyNonItemField(nonItemProjection),
			IsChallengeTask: string.Equals(ReadStringAttribute(quest, "category", defaultValue: "QUEST"), "CHALLENGE_TASK", StringComparison.Ordinal),
			NonItemProjection: nonItemProjection,
			RewardRepeatCount: ReadIntAttribute(quest, "reward_repeat_count"));
	}

	private static QuestFinishRewardNonItemTemplateProjection CreateNonItemProjection(XElement rewards)
	{
		return new QuestFinishRewardNonItemTemplateProjection(
			Kinah: ReadLongAttribute(rewards, "gold"),
			Experience: ReadIntAttribute(rewards, "exp"),
			Title: ReadIntAttribute(rewards, "title"),
			AbyssPoints: ReadIntAttribute(rewards, "ap"),
			DivinePoints: ReadIntAttribute(rewards, "dp"),
			GloryPoints: ReadIntAttribute(rewards, "gp"),
			ExtendInventory: ReadIntAttribute(rewards, "extend_inventory"),
			ExtendStigma: ReadIntAttribute(rewards, "extend_stigma"),
			CollectItemChecks: ReadWhitespaceIntList(rewards.Attribute("ccheck")?.Value),
			InventoryItemCheck: ReadIntAttribute(rewards, "icheck"));
	}

	private static bool HasAnyNonItemField(QuestFinishRewardNonItemTemplateProjection projection)
	{
		return projection.Kinah != 0
			|| projection.Experience != 0
			|| projection.Title != 0
			|| projection.AbyssPoints != 0
			|| projection.DivinePoints != 0
			|| projection.GloryPoints != 0
			|| projection.ExtendInventory != 0
			|| projection.ExtendStigma != 0
			|| projection.CollectItemChecks.Count != 0
			|| projection.InventoryItemCheck != 0;
	}

	private static int ReadQuestId(XElement quest) => ReadRequiredIntAttribute(quest, "id");

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

	private static int ReadIntAttribute(XElement element, string attributeName)
	{
		var value = element.Attribute(attributeName)?.Value;
		return int.TryParse(value, out var parsed) ? parsed : 0;
	}

	private static long ReadLongAttribute(XElement element, string attributeName)
	{
		var value = element.Attribute(attributeName)?.Value;
		return long.TryParse(value, out var parsed) ? parsed : 0;
	}

	private static IReadOnlyList<int> ReadWhitespaceIntList(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return [];

		return value
			.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(item => int.TryParse(item, out var parsed) ? parsed : (int?)null)
			.Where(item => item.HasValue)
			.Select(item => item!.Value)
			.ToArray();
	}
}
