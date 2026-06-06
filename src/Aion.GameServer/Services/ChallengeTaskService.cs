using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public interface IChallengeTaskService
{
	bool CanRaiseLegionLevel(
		ChallengeTaskTable challengeTasks,
		IReadOnlyList<ChallengeTaskProgressRow> progressRows,
		int legionLevel);

	Task<IReadOnlyList<ChallengeTaskState>> BuildLegionTaskListAsync(
		ChallengeTaskTable challengeTasks,
		IPlayerEnterWorldRepository repository,
		int legionId,
		int legionLevel,
		string playerRace,
		CancellationToken cancellationToken = default);
}

public sealed record ChallengeQuestState(int QuestId, int MaxRepeats, int ScorePerQuest, int CompleteCount);

public sealed record ChallengeTaskState(
	int TaskId,
	int CompleteTimeEpochSeconds,
	bool IsRepeatable,
	IReadOnlyList<ChallengeQuestState> Quests)
{
	public bool IsCompleted => Quests.All(quest => quest.CompleteCount >= quest.MaxRepeats);
}

public sealed class ChallengeTaskService : IChallengeTaskService
{
	public bool CanRaiseLegionLevel(
		ChallengeTaskTable challengeTasks,
		IReadOnlyList<ChallengeTaskProgressRow> progressRows,
		int legionLevel)
	{
		// Java parity: ChallengeTaskService.canRaiseLegionLevel filters loaded LEGION tasks by
		// ChallengeTaskTemplate.isLegionLevelTask && template.minLevel == legion.getLegionLevel().
		var loadedRequiredTasks = progressRows
			.GroupBy(row => row.TaskId)
			.Select(group => new
			{
				Template = challengeTasks.GetTaskById(group.Key),
				ProgressByQuestId = group.ToDictionary(row => row.QuestId, row => row.CompleteCount),
			})
			.Where(task =>
				task.Template != null
				&& task.Template.IsLegionLevelTask
				&& task.Template.MinLevel == legionLevel
				&& string.Equals(task.Template.Type, "LEGION", StringComparison.Ordinal))
			.ToArray();

		if (loadedRequiredTasks.Length == 0)
			return false;

		foreach (var task in loadedRequiredTasks)
		{
			foreach (var quest in task.Template!.Quests)
			{
				if (!task.ProgressByQuestId.TryGetValue(quest.QuestId, out var completeCount) || completeCount < quest.RepeatCount)
					return false;
			}
		}

		return true;
	}

	public async Task<IReadOnlyList<ChallengeTaskState>> BuildLegionTaskListAsync(
		ChallengeTaskTable challengeTasks,
		IPlayerEnterWorldRepository repository,
		int legionId,
		int legionLevel,
		string playerRace,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ChallengeTaskService.buildTaskList loads stored owner tasks, sends repeatable or incomplete
		// tasks, then creates/stores newly available templates for the player's race and owner level.
		var loadedTasks = BuildStates(challengeTasks, await repository.LoadLegionChallengeTasksAsync(legionId, cancellationToken));
		var loadedByTaskId = loadedTasks.ToDictionary(task => task.TaskId);
		var availableTasks = loadedTasks
			.Where(task => task.IsRepeatable || !task.IsCompleted)
			.ToList();

		foreach (var template in challengeTasks.GetLegionTaskTemplatesForRaceAndLevel(playerRace, legionLevel))
		{
			if (loadedByTaskId.ContainsKey(template.TaskId))
				continue;

			if (template.PreviousTaskId.HasValue
				&& (!loadedByTaskId.TryGetValue(template.PreviousTaskId.Value, out var previousTask) || !previousTask.IsCompleted))
				continue;

			var task = CreateNewState(template);
			if (await repository.SaveNewLegionChallengeTaskAsync(legionId, template, cancellationToken))
			{
				loadedByTaskId[task.TaskId] = task;
				availableTasks.Add(task);
			}
		}

		return availableTasks;
	}

	private static IReadOnlyList<ChallengeTaskState> BuildStates(
		ChallengeTaskTable challengeTasks,
		IReadOnlyList<ChallengeTaskProgressRow> progressRows)
	{
		return progressRows
			.GroupBy(row => row.TaskId)
			.Select(group =>
			{
				var template = challengeTasks.GetTaskById(group.Key);
				if (template == null)
					return null;

				var progressByQuestId = group.ToDictionary(row => row.QuestId);
				var completeTime = group.Max(row => row.CompleteTimeEpochSeconds);
				return new ChallengeTaskState(
					template.TaskId,
					completeTime,
					template.IsRepeatable,
					template.Quests
						.Select(quest => new ChallengeQuestState(
							quest.QuestId,
							quest.RepeatCount,
							quest.Score,
							progressByQuestId.TryGetValue(quest.QuestId, out var progress) ? progress.CompleteCount : 0))
						.ToArray());
			})
			.Where(task => task != null)
			.Cast<ChallengeTaskState>()
			.ToArray();
	}

	private static ChallengeTaskState CreateNewState(ChallengeTaskSummary template)
	{
		return new ChallengeTaskState(
			template.TaskId,
			0,
			template.IsRepeatable,
			template.Quests
				.Select(quest => new ChallengeQuestState(quest.QuestId, quest.RepeatCount, quest.Score, 0))
				.ToArray());
	}
}
