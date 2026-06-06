namespace Aion.GameServer.Dataholders;

public sealed record ChallengeQuestSummary(int QuestId, int RepeatCount);

public sealed record ChallengeTaskSummary(
	int TaskId,
	string Type,
	string Race,
	int MinLevel,
	int MaxLevel,
	bool IsLegionLevelTask,
	IReadOnlyList<ChallengeQuestSummary> Quests);

public sealed class ChallengeTaskTable
{
	private readonly IReadOnlyDictionary<int, ChallengeTaskSummary> _tasksById;
	private readonly IReadOnlyList<ChallengeTaskSummary> _legionLevelTasks;

	public ChallengeTaskTable(IReadOnlyList<ChallengeTaskSummary> tasks)
	{
		_tasksById = tasks.ToDictionary(task => task.TaskId);
		_legionLevelTasks = tasks
			.Where(task => task.IsLegionLevelTask && string.Equals(task.Type, "LEGION", StringComparison.Ordinal))
			.ToArray();
	}

	public int Count => _tasksById.Count;

	public ChallengeTaskSummary? GetTaskById(int taskId)
	{
		return _tasksById.GetValueOrDefault(taskId);
	}

	public IReadOnlyList<ChallengeTaskSummary> GetRequiredLegionLevelTasks(int legionLevel)
	{
		// Java parity: ChallengeTaskService.canRaiseLegionLevel filters by isLegionLevelTask and minLevel == legion level.
		return _legionLevelTasks
			.Where(task => task.MinLevel == legionLevel)
			.ToArray();
	}
}
