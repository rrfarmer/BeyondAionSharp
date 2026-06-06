using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
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
					[
						new ChallengeQuestSummary(17000, 6),
						new ChallengeQuestSummary(17001, 12),
						new ChallengeQuestSummary(17002, 42),
					]),
				new ChallengeTaskSummary(
					400,
					"LEGION",
					"ASMODIANS",
					5,
					5,
					true,
					[
						new ChallengeQuestSummary(27000, 6),
						new ChallengeQuestSummary(27001, 12),
						new ChallengeQuestSummary(27002, 42),
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
}
