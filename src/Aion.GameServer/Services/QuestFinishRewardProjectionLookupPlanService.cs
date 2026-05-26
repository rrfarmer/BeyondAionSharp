using Aion.GameServer.Dataholders;
using System.Xml.Linq;

namespace Aion.GameServer.Services;

public enum QuestFinishRewardProjectionLookupStatus
{
	Found,
	MissingQuestTemplate,
	MissingRewardGroupProjection,
}

public enum QuestFinishRewardProjectionLookupDiagnostic
{
	MissingPlayerClassForClassSelectableReward,
	MissingTargetNpcTemplateForExperienceReward,
}

public sealed record QuestFinishRewardProjectionLookupInput(
	int QuestId,
	int DialogActionId,
	int? ExtendedRewardIndex,
	int CompleteCount,
	int? CorrectedRewardGroup,
	string? PlayerClass = null,
	int TargetNpcId = 0,
	bool HasTargetNpcTemplate = false);

public sealed record QuestFinishRewardProjectionLookupEntry(
	NearbyQuestTemplateSummary Template,
	IReadOnlyDictionary<int, QuestFinishRewardTemplateProjection> RewardGroupProjections);

public sealed class QuestFinishRewardProjectionLookupTable
{
	private readonly Dictionary<int, QuestFinishRewardProjectionLookupEntry> _entries;

	public QuestFinishRewardProjectionLookupTable(IEnumerable<(int QuestId, QuestFinishRewardProjectionLookupEntry Entry)> entries)
	{
		// Java parity breadcrumb: dataholders/QuestsData#afterUnmarshal indexes full QuestTemplate objects by quest id.
		_entries = entries.ToDictionary(entry => entry.QuestId, entry => entry.Entry);
	}

	public int Count => _entries.Count;

	public IEnumerable<QuestFinishRewardProjectionLookupEntry> Entries => _entries.Values;

	public bool TryGetQuest(int questId, out QuestFinishRewardProjectionLookupEntry? entry)
	{
		return _entries.TryGetValue(questId, out entry);
	}
}

public sealed class QuestFinishRewardProjectionLookupTableXmlFactory
{
	public QuestFinishRewardProjectionLookupTable Create(string xmlContent)
	{
		using var reader = new StringReader(xmlContent);
		return Create(XDocument.Load(reader, LoadOptions.None));
	}

	public QuestFinishRewardProjectionLookupTable Create(Stream stream)
	{
		return Create(XDocument.Load(stream, LoadOptions.None));
	}

	public QuestFinishRewardProjectionLookupTable Create(XDocument document)
	{
		ArgumentNullException.ThrowIfNull(document);

		// Java parity breadcrumb: this is a non-live bridge toward QuestsData#afterUnmarshal plus
		// QuestTemplate#getRewards. It materializes all regular reward groups, but still does not
		// expose production StaticData or socket wiring.
		var summaries = new NearbyQuestTemplateXmlExtractor()
			.Extract(document.ToString(SaveOptions.DisableFormatting))
			.ToDictionary(template => template.QuestId);
		var extractor = new QuestFinishRewardTemplateXmlProjectionExtractor();
		var entries = document
			.Descendants()
			.Where(element => element.Name.LocalName == "quest")
			.Select(quest =>
			{
				var questId = ReadRequiredQuestId(quest);
				var rewardGroupCount = quest.Elements().Count(element => element.Name.LocalName == "rewards");
				var rewardGroupIndexes = rewardGroupCount == 0
					? [0]
					: Enumerable.Range(0, rewardGroupCount);
				var projections = rewardGroupIndexes.ToDictionary(
					rewardGroupIndex => rewardGroupIndex,
					rewardGroupIndex => extractor.CreateProjection(quest, rewardGroupIndex));
				return (questId, new QuestFinishRewardProjectionLookupEntry(summaries[questId], projections));
			});

		return new QuestFinishRewardProjectionLookupTable(entries);
	}

	private static int ReadRequiredQuestId(XElement quest)
	{
		var value = quest.Attribute("id")?.Value;
		if (!int.TryParse(value, out var parsed))
			throw new FormatException("Missing or invalid quest attribute 'id'.");

		return parsed;
	}
}

public sealed record QuestFinishRewardProjectionLookupPlan(
	QuestFinishRewardProjectionLookupStatus Status,
	QuestFinishRewardTemplateProjection? Projection,
	IReadOnlyList<QuestFinishRewardProjectionLookupDiagnostic> Diagnostics);

public static class QuestFinishRewardProjectionLookupPlanService
{
	public static QuestFinishRewardProjectionLookupPlan CreatePlan(
		QuestFinishRewardProjectionLookupInput input,
		QuestFinishRewardProjectionLookupTable lookupTable)
	{
		ArgumentNullException.ThrowIfNull(lookupTable);

		if (!lookupTable.TryGetQuest(input.QuestId, out var entry) || entry == null)
		{
			return new QuestFinishRewardProjectionLookupPlan(
				QuestFinishRewardProjectionLookupStatus.MissingQuestTemplate,
				Projection: null,
				Diagnostics: []);
		}

		var rewardGroup = input.CorrectedRewardGroup ?? 0;
		if (!entry.RewardGroupProjections.TryGetValue(rewardGroup, out var projection))
		{
			return new QuestFinishRewardProjectionLookupPlan(
				QuestFinishRewardProjectionLookupStatus.MissingRewardGroupProjection,
				Projection: null,
				Diagnostics: []);
		}

		var preparedProjection = projection with
		{
			DialogActionId = input.DialogActionId,
			ExtendedRewardIndex = input.ExtendedRewardIndex,
			RewardRepeatCount = projection.RewardRepeatCount == 0
				? entry.Template.RewardRepeatCount
				: projection.RewardRepeatCount,
			PlayerClass = input.PlayerClass,
			TargetNpcId = input.TargetNpcId,
			HasTargetNpcTemplate = input.HasTargetNpcTemplate,
		};
		var diagnostics = CreateDiagnostics(input, preparedProjection);

		return new QuestFinishRewardProjectionLookupPlan(
			QuestFinishRewardProjectionLookupStatus.Found,
			preparedProjection,
			diagnostics);
	}

	private static IReadOnlyList<QuestFinishRewardProjectionLookupDiagnostic> CreateDiagnostics(
		QuestFinishRewardProjectionLookupInput input,
		QuestFinishRewardTemplateProjection projection)
	{
		var diagnostics = new List<QuestFinishRewardProjectionLookupDiagnostic>();
		if (string.IsNullOrWhiteSpace(input.PlayerClass)
			&& projection.ItemProjection?.ClassSelectableRewards.Count > 0)
		{
			diagnostics.Add(QuestFinishRewardProjectionLookupDiagnostic.MissingPlayerClassForClassSelectableReward);
		}

		if (input.TargetNpcId != 0
			&& !input.HasTargetNpcTemplate
			&& ((projection.NonItemProjection?.Experience ?? 0) != 0
				|| (projection.ExtendedNonItemProjection?.Experience ?? 0) != 0))
		{
			diagnostics.Add(QuestFinishRewardProjectionLookupDiagnostic.MissingTargetNpcTemplateForExperienceReward);
		}

		return diagnostics;
	}
}
