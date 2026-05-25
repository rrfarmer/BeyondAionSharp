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

	[Fact]
	public void CreateRewardItemProjection_AddsExtendedRewardsBeforeRegularRewardsLikeJava()
	{
		var input = new QuestFinishRewardItemProjectionInput(
			QuestId: 1001,
			DialogActionId: 8,
			ExtendedRewardIndex: null,
			CompleteCount: 2,
			RewardRepeatCount: 3,
			RewardGroup: 0);
		var template = new QuestFinishRewardItemTemplateProjection(
			RewardGroups:
			[
				new QuestFinishRewardGroupProjection(
					RewardGroupIndex: 0,
					FixedRewardItems: [new QuestFinishRewardItem(ItemId: 182400001, Count: 2)],
					SelectableRewardItems: [new QuestFinishRewardItem(ItemId: 182400002, Count: 1)]),
			],
			ExtendedRewards: new QuestFinishRewardGroupProjection(
				RewardGroupIndex: -1,
				FixedRewardItems: [new QuestFinishRewardItem(ItemId: 186000001, Count: 5)]));

		var plan = QuestFinishRewardPlanService.CreateRewardItemProjection(input, template);

		Assert.Empty(plan.Warnings);
		Assert.Equal(
		[
			QuestFinishRewardItemSource.ExtendedFixed,
			QuestFinishRewardItemSource.RegularFixed,
			QuestFinishRewardItemSource.RegularSelectable,
		], plan.Items.Select(item => item.Source));
		Assert.Equal([186000001, 182400001, 182400002], plan.Items.Select(item => item.ItemId));
		Assert.Equal([1, 2, 3], plan.Items.Select(item => item.Order));
		Assert.All(plan.Items, item => Assert.False(item.IsLive));
	}

	[Fact]
	public void CreateRewardItemProjection_UsesExtendedIndexMinusEightBeforeMinusOneLikeJava()
	{
		var input = new QuestFinishRewardItemProjectionInput(
			QuestId: 1001,
			DialogActionId: 23,
			ExtendedRewardIndex: 9,
			CompleteCount: 0,
			RewardRepeatCount: 1,
			RewardGroup: null);
		var selectable = Enumerable.Range(0, 9)
			.Select(index => new QuestFinishRewardItem(ItemId: 182500000 + index))
			.ToArray();
		var template = new QuestFinishRewardItemTemplateProjection(
			ExtendedRewards: new QuestFinishRewardGroupProjection(
				RewardGroupIndex: -1,
				SelectableRewardItems: selectable));

		var plan = QuestFinishRewardPlanService.CreateRewardItemProjection(input, template);

		var item = Assert.Single(plan.Items);
		Assert.Equal(QuestFinishRewardItemSource.ExtendedSelectable, item.Source);
		Assert.Equal(1, item.SelectableIndex);
		Assert.Equal(182500001, item.ItemId);
		Assert.Empty(plan.Warnings);
	}

	[Fact]
	public void CreateRewardItemProjection_UsesClassSelectableRewardsOnLastRepeat()
	{
		var input = new QuestFinishRewardItemProjectionInput(
			QuestId: 1001,
			DialogActionId: 9,
			ExtendedRewardIndex: null,
			CompleteCount: 4,
			RewardRepeatCount: 5,
			RewardGroup: 0,
			PlayerClass: "RANGER");
		var template = new QuestFinishRewardItemTemplateProjection(
			RewardGroups:
			[
				new QuestFinishRewardGroupProjection(
					RewardGroupIndex: 0,
					SelectableRewardItems:
					[
						new QuestFinishRewardItem(ItemId: 100),
						new QuestFinishRewardItem(ItemId: 101),
					]),
			],
			ClassSelectableRewards: new Dictionary<string, IReadOnlyList<QuestFinishRewardItem>>
			{
				["RANGER"] =
				[
					new QuestFinishRewardItem(ItemId: 182600000),
					new QuestFinishRewardItem(ItemId: 182600001),
				],
			},
			SingleTimeClassReward: true);

		var plan = QuestFinishRewardPlanService.CreateRewardItemProjection(input, template);

		var item = Assert.Single(plan.Items);
		Assert.Equal(QuestFinishRewardItemSource.ClassSelectable, item.Source);
		Assert.Equal(1, item.SelectableIndex);
		Assert.Equal("RANGER", item.PlayerClass);
		Assert.Equal(182600001, item.ItemId);
		Assert.Empty(plan.Warnings);
	}

	[Fact]
	public void CreateRewardItemProjection_ProjectsNoRewardClassSelectionFromExtendedIndex()
	{
		var input = new QuestFinishRewardItemProjectionInput(
			QuestId: 1001,
			DialogActionId: 23,
			ExtendedRewardIndex: 10,
			CompleteCount: 1,
			RewardRepeatCount: 3,
			RewardGroup: 0,
			PlayerClass: "CLERIC");
		var template = new QuestFinishRewardItemTemplateProjection(
			RewardGroups:
			[
				new QuestFinishRewardGroupProjection(RewardGroupIndex: 0),
			],
			ClassSelectableRewards: new Dictionary<string, IReadOnlyList<QuestFinishRewardItem>>
			{
				["CLERIC"] =
				[
					new QuestFinishRewardItem(ItemId: 182700000),
					new QuestFinishRewardItem(ItemId: 182700001),
					new QuestFinishRewardItem(ItemId: 182700002),
				],
			},
			ClassRewardOnEveryRepeat: true);

		var plan = QuestFinishRewardPlanService.CreateRewardItemProjection(input, template);

		var item = Assert.Single(plan.Items);
		Assert.Equal(QuestFinishRewardItemSource.ClassSelectable, item.Source);
		Assert.Equal(2, item.SelectableIndex);
		Assert.Equal(182700002, item.ItemId);
		Assert.Empty(plan.Warnings);
	}

	[Fact]
	public void CreateRewardItemProjection_LeavesBonusAsExplicitProjectionWarning()
	{
		var input = new QuestFinishRewardItemProjectionInput(
			QuestId: 1001,
			DialogActionId: 8,
			ExtendedRewardIndex: null,
			CompleteCount: 0,
			RewardRepeatCount: 1,
			RewardGroup: null);
		var template = new QuestFinishRewardItemTemplateProjection(HasBonus: true);

		var plan = QuestFinishRewardPlanService.CreateRewardItemProjection(input, template);

		var warning = Assert.Single(plan.Warnings);
		Assert.Equal(QuestFinishRewardItemProjectionWarning.BonusHandlerNotProjected, warning.Warning);
		Assert.Empty(plan.Items);
	}
}
