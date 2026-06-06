using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public interface IChallengeTaskService
{
	bool CanRaiseLegionLevel(
		ChallengeTaskTable challengeTasks,
		IReadOnlyList<ChallengeTaskProgressRow> progressRows,
		int legionLevel);
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
}
