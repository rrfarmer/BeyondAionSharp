using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestFinishStateMutationServiceTests
{
	[Fact]
	public void ApplyRewardCompletion_CompletesRewardQuestLikeJavaQuestState()
	{
		var now = new DateTimeOffset(2026, 5, 25, 14, 30, 0, TimeSpan.Zero);
		var questState = new PlayerQuestState(
			QuestId: 1001,
			Status: "REWARD",
			QuestVars: 0x123456,
			Flags: 7,
			CompleteCount: 0,
			RewardGroup: 2);

		var result = QuestFinishStateMutationService.ApplyRewardCompletion(
			questState,
			new NearbyQuestTemplateSummary(1001),
			now,
			CreateOptions("UTC"));

		Assert.True(result.Applied);
		Assert.Equal(QuestFinishStateMutationStatus.Applied, result.Status);
		Assert.NotNull(result.QuestState);
		Assert.Equal("COMPLETE", result.QuestState.Status);
		Assert.Equal(0, result.QuestState.QuestVars);
		Assert.Equal(7, result.QuestState.Flags);
		Assert.Equal(1, result.QuestState.CompleteCount);
		Assert.Equal(2, result.QuestState.RewardGroup);
		Assert.Equal(now, result.QuestState.CompleteTime);
		Assert.Null(result.QuestState.NextRepeatTime);
	}

	[Fact]
	public void ApplyRewardCompletion_SetsNextRepeatTimeForTimeBasedQuest()
	{
		var now = new DateTimeOffset(2026, 5, 25, 10, 0, 0, TimeSpan.Zero);
		var questState = new PlayerQuestState(
			QuestId: 2001,
			Status: "REWARD",
			QuestVars: 12,
			Flags: 0,
			CompleteCount: 1);

		var result = QuestFinishStateMutationService.ApplyRewardCompletion(
			questState,
			new NearbyQuestTemplateSummary(
				2001,
				MaxRepeatCount: 255,
				IsTimeBased: true,
				RepeatCycle: ["MON", "WED"]),
			now,
			CreateOptions("UTC"));

		Assert.True(result.Applied);
		Assert.NotNull(result.QuestState);
		Assert.Equal("COMPLETE", result.QuestState.Status);
		Assert.Equal(0, result.QuestState.QuestVars);
		Assert.Equal(2, result.QuestState.CompleteCount);
		Assert.Equal(now, result.QuestState.CompleteTime);
		Assert.Equal(new DateTimeOffset(2026, 5, 27, 9, 0, 0, TimeSpan.Zero), result.QuestState.NextRepeatTime);
	}

	[Fact]
	public void ApplyRewardCompletion_RejectsMissingQuestState()
	{
		var result = QuestFinishStateMutationService.ApplyRewardCompletion(
			null,
			new NearbyQuestTemplateSummary(3001),
			new DateTimeOffset(2026, 5, 25, 14, 30, 0, TimeSpan.Zero),
			CreateOptions("UTC"));

		Assert.False(result.Applied);
		Assert.Equal(QuestFinishStateMutationStatus.MissingQuestState, result.Status);
		Assert.Null(result.QuestState);
	}

	[Theory]
	[InlineData("START")]
	[InlineData("COMPLETE")]
	[InlineData("LOCKED")]
	public void ApplyRewardCompletion_RejectsNonRewardQuestState(string status)
	{
		var questState = new PlayerQuestState(4001, status, QuestVars: 3, Flags: 0, CompleteCount: 0);

		var result = QuestFinishStateMutationService.ApplyRewardCompletion(
			questState,
			new NearbyQuestTemplateSummary(4001),
			new DateTimeOffset(2026, 5, 25, 14, 30, 0, TimeSpan.Zero),
			CreateOptions("UTC"));

		Assert.False(result.Applied);
		Assert.Equal(QuestFinishStateMutationStatus.NotRewardState, result.Status);
		Assert.Same(questState, result.QuestState);
	}

	[Fact]
	public void ApplyRewardCompletion_RejectsRepeatedMissionCompletionLikeJava()
	{
		var questState = new PlayerQuestState(5001, "REWARD", QuestVars: 3, Flags: 0, CompleteCount: 1);

		var result = QuestFinishStateMutationService.ApplyRewardCompletion(
			questState,
			new NearbyQuestTemplateSummary(5001, QuestCategory: "MISSION"),
			new DateTimeOffset(2026, 5, 25, 14, 30, 0, TimeSpan.Zero),
			CreateOptions("UTC"));

		Assert.False(result.Applied);
		Assert.Equal(QuestFinishStateMutationStatus.MissionAlreadyCompleted, result.Status);
		Assert.Same(questState, result.QuestState);
	}

	[Fact]
	public void ApplyRewardCompletion_AllowsFirstMissionCompletion()
	{
		var now = new DateTimeOffset(2026, 5, 25, 14, 30, 0, TimeSpan.Zero);
		var questState = new PlayerQuestState(5002, "REWARD", QuestVars: 3, Flags: 0, CompleteCount: 0);

		var result = QuestFinishStateMutationService.ApplyRewardCompletion(
			questState,
			new NearbyQuestTemplateSummary(5002, QuestCategory: "MISSION"),
			now,
			CreateOptions("UTC"));

		Assert.True(result.Applied);
		Assert.NotNull(result.QuestState);
		Assert.Equal("COMPLETE", result.QuestState.Status);
		Assert.Equal(1, result.QuestState.CompleteCount);
		Assert.Equal(now, result.QuestState.CompleteTime);
	}

	private static GameServerOptions CreateOptions(string timeZoneId)
	{
		return new GameServerOptions
		{
			Core = new GameServerCoreOptions
			{
				TimeZoneId = timeZoneId,
			},
		};
	}
}
