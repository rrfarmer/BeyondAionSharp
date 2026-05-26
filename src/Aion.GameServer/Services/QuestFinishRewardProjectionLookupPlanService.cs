using Aion.GameServer.Dataholders;

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

	public bool TryGetQuest(int questId, out QuestFinishRewardProjectionLookupEntry? entry)
	{
		return _entries.TryGetValue(questId, out entry);
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
