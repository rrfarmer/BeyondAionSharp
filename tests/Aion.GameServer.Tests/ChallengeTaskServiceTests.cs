using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ChallengeTaskServiceTests
{
	private readonly ChallengeTaskService _service = new();

	[Fact]
	public void CanRaiseLegionLevel_ReturnsFalseWhenNoLoadedLegionLevelTaskMatchesJava()
	{
		var table = CreateChallengeTaskTable();

		var result = _service.CanRaiseLegionLevel(table, [], legionLevel: 5);

		Assert.False(result);
	}

	[Fact]
	public void CanRaiseLegionLevel_ReturnsFalseWhenLoadedTaskQuestIsIncompleteLikeJava()
	{
		var table = CreateChallengeTaskTable();
		var rows = new[]
		{
			new ChallengeTaskProgressRow(300, 17000, 6),
			new ChallengeTaskProgressRow(300, 17001, 11),
			new ChallengeTaskProgressRow(300, 17002, 42),
		};

		var result = _service.CanRaiseLegionLevel(table, rows, legionLevel: 5);

		Assert.False(result);
	}

	[Fact]
	public void CanRaiseLegionLevel_ReturnsTrueWhenLoadedTaskQuestsAreCompleteLikeJava()
	{
		var table = CreateChallengeTaskTable();
		var rows = CreateCompletedLevelFiveRows();

		var result = _service.CanRaiseLegionLevel(table, rows, legionLevel: 5);

		Assert.True(result);
	}

	[Fact]
	public void CanRaiseLegionLevel_DoesNotRequireOtherRaceTaskWhenOnlyOneLevelTaskIsLoadedLikeJava()
	{
		var table = CreateChallengeTaskTable();
		var rows = CreateCompletedLevelFiveRows();

		var result = _service.CanRaiseLegionLevel(table, rows, legionLevel: 5);

		Assert.True(result);
	}

	[Fact]
	public async Task BuildLegionTaskListAsync_CreatesStoresAndReturnsAvailableRaceTaskLikeJava()
	{
		var table = CreateChallengeTaskTable();
		var repository = new EmptyPlayerEnterWorldRepository();

		var result = await _service.BuildLegionTaskListAsync(table, repository, legionId: 77, legionLevel: 5, playerRace: "ELYOS");

		var task = Assert.Single(result);
		Assert.Equal(300, task.TaskId);
		Assert.Equal(0, task.CompleteTimeEpochSeconds);
		Assert.False(task.IsCompleted);
		Assert.Equal(1, repository.LoadLegionChallengeTasksCalls);
		var saved = Assert.Single(repository.SavedNewLegionChallengeTasks);
		Assert.Equal(77, saved.LegionId);
		Assert.Equal(300, saved.Task.TaskId);
		Assert.Collection(
			task.Quests,
			quest =>
			{
				Assert.Equal(17000, quest.QuestId);
				Assert.Equal(6, quest.MaxRepeats);
				Assert.Equal(5, quest.ScorePerQuest);
				Assert.Equal(0, quest.CompleteCount);
			},
			quest =>
			{
				Assert.Equal(17001, quest.QuestId);
				Assert.Equal(12, quest.MaxRepeats);
				Assert.Equal(6, quest.ScorePerQuest);
				Assert.Equal(0, quest.CompleteCount);
			},
			quest =>
			{
				Assert.Equal(17002, quest.QuestId);
				Assert.Equal(42, quest.MaxRepeats);
				Assert.Equal(7, quest.ScorePerQuest);
				Assert.Equal(0, quest.CompleteCount);
			});
	}

	[Fact]
	public async Task OnChallengeQuestFinishAsync_UpdatesLoadedLegionQuestProgressLikeJava()
	{
		var table = CreateChallengeTaskTable();
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedLegionChallengeTasks =
			[
				new ChallengeTaskProgressRow(300, 17000, CompleteCount: 5),
				new ChallengeTaskProgressRow(300, 17001, CompleteCount: 0),
				new ChallengeTaskProgressRow(300, 17002, CompleteCount: 0),
			],
		};
		var player = CreateLegionPlayer();

		var result = await _service.OnChallengeQuestFinishAsync(
			table,
			repository,
			player,
			questId: 17000,
			currentEpochSeconds: 1_800_000_000);

		Assert.True(result.Updated);
		Assert.Equal(ChallengeTaskFinishStatus.Updated, result.Status);
		Assert.Equal(300, result.TaskId);
		Assert.Equal(17000, result.QuestId);
		Assert.Equal(6, result.CompleteCount);
		var saved = Assert.Single(repository.SavedLegionChallengeTaskProgress);
		Assert.Equal((77, 300, 17000, 6, 1_800_000_000), saved);
		var availableTask = Assert.Single(result.AvailableTasks!);
		Assert.Equal(300, availableTask.TaskId);
		Assert.Equal(1_800_000_000, availableTask.CompleteTimeEpochSeconds);
		Assert.Equal(6, availableTask.Quests.Single(quest => quest.QuestId == 17000).CompleteCount);
	}

	[Fact]
	public async Task OnChallengeQuestFinishAsync_DoesNotUpdateCompletedLegionQuestLikeJava()
	{
		var table = CreateChallengeTaskTable();
		var repository = new EmptyPlayerEnterWorldRepository
		{
			LoadedLegionChallengeTasks =
			[
				new ChallengeTaskProgressRow(300, 17000, CompleteCount: 6, CompleteTimeEpochSeconds: 1_700_000_000),
			],
		};

		var result = await _service.OnChallengeQuestFinishAsync(
			table,
			repository,
			CreateLegionPlayer(),
			questId: 17000,
			currentEpochSeconds: 1_800_000_000);

		Assert.Equal(ChallengeTaskFinishStatus.AlreadyComplete, result.Status);
		Assert.Empty(repository.SavedLegionChallengeTaskProgress);
	}

	private static ChallengeTaskTable CreateChallengeTaskTable()
	{
		return new ChallengeTaskTable(
			[
				new ChallengeTaskSummary(
					300,
					"LEGION",
					"ELYOS",
					5,
					5,
					true,
					false,
					null,
					[
						new ChallengeQuestSummary(17000, 6, 5),
						new ChallengeQuestSummary(17001, 12, 6),
						new ChallengeQuestSummary(17002, 42, 7),
					]),
				new ChallengeTaskSummary(
					400,
					"LEGION",
					"ASMODIANS",
					5,
					5,
					true,
					false,
					null,
					[
						new ChallengeQuestSummary(27000, 6, 5),
						new ChallengeQuestSummary(27001, 12, 6),
						new ChallengeQuestSummary(27002, 42, 7),
					]),
			]);
	}

	private static ChallengeTaskProgressRow[] CreateCompletedLevelFiveRows()
	{
		return
		[
			new ChallengeTaskProgressRow(300, 17000, 6),
			new ChallengeTaskProgressRow(300, 17001, 12),
			new ChallengeTaskProgressRow(300, 17002, 42),
		];
	}

	private static Player CreateLegionPlayer()
	{
		return new Player
		{
			ObjectId = 1001,
			LegionId = 77,
			Race = "ELYOS",
			LegionLevel = 5,
		};
	}
}
