using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestFinishRewardPlanServiceTests
{
	[Fact]
	public void CorrectRewardGroup_DefaultsMissingRewardGroupToFirstJavaRewardGroup()
	{
		var questState = new PlayerQuestState(QuestId: 1001, Status: "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0);

		var result = QuestFinishRewardPlanService.CorrectRewardGroup(questState, rewardGroupCount: 2);

		Assert.Equal(QuestFinishRewardGroupCorrectionStatus.DefaultedFirstRewardGroup, result.Status);
		Assert.Null(result.OriginalRewardGroup);
		Assert.Equal(0, result.QuestState.RewardGroup);
	}

	[Fact]
	public void CorrectRewardGroup_ClearsRewardGroupWhenJavaRewardsAreMissing()
	{
		var questState = new PlayerQuestState(
			QuestId: 1001,
			Status: "REWARD",
			QuestVars: 0,
			Flags: 0,
			CompleteCount: 0,
			RewardGroup: 1);

		var result = QuestFinishRewardPlanService.CorrectRewardGroup(questState, rewardGroupCount: null);

		Assert.Equal(QuestFinishRewardGroupCorrectionStatus.ClearedMissingRewards, result.Status);
		Assert.Equal(1, result.OriginalRewardGroup);
		Assert.Null(result.QuestState.RewardGroup);
	}

	[Theory]
	[InlineData(-1, 3, 2)]
	[InlineData(5, 3, 2)]
	[InlineData(0, 0, -1)]
	public void CorrectRewardGroup_ClampsOutOfRangeRewardGroupLikeJava(
		int originalRewardGroup,
		int rewardGroupCount,
		int expectedRewardGroup)
	{
		var questState = new PlayerQuestState(
			QuestId: 1001,
			Status: "REWARD",
			QuestVars: 0,
			Flags: 0,
			CompleteCount: 0,
			RewardGroup: originalRewardGroup);

		var result = QuestFinishRewardPlanService.CorrectRewardGroup(questState, rewardGroupCount);

		Assert.Equal(QuestFinishRewardGroupCorrectionStatus.ClampedOutOfRange, result.Status);
		Assert.Equal(originalRewardGroup, result.OriginalRewardGroup);
		Assert.Equal(expectedRewardGroup, result.QuestState.RewardGroup);
	}

	[Fact]
	public void CorrectRewardGroup_IgnoresNonRewardStateLikeJavaGuard()
	{
		var questState = new PlayerQuestState(QuestId: 1001, Status: "START", QuestVars: 0, Flags: 0, CompleteCount: 0);

		var result = QuestFinishRewardPlanService.CorrectRewardGroup(questState, rewardGroupCount: 2);

		Assert.Equal(QuestFinishRewardGroupCorrectionStatus.IgnoredNonRewardState, result.Status);
		Assert.Same(questState, result.QuestState);
		Assert.Null(result.QuestState.RewardGroup);
	}

	[Fact]
	public void CreatePlan_EmitsNoDescriptorsWhenJavaRewardGuardFails()
	{
		var questState = new PlayerQuestState(QuestId: 1001, Status: "START", QuestVars: 0, Flags: 0, CompleteCount: 0);
		var projection = new QuestFinishRewardTemplateProjection(
			RewardGroupCount: 2,
			HasItemRewards: true,
			HasNonItemRewards: true,
			IsChallengeTask: true,
			WorkItems:
			[
				new QuestFinishRewardWorkItem(ItemId: 182400001, Count: 3),
			]);

		var plan = QuestFinishRewardPlanService.CreatePlan(questState, projection);

		Assert.Equal(QuestFinishRewardGroupCorrectionStatus.IgnoredNonRewardState, plan.CorrectionStatus);
		Assert.Empty(plan.Descriptors);
		Assert.Same(questState, plan.QuestState);
	}

	[Fact]
	public void CreatePlan_EmitsNonLiveRewardAndWorkItemDescriptors()
	{
		var questState = new PlayerQuestState(QuestId: 1001, Status: "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0);
		var projection = new QuestFinishRewardTemplateProjection(
			RewardGroupCount: 2,
			HasItemRewards: true,
			HasNonItemRewards: true,
			IsChallengeTask: true,
			WorkItems:
			[
				new QuestFinishRewardWorkItem(ItemId: 182400001, Count: 3),
			]);

		var plan = QuestFinishRewardPlanService.CreatePlan(questState, projection);

		Assert.Equal(QuestFinishRewardGroupCorrectionStatus.DefaultedFirstRewardGroup, plan.CorrectionStatus);
		Assert.Equal(0, plan.QuestState.RewardGroup);
		Assert.All(plan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.Equal(
		[
			QuestFinishRewardOperationAction.RewardGroupCorrection,
			QuestFinishRewardOperationAction.ItemRewardPlaceholder,
			QuestFinishRewardOperationAction.NonItemRewardPlaceholder,
			QuestFinishRewardOperationAction.ChallengeTaskCompletionPlaceholder,
			QuestFinishRewardOperationAction.RemoveQuestWorkItemsPlaceholder,
		], plan.Descriptors.Select(descriptor => descriptor.Action));
		Assert.Equal([1, 2, 3, 4, 5], plan.Descriptors.Select(descriptor => descriptor.Order));
		var workItemDescriptor = Assert.Single(
			plan.Descriptors,
			descriptor => descriptor.Action == QuestFinishRewardOperationAction.RemoveQuestWorkItemsPlaceholder);
		Assert.Equal(182400001, workItemDescriptor.ItemId);
		Assert.Equal(3, workItemDescriptor.Count);
	}
}
