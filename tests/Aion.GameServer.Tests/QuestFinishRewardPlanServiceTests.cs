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
		var template = new QuestFinishRewardItemTemplateProjection(
			HasBonus: true,
			BonusProjection: new QuestFinishRewardBonusTemplateProjection(
				"FOOD",
				Level: 20,
				SupportStatus: QuestFinishRewardBonusSupportStatus.SupportedByJavaBonusService));

		var plan = QuestFinishRewardPlanService.CreateRewardItemProjection(input, template);

		var warning = Assert.Single(plan.Warnings);
		Assert.Equal(QuestFinishRewardItemProjectionWarning.BonusHandlerNotProjected, warning.Warning);
		Assert.Equal("FOOD", warning.BonusType);
		Assert.Equal(20, warning.BonusLevel);
		Assert.Equal(QuestFinishRewardBonusSupportStatus.SupportedByJavaBonusService, warning.BonusSupportStatus);
		Assert.Empty(plan.Items);
	}

	[Fact]
	public void CreateNonItemRewardProjection_EmitsJavaGiveRewardOrderAndRateMetadata()
	{
		var input = new QuestFinishRewardNonItemProjectionInput(
			QuestId: 1001,
			QuestCategory: "QUEST",
			TargetNpcId: 203001,
			HasTargetNpcTemplate: true);
		var template = new QuestFinishRewardNonItemTemplateProjection(
			Kinah: 1_000,
			Experience: 2_000,
			Title: 10,
			AbyssPoints: 30,
			DivinePoints: 40,
			GloryPoints: 50,
			ExtendInventory: 1);

		var plan = QuestFinishRewardPlanService.CreateNonItemRewardProjection(input, template);

		Assert.Empty(plan.Warnings);
		Assert.All(plan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.All(plan.Descriptors, descriptor => Assert.Equal(QuestFinishRewardNonItemSource.Regular, descriptor.Source));
		Assert.Equal(
		[
			QuestFinishRewardNonItemAction.Kinah,
			QuestFinishRewardNonItemAction.Experience,
			QuestFinishRewardNonItemAction.Title,
			QuestFinishRewardNonItemAction.AbyssPoints,
			QuestFinishRewardNonItemAction.DivinePoints,
			QuestFinishRewardNonItemAction.GloryPoints,
			QuestFinishRewardNonItemAction.CubeExpansion,
		], plan.Descriptors.Select(descriptor => descriptor.Action));
		Assert.Equal(Enumerable.Range(1, 7), plan.Descriptors.Select(descriptor => descriptor.Order));
		Assert.Equal("Rates.QUEST_KINAH", plan.Descriptors[0].RateSource);
		Assert.Equal("Rates.XP_QUEST", plan.Descriptors[1].RateSource);
		Assert.True(plan.Descriptors[1].RequiresTargetNpcL10nLookup);
		Assert.Equal(203001, plan.Descriptors[1].TargetNpcId);
		Assert.Equal("Rates.AP_QUEST", plan.Descriptors[3].RateSource);
		Assert.Equal("Rates.GP", plan.Descriptors[5].RateSource);
	}

	[Fact]
	public void CreateNonItemRewardProjection_SkipsZerosAndBypassesApRateForNonCountQuest()
	{
		var input = new QuestFinishRewardNonItemProjectionInput(
			QuestId: 1001,
			QuestCategory: "NON_COUNT");
		var template = new QuestFinishRewardNonItemTemplateProjection(AbyssPoints: 500);

		var plan = QuestFinishRewardPlanService.CreateNonItemRewardProjection(input, template);

		var descriptor = Assert.Single(plan.Descriptors);
		Assert.Equal(QuestFinishRewardNonItemAction.AbyssPoints, descriptor.Action);
		Assert.Equal(500, descriptor.Amount);
		Assert.Null(descriptor.RateSource);
		Assert.True(descriptor.RateBypassed);
		Assert.Empty(plan.Warnings);
	}

	[Fact]
	public void CreateNonItemRewardProjection_ProjectsWarehouseExpansionAndIgnoredXmlWarnings()
	{
		var template = new QuestFinishRewardNonItemTemplateProjection(
			ExtendInventory: 2,
			ExtendStigma: 1,
			CollectItemChecks: [182400001, 182400002],
			InventoryItemCheck: 99);

		var plan = QuestFinishRewardPlanService.CreateNonItemRewardProjection(
			new QuestFinishRewardNonItemProjectionInput(
				QuestId: 1001,
				Source: QuestFinishRewardNonItemSource.Extended),
			template);

		var descriptor = Assert.Single(plan.Descriptors);
		Assert.Equal(QuestFinishRewardNonItemAction.WarehouseExpansion, descriptor.Action);
		Assert.Equal(2, descriptor.Amount);
		Assert.Equal(QuestFinishRewardNonItemSource.Extended, descriptor.Source);
		Assert.Equal(
		[
			"extend_stigma",
			"ccheck",
			"icheck",
		], plan.Warnings.Select(warning => warning.FieldName));
		Assert.All(
			plan.Warnings,
			warning =>
			{
				Assert.Equal(
					QuestFinishRewardNonItemProjectionWarning.XmlFieldIgnoredByJavaGiveReward,
					warning.Warning);
				Assert.Equal(QuestFinishRewardNonItemSource.Extended, warning.Source);
			});
	}

	[Fact]
	public void CreateNonItemRewardProjection_RecordsUnsupportedExtendInventoryValues()
	{
		var template = new QuestFinishRewardNonItemTemplateProjection(ExtendInventory: 3);

		var plan = QuestFinishRewardPlanService.CreateNonItemRewardProjection(
			new QuestFinishRewardNonItemProjectionInput(QuestId: 1001),
			template);

		Assert.Empty(plan.Descriptors);
		var warning = Assert.Single(plan.Warnings);
		Assert.Equal(QuestFinishRewardNonItemProjectionWarning.UnsupportedExtendInventoryValue, warning.Warning);
		Assert.Equal("extend_inventory", warning.FieldName);
		Assert.Equal(3, warning.Value);
	}
}
