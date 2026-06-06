namespace Aion.GameServer.Dataholders;

public sealed record ChallengeQuestSummary(int QuestId, int RepeatCount, int Score);

public sealed record ChallengeTaskSummary(
	int TaskId,
	string Type,
	string Race,
	int MinLevel,
	int MaxLevel,
	bool IsLegionLevelTask,
	bool IsRepeatable,
	int? PreviousTaskId,
	IReadOnlyList<ChallengeQuestSummary> Quests);

public sealed class ChallengeTaskTable
{
	private readonly IReadOnlyDictionary<int, ChallengeTaskSummary> _tasksById;
	private readonly IReadOnlyList<ChallengeTaskSummary> _legionTasks;
	private readonly IReadOnlyList<ChallengeTaskSummary> _legionLevelTasks;

	public ChallengeTaskTable(IReadOnlyList<ChallengeTaskSummary> tasks)
	{
		_tasksById = tasks.ToDictionary(task => task.TaskId);
		_legionTasks = tasks
			.Where(task => string.Equals(task.Type, "LEGION", StringComparison.Ordinal))
			.ToArray();
		_legionLevelTasks = _legionTasks
			.Where(task => task.IsLegionLevelTask)
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

	public IReadOnlyList<ChallengeTaskSummary> GetLegionTaskTemplatesForRaceAndLevel(string race, int legionLevel)
	{
		// Java parity: ChallengeTaskService.buildTaskList filters template type, player race, and owner level range.
		return _legionTasks
			.Where(task =>
				string.Equals(task.Race, race, StringComparison.Ordinal)
				&& legionLevel >= task.MinLevel
				&& legionLevel <= task.MaxLevel)
			.ToArray();
	}
}
