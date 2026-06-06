using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

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

	Task<ChallengeTaskFinishResult> OnChallengeQuestFinishAsync(
		ChallengeTaskTable challengeTasks,
		IPlayerEnterWorldRepository repository,
		Player player,
		int questId,
		int currentEpochSeconds,
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

public sealed record ChallengeTaskFinishResult(
	ChallengeTaskFinishStatus Status,
	int TaskId = 0,
	int QuestId = 0,
	int CompleteCount = 0,
	int CompleteTimeEpochSeconds = 0)
{
	public bool Updated => Status == ChallengeTaskFinishStatus.Updated;
}

public enum ChallengeTaskFinishStatus
{
	Updated,
	MissingTaskTemplate,
	TownTaskDeferred,
	UnsupportedTaskType,
	NoLegion,
	NoLoadedProgress,
	AlreadyComplete,
	PersistenceFailed,
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

	public async Task<ChallengeTaskFinishResult> OnChallengeQuestFinishAsync(
		ChallengeTaskTable challengeTasks,
		IPlayerEnterWorldRepository repository,
		Player player,
		int questId,
		int currentEpochSeconds,
		CancellationToken cancellationToken = default)
	{
		// Java parity: QuestService.finishQuest invokes ChallengeTaskService.onChallengeQuestFinish
		// for CHALLENGE_TASK templates, then the service dispatches by ChallengeTaskTemplate.type.
		var taskTemplate = challengeTasks.GetTaskByQuestId(questId);
		if (taskTemplate == null)
			return new ChallengeTaskFinishResult(ChallengeTaskFinishStatus.MissingTaskTemplate);

		if (string.Equals(taskTemplate.Type, "TOWN", StringComparison.Ordinal))
			return new ChallengeTaskFinishResult(ChallengeTaskFinishStatus.TownTaskDeferred, taskTemplate.TaskId, questId);

		if (!string.Equals(taskTemplate.Type, "LEGION", StringComparison.Ordinal))
			return new ChallengeTaskFinishResult(ChallengeTaskFinishStatus.UnsupportedTaskType, taskTemplate.TaskId, questId);

		if (player.LegionId <= 0)
			return new ChallengeTaskFinishResult(ChallengeTaskFinishStatus.NoLegion, taskTemplate.TaskId, questId);

		var questTemplate = taskTemplate.Quests.FirstOrDefault(quest => quest.QuestId == questId);
		if (questTemplate == null)
			return new ChallengeTaskFinishResult(ChallengeTaskFinishStatus.MissingTaskTemplate, taskTemplate.TaskId, questId);

		var loadedProgress = await repository.LoadLegionChallengeTasksAsync(player.LegionId, cancellationToken);
		var progress = loadedProgress.FirstOrDefault(row => row.TaskId == taskTemplate.TaskId && row.QuestId == questId);
		if (progress == null)
			return new ChallengeTaskFinishResult(ChallengeTaskFinishStatus.NoLoadedProgress, taskTemplate.TaskId, questId);

		if (progress.CompleteCount >= questTemplate.RepeatCount)
			return new ChallengeTaskFinishResult(
				ChallengeTaskFinishStatus.AlreadyComplete,
				taskTemplate.TaskId,
				questId,
				progress.CompleteCount,
				progress.CompleteTimeEpochSeconds);

		var completeCount = progress.CompleteCount + 1;
		var completeTime = Math.Max(0, currentEpochSeconds);
		var saved = await repository.SaveLegionChallengeTaskProgressAsync(
			player.LegionId,
			taskTemplate.TaskId,
			questId,
			completeCount,
			completeTime,
			cancellationToken);

		return saved
			? new ChallengeTaskFinishResult(ChallengeTaskFinishStatus.Updated, taskTemplate.TaskId, questId, completeCount, completeTime)
			: new ChallengeTaskFinishResult(
				ChallengeTaskFinishStatus.PersistenceFailed,
				taskTemplate.TaskId,
				questId,
				progress.CompleteCount,
				progress.CompleteTimeEpochSeconds);
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
