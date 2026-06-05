using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
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

	[Fact]
	public void GetReusableDailyQuestIds_MatchesJavaSendDailyQuestAssignedQuestBranch()
	{
		var snapshot = new PlayerNpcFactionsSnapshot(
		[
			new PlayerNpcFactionState(
				FactionId: 2,
				IsActive: true,
				IsMentor: false,
				TimeEpochSeconds: 2_000,
				State: PlayerNpcFactionQuestState.Noting,
				QuestId: 35007),
			new PlayerNpcFactionState(
				FactionId: 8,
				IsActive: true,
				IsMentor: true,
				TimeEpochSeconds: 2_500,
				State: PlayerNpcFactionQuestState.Noting,
				QuestId: 47000),
			new PlayerNpcFactionState(
				FactionId: 12,
				IsActive: false,
				IsMentor: false,
				TimeEpochSeconds: 2_500,
				State: PlayerNpcFactionQuestState.Noting,
				QuestId: 48000),
		]);

		var questIds = snapshot.GetReusableDailyQuestIds(currentEpochSeconds: 1_000);

		Assert.Equal([35007, 47000], questIds);
	}

	[Theory]
	[InlineData(PlayerNpcFactionQuestState.Noting, 900, 35007)]
	[InlineData(PlayerNpcFactionQuestState.Noting, 2_000, 0)]
	[InlineData(PlayerNpcFactionQuestState.Start, 2_000, 35007)]
	[InlineData(PlayerNpcFactionQuestState.Complete, 2_000, 35007)]
	public void GetReusableDailyQuestIds_SkipsBranchesThatRequireRandomSelectionOrNoPacket(
		PlayerNpcFactionQuestState state,
		int timeEpochSeconds,
		int questId)
	{
		var snapshot = new PlayerNpcFactionsSnapshot(
		[
			new PlayerNpcFactionState(
				FactionId: 2,
				IsActive: true,
				IsMentor: false,
				TimeEpochSeconds: timeEpochSeconds,
				State: state,
				QuestId: questId),
		]);

		Assert.Empty(snapshot.GetReusableDailyQuestIds(currentEpochSeconds: 1_000));
	}

	[Fact]
	public void LevelUpPlan_DeactivatesOverLevelActiveFactionAndRecordsJavaSideEffects()
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
				State: PlayerNpcFactionQuestState.Noting,
				QuestId: 35008),
		]);
		var table = CreateFactionTable(
			new NpcFactionSummary(2, "Alabaster Order", 1129000, "DAILY", 30, 45, "ELYOS", [799803], 0),
			new NpcFactionSummary(8, "Kaisinel Academy", 1129006, "MENTOR", 10, 65, "ELYOS", [799813], 0));

		var plan = NpcFactionLevelUpPlanService.CreatePlan(snapshot, playerLevel: 46, table);

		Assert.True(plan.Applied);
		Assert.Equal(NpcFactionLevelUpPlanStatus.Applied, plan.Status);
		Assert.True(plan.PlannedSnapshot.TryGetFaction(2, out var dailyFaction));
		Assert.NotNull(dailyFaction);
		Assert.False(dailyFaction.IsActive);
		Assert.Equal(PlayerNpcFactionQuestState.Noting, dailyFaction.State);
		Assert.Equal(35007, dailyFaction.QuestId);
		Assert.True(plan.PlannedSnapshot.TryGetFaction(8, out var mentorFaction));
		Assert.NotNull(mentorFaction);
		Assert.True(mentorFaction.IsActive);
		Assert.Equal(PlayerNpcFactionQuestState.Noting, mentorFaction.State);

		Assert.Collection(
			plan.Descriptors,
			descriptor =>
			{
				Assert.False(descriptor.IsMentorSlot);
				Assert.Equal(2, descriptor.FactionId);
				Assert.Equal(NpcFactionLevelUpDescriptorStatus.PlannedLeaveByLevelLimit, descriptor.Status);
				Assert.Equal(45, descriptor.TemplateMaxLevel);
				Assert.Equal(1129000, descriptor.TemplateNameId);
				Assert.Equal(35007, descriptor.QuestIdToAbandon);
				Assert.Equal(NpcFactionLevelUpPlanService.FactionLeaveByLevelLimitSystemMessageId, descriptor.SystemMessageId);
				Assert.False(descriptor.IsLive);
			},
			descriptor =>
			{
				Assert.True(descriptor.IsMentorSlot);
				Assert.Equal(8, descriptor.FactionId);
				Assert.Equal(NpcFactionLevelUpDescriptorStatus.WithinLevelLimit, descriptor.Status);
				Assert.Null(descriptor.QuestIdToAbandon);
				Assert.Null(descriptor.SystemMessageId);
			});
	}

	[Fact]
	public void LevelUpPlan_RecordsNoChangesMissingTemplateAndNoActiveBranches()
	{
		var activeSnapshot = new PlayerNpcFactionsSnapshot(
		[
			new PlayerNpcFactionState(
				FactionId: 2,
				IsActive: true,
				IsMentor: false,
				TimeEpochSeconds: 0,
				State: PlayerNpcFactionQuestState.Complete,
				QuestId: 35007),
		]);
		var inactiveSnapshot = new PlayerNpcFactionsSnapshot(
		[
			new PlayerNpcFactionState(
				FactionId: 2,
				IsActive: false,
				IsMentor: false,
				TimeEpochSeconds: 0,
				State: PlayerNpcFactionQuestState.Noting,
				QuestId: 35007),
		]);
		var table = CreateFactionTable(new NpcFactionSummary(2, "Alabaster Order", 1129000, "DAILY", 30, 45, "ELYOS", [799803], 0));

		var noChanges = NpcFactionLevelUpPlanService.CreatePlan(activeSnapshot, playerLevel: 45, table);
		var missingTemplate = NpcFactionLevelUpPlanService.CreatePlan(activeSnapshot, playerLevel: 46, CreateFactionTable());
		var missingTable = NpcFactionLevelUpPlanService.CreatePlan(activeSnapshot, playerLevel: 46, npcFactionTable: null);
		var noActive = NpcFactionLevelUpPlanService.CreatePlan(inactiveSnapshot, playerLevel: 46, table);
		var missingSnapshot = NpcFactionLevelUpPlanService.CreatePlan(npcFactions: null, playerLevel: 46, table);

		Assert.Equal(NpcFactionLevelUpPlanStatus.NoChanges, noChanges.Status);
		Assert.Single(noChanges.Descriptors);
		Assert.Equal(NpcFactionLevelUpDescriptorStatus.WithinLevelLimit, noChanges.Descriptors[0].Status);
		Assert.True(noChanges.PlannedSnapshot.HasActiveFaction(2));
		Assert.Equal(NpcFactionLevelUpPlanStatus.BlockedMissingTemplate, missingTemplate.Status);
		Assert.Equal(NpcFactionLevelUpDescriptorStatus.MissingTemplate, Assert.Single(missingTemplate.Descriptors).Status);
		Assert.Equal(NpcFactionLevelUpPlanStatus.BlockedMissingTemplate, missingTable.Status);
		Assert.Equal(NpcFactionLevelUpPlanStatus.NoActiveFactions, noActive.Status);
		Assert.Empty(noActive.Descriptors);
		Assert.Equal(NpcFactionLevelUpPlanStatus.MissingSnapshot, missingSnapshot.Status);
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

	private static NpcFactionTable CreateFactionTable(params NpcFactionSummary[] factions)
	{
		return new NpcFactionTable(factions);
	}
}
