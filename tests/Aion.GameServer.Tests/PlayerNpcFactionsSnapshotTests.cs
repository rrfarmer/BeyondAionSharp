using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerNpcFactionsSnapshotTests
{
	[Fact]
	public void CanStartAssignedQuest_MatchesJavaNpcFactionStartQuestGuard()
	{
		var snapshot = new PlayerNpcFactionsSnapshot(
		[
			new PlayerNpcFactionState(
				FactionId: 2,
				IsActive: true,
				IsMentor: false,
				TimeEpochSeconds: 0,
				State: PlayerNpcFactionQuestState.Noting,
				QuestId: 35007),
			new PlayerNpcFactionState(
				FactionId: 4,
				IsActive: false,
				IsMentor: false,
				TimeEpochSeconds: 0,
				State: PlayerNpcFactionQuestState.Noting,
				QuestId: 35008),
		]);

		Assert.True(snapshot.CanStartAssignedQuest(factionId: 2, questId: 35007));
		Assert.False(snapshot.CanStartAssignedQuest(factionId: 2, questId: 35008));
		Assert.False(snapshot.CanStartAssignedQuest(factionId: 4, questId: 35008));
		Assert.False(snapshot.CanStartAssignedQuest(factionId: 9, questId: 35009));
	}

	[Fact]
	public void CompleteActiveQuest_MatchesJavaNpcFactionCompletionForActiveSlot()
	{
		var snapshot = new PlayerNpcFactionsSnapshot(
		[
			new PlayerNpcFactionState(
				FactionId: 2,
				IsActive: true,
				IsMentor: false,
				TimeEpochSeconds: 0,
				State: PlayerNpcFactionQuestState.Start,
				QuestId: 35007),
		]);

		var result = snapshot.CompleteActiveQuest(isMentorQuest: false, nextResetEpochSeconds: 2000);

		Assert.True(result.Applied);
		Assert.Equal(PlayerNpcFactionCompletionStatus.Applied, result.Status);
		Assert.NotNull(result.CompletedFaction);
		Assert.Equal(2, result.CompletedFaction.FactionId);
		Assert.Equal(PlayerNpcFactionQuestState.Complete, result.CompletedFaction.State);
		Assert.Equal(2000, result.CompletedFaction.TimeEpochSeconds);
		Assert.Equal(35007, result.CompletedFaction.QuestId);
		Assert.True(result.Snapshot.HasActiveFaction(2));
		Assert.False(result.Snapshot.CanStartQuest(isMentorQuest: false, currentEpochSeconds: 2000));
		Assert.True(result.Snapshot.CanStartQuest(isMentorQuest: false, currentEpochSeconds: 2001));
	}

	[Fact]
	public void CompleteActiveQuest_UsesMentorSlotLikeJavaQuestTemplate()
	{
		var snapshot = new PlayerNpcFactionsSnapshot(
		[
			new PlayerNpcFactionState(
				FactionId: 2,
				IsActive: true,
				IsMentor: false,
				TimeEpochSeconds: 0,
				State: PlayerNpcFactionQuestState.Start,
				QuestId: 35007),
			new PlayerNpcFactionState(
				FactionId: 8,
				IsActive: true,
				IsMentor: true,
				TimeEpochSeconds: 0,
				State: PlayerNpcFactionQuestState.Start,
				QuestId: 35008),
		]);

		var result = snapshot.CompleteActiveQuest(isMentorQuest: true, nextResetEpochSeconds: 3000);

		Assert.True(result.Applied);
		Assert.NotNull(result.CompletedFaction);
		Assert.Equal(8, result.CompletedFaction.FactionId);
		Assert.True(result.Snapshot.TryGetFaction(2, out var dailyFaction));
		Assert.NotNull(dailyFaction);
		Assert.Equal(PlayerNpcFactionQuestState.Start, dailyFaction.State);
		Assert.Equal(0, dailyFaction.TimeEpochSeconds);
		Assert.True(result.Snapshot.TryGetFaction(8, out var mentorFaction));
		Assert.NotNull(mentorFaction);
		Assert.Equal(PlayerNpcFactionQuestState.Complete, mentorFaction.State);
		Assert.Equal(3000, mentorFaction.TimeEpochSeconds);
	}

	[Fact]
	public void CompleteActiveQuest_ReturnsNoActiveFactionWhenSlotIsEmpty()
	{
		var snapshot = new PlayerNpcFactionsSnapshot(
		[
			new PlayerNpcFactionState(
				FactionId: 2,
				IsActive: true,
				IsMentor: false,
				TimeEpochSeconds: 0,
				State: PlayerNpcFactionQuestState.Start,
				QuestId: 35007),
		]);

		var result = snapshot.CompleteActiveQuest(isMentorQuest: true, nextResetEpochSeconds: 3000);

		Assert.False(result.Applied);
		Assert.Equal(PlayerNpcFactionCompletionStatus.NoActiveFaction, result.Status);
		Assert.Same(snapshot, result.Snapshot);
		Assert.Null(result.CompletedFaction);
	}

	[Theory]
	[InlineData(8, 59, 2026, 5, 25)]
	[InlineData(9, 0, 2026, 5, 26)]
	[InlineData(10, 0, 2026, 5, 26)]
	public void NpcFactionDailyResetService_AppliesJavaNineAmBoundary(
		int hour,
		int minute,
		int expectedYear,
		int expectedMonth,
		int expectedDay)
	{
		var now = new DateTimeOffset(2026, 5, 25, hour, minute, 0, TimeSpan.Zero);
		var expectedReset = new DateTimeOffset(expectedYear, expectedMonth, expectedDay, 9, 0, 0, TimeSpan.Zero);

		var nextReset = NpcFactionDailyResetService.GetNextResetEpochSeconds(now, CreateOptions("UTC"));

		Assert.Equal(expectedReset.ToUnixTimeSeconds(), nextReset);
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
