using System.Xml.Linq;

namespace Aion.GameServer.Services;

public sealed class QuestFinishRewardTemplateXmlProjectionExtractor
{
	private static readonly IReadOnlyDictionary<string, string> ClassSelectableRewardElements =
		new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["fighter_selectable_reward"] = "GLADIATOR",
			["knight_selectable_reward"] = "TEMPLAR",
			["ranger_selectable_reward"] = "RANGER",
			["assassin_selectable_reward"] = "ASSASSIN",
			["wizard_selectable_reward"] = "SORCERER",
			["elementalist_selectable_reward"] = "SPIRIT_MASTER",
			["priest_selectable_reward"] = "CLERIC",
			["chanter_selectable_reward"] = "CHANTER",
			["gunner_selectable_reward"] = "GUNNER",
			["rider_selectable_reward"] = "RIDER",
			["bard_selectable_reward"] = "BARD",
		};

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
		// services/QuestService.getRewardItems/giveReward. This projection covers the
		// default regular reward group only; bonus rewards stay disabled.
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
		var extendedRewards = quest.Elements().FirstOrDefault(element => element.Name.LocalName == "extended_rewards");
		var nonItemProjection = selectedRewards is null
			? null
			: CreateNonItemProjection(selectedRewards);
		var itemProjection = CreateItemProjection(quest, selectedRewards, rewardGroupIndex, extendedRewards);

		return new QuestFinishRewardTemplateProjection(
			RewardGroupCount: rewards.Length == 0 ? null : rewards.Length,
			HasItemRewards: itemProjection is not null,
			HasNonItemRewards: nonItemProjection is not null && HasAnyNonItemField(nonItemProjection),
			IsChallengeTask: string.Equals(ReadStringAttribute(quest, "category", defaultValue: "QUEST"), "CHALLENGE_TASK", StringComparison.Ordinal),
			ItemProjection: itemProjection,
			NonItemProjection: nonItemProjection,
			RewardRepeatCount: ReadIntAttribute(quest, "reward_repeat_count"));
	}

	private static QuestFinishRewardItemTemplateProjection? CreateItemProjection(
		XElement quest,
		XElement? rewards,
		int rewardGroupIndex,
		XElement? extendedRewards)
	{
		IReadOnlyList<QuestFinishRewardItem> fixedItems = rewards is null ? [] : ReadQuestItems(rewards, "reward_item");
		IReadOnlyList<QuestFinishRewardItem> selectableItems = rewards is null ? [] : ReadQuestItems(rewards, "selectable_reward_item");
		IReadOnlyList<QuestFinishRewardItem> extendedFixedItems = extendedRewards is null ? [] : ReadQuestItems(extendedRewards, "reward_item");
		IReadOnlyList<QuestFinishRewardItem> extendedSelectableItems = extendedRewards is null ? [] : ReadQuestItems(extendedRewards, "selectable_reward_item");
		var classSelectableRewards = ReadClassSelectableRewards(quest);
		if (fixedItems.Count == 0
			&& selectableItems.Count == 0
			&& extendedFixedItems.Count == 0
			&& extendedSelectableItems.Count == 0
			&& classSelectableRewards.Count == 0)
		{
			return null;
		}

		IReadOnlyList<QuestFinishRewardGroupProjection> rewardGroups = fixedItems.Count == 0 && selectableItems.Count == 0
			? []
			:
			[
				new QuestFinishRewardGroupProjection(
					rewardGroupIndex,
					fixedItems,
					selectableItems),
			];
		var extendedGroup = extendedFixedItems.Count == 0 && extendedSelectableItems.Count == 0
			? null
			: new QuestFinishRewardGroupProjection(
				RewardGroupIndex: -1,
				FixedRewardItems: extendedFixedItems,
				SelectableRewardItems: extendedSelectableItems);

		return new QuestFinishRewardItemTemplateProjection(
			RewardGroups: rewardGroups,
			ExtendedRewards: extendedGroup,
			ClassSelectableRewards: classSelectableRewards,
			SingleTimeClassReward: ReadIntAttribute(quest, "use_class_reward") == 2,
			ClassRewardOnEveryRepeat: ReadIntAttribute(quest, "use_class_reward") == 1);
	}

	private static IReadOnlyDictionary<string, IReadOnlyList<QuestFinishRewardItem>> ReadClassSelectableRewards(XElement quest)
	{
		var classRewards = new Dictionary<string, IReadOnlyList<QuestFinishRewardItem>>(StringComparer.Ordinal);
		foreach (var (elementName, playerClass) in ClassSelectableRewardElements)
		{
			var rewards = ReadQuestItems(quest, elementName);
			if (rewards.Count != 0)
			{
				classRewards[playerClass] = rewards;
			}
		}

		return classRewards;
	}

	private static IReadOnlyList<QuestFinishRewardItem> ReadQuestItems(XElement rewards, string elementName)
	{
		return rewards
			.Elements()
			.Where(element => element.Name.LocalName == elementName)
			.Select(element => new QuestFinishRewardItem(
				ReadRequiredIntAttribute(element, "item_id"),
				ReadLongAttribute(element, "count", defaultValue: 1)))
			.ToArray();
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

	private static long ReadLongAttribute(XElement element, string attributeName, long defaultValue = 0)
	{
		var value = element.Attribute(attributeName)?.Value;
		return long.TryParse(value, out var parsed) ? parsed : defaultValue;
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
