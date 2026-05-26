using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestFinishStaticRewardProjectionCompositionTests
{
	[Fact]
	public void StaticExtendedItemRewardProjection_ComposesOnLastRepeatBeforeRegularRewardsWithoutLiveSideEffects()
	{
		const string xml = """
			<quests>
				<quest id="1003" can_report="true" category="QUEST" reward_repeat_count="3">
					<rewards>
						<reward_item item_id="182400001" count="2" />
					</rewards>
					<extended_rewards>
						<reward_item item_id="186000001" count="5" />
						<selectable_reward_item item_id="186000010" count="6" />
						<selectable_reward_item item_id="186000011" count="7" />
					</extended_rewards>
				</quest>
			</quests>
			""";
		var template = new NearbyQuestTemplateXmlExtractor().Extract(xml).Single();
		var rewardProjection = new QuestFinishRewardTemplateXmlProjectionExtractor()
			.ExtractDefaultRegularNonItemProjections(xml)[1003] with
		{
			DialogActionId = 23,
			ExtendedRewardIndex = 9
		};

		var operationPlan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(1003, "REWARD", QuestVars: 0x12, Flags: 0, CompleteCount: 2),
			template,
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 26, 10, 0, 0, TimeSpan.Zero),
			CreateOptions("UTC"),
			rewardProjection);

		Assert.True(rewardProjection.HasItemRewards);
		Assert.NotNull(rewardProjection.ItemProjection?.ExtendedRewards);
		Assert.True(operationPlan.Applied);
		Assert.Equal("COMPLETE", operationPlan.QuestState?.Status);
		Assert.All(operationPlan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		var projectedItems = operationPlan.Descriptors
			.Where(descriptor => descriptor.Action == QuestFinishOperationAction.ItemRewardProjection)
			.ToArray();
		Assert.Collection(
			projectedItems,
			descriptor =>
			{
				Assert.Equal(QuestFinishRewardItemSource.ExtendedFixed, descriptor.RewardItemProjection?.Source);
				Assert.Equal(186000001, descriptor.ItemId);
				Assert.Equal(5, descriptor.Count);
				Assert.Equal(-1, descriptor.RewardItemProjection?.RewardGroupIndex);
				Assert.Null(descriptor.RewardItemProjection?.SelectableIndex);
			},
			descriptor =>
			{
				Assert.Equal(QuestFinishRewardItemSource.ExtendedSelectable, descriptor.RewardItemProjection?.Source);
				Assert.Equal(186000011, descriptor.ItemId);
				Assert.Equal(7, descriptor.Count);
				Assert.Equal(-1, descriptor.RewardItemProjection?.RewardGroupIndex);
				Assert.Equal(1, descriptor.RewardItemProjection?.SelectableIndex);
			},
			descriptor =>
			{
				Assert.Equal(QuestFinishRewardItemSource.RegularFixed, descriptor.RewardItemProjection?.Source);
				Assert.Equal(182400001, descriptor.ItemId);
				Assert.Equal(2, descriptor.Count);
				Assert.Equal(0, descriptor.RewardItemProjection?.RewardGroupIndex);
			});
		Assert.DoesNotContain(operationPlan.Descriptors, descriptor => descriptor.Action == QuestFinishOperationAction.ItemRewardProjectionWarning);
		Assert.Contains(operationPlan.Descriptors, descriptor => descriptor.Action == QuestFinishOperationAction.ItemRewardPlaceholder);
		Assert.True(
			IndexOf(operationPlan.Descriptors, QuestFinishOperationAction.ItemRewardProjection)
			< IndexOf(operationPlan.Descriptors, QuestFinishOperationAction.ItemRewardPlaceholder));
		Assert.True(
			IndexOf(operationPlan.Descriptors, QuestFinishOperationAction.ItemRewardPlaceholder)
			< IndexOf(operationPlan.Descriptors, QuestFinishOperationAction.QuestStateMutation));
	}

	[Fact]
	public void StaticRegularItemRewardProjection_ComposesThroughRewardAndOperationPlansWithoutLiveSideEffects()
	{
		const string xml = """
			<quests>
				<quest id="1002" can_report="true" category="QUEST" reward_repeat_count="1">
					<rewards>
						<reward_item item_id="182400001" count="2" />
						<reward_item item_id="182400002" />
						<selectable_reward_item item_id="182400010" count="3" />
						<selectable_reward_item item_id="182400011" />
					</rewards>
				</quest>
			</quests>
			""";
		var template = new NearbyQuestTemplateXmlExtractor().Extract(xml).Single();
		var rewardProjection = new QuestFinishRewardTemplateXmlProjectionExtractor()
			.ExtractDefaultRegularNonItemProjections(xml)[1002] with
		{
			DialogActionId = 8
		};

		var operationPlan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(1002, "REWARD", QuestVars: 0x12, Flags: 0, CompleteCount: 0),
			template,
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 26, 9, 0, 0, TimeSpan.Zero),
			CreateOptions("UTC"),
			rewardProjection);

		Assert.True(rewardProjection.HasItemRewards);
		Assert.False(rewardProjection.HasNonItemRewards);
		Assert.True(operationPlan.Applied);
		Assert.Equal("COMPLETE", operationPlan.QuestState?.Status);
		Assert.All(operationPlan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.DoesNotContain(operationPlan.Descriptors, descriptor => descriptor.Action == QuestFinishOperationAction.RewardMutationPlaceholder);
		var projectedItems = operationPlan.Descriptors
			.Where(descriptor => descriptor.Action == QuestFinishOperationAction.ItemRewardProjection)
			.ToArray();
		Assert.Collection(
			projectedItems,
			descriptor =>
			{
				Assert.Equal(QuestFinishRewardItemSource.RegularFixed, descriptor.RewardItemProjection?.Source);
				Assert.Equal(182400001, descriptor.ItemId);
				Assert.Equal(2, descriptor.Count);
				Assert.Equal(0, descriptor.RewardItemProjection?.RewardGroupIndex);
				Assert.Null(descriptor.RewardItemProjection?.SelectableIndex);
			},
			descriptor =>
			{
				Assert.Equal(QuestFinishRewardItemSource.RegularFixed, descriptor.RewardItemProjection?.Source);
				Assert.Equal(182400002, descriptor.ItemId);
				Assert.Equal(1, descriptor.Count);
				Assert.Equal(0, descriptor.RewardItemProjection?.RewardGroupIndex);
				Assert.Null(descriptor.RewardItemProjection?.SelectableIndex);
			},
			descriptor =>
			{
				Assert.Equal(QuestFinishRewardItemSource.RegularSelectable, descriptor.RewardItemProjection?.Source);
				Assert.Equal(182400010, descriptor.ItemId);
				Assert.Equal(3, descriptor.Count);
				Assert.Equal(0, descriptor.RewardItemProjection?.RewardGroupIndex);
				Assert.Equal(0, descriptor.RewardItemProjection?.SelectableIndex);
			});
		Assert.DoesNotContain(operationPlan.Descriptors, descriptor => descriptor.Action == QuestFinishOperationAction.ItemRewardProjectionWarning);
		Assert.Contains(operationPlan.Descriptors, descriptor => descriptor.Action == QuestFinishOperationAction.RewardGroupCorrection);
		Assert.Contains(operationPlan.Descriptors, descriptor => descriptor.Action == QuestFinishOperationAction.ItemRewardPlaceholder);
		Assert.True(
			IndexOf(operationPlan.Descriptors, QuestFinishOperationAction.ItemRewardProjection)
			< IndexOf(operationPlan.Descriptors, QuestFinishOperationAction.ItemRewardPlaceholder));
		Assert.True(
			IndexOf(operationPlan.Descriptors, QuestFinishOperationAction.ItemRewardPlaceholder)
			< IndexOf(operationPlan.Descriptors, QuestFinishOperationAction.QuestStateMutation));
	}

	[Fact]
	public void StaticNonItemRewardProjection_ComposesThroughGuardAndOperationPlanWithoutLiveSideEffects()
	{
		const string xml = """
			<quests>
				<quest id="1001" can_report="true" category="QUEST" reward_repeat_count="2">
					<rewards gold="100" exp="400" ap="50" extend_stigma="3" ccheck="-1" />
				</quest>
			</quests>
			""";
		var template = new NearbyQuestTemplateXmlExtractor().Extract(xml).Single();
		var rewardProjection = new QuestFinishRewardTemplateXmlProjectionExtractor()
			.ExtractDefaultRegularNonItemProjections(xml)[1001] with
		{
			DialogActionId = 108
		};

		var guardPlan = QuestDialogAutoRewardGuardPlanService.CreatePlanFromTemplateSummary(
			new QuestDialogAutoRewardGuardTemplateInput(
				PlayerObjectId: 77,
				TargetObjectId: 0,
				DialogActionId: 108,
				QuestId: 1001,
				QuestTemplate: template));
		var operationPlan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(1001, "REWARD", QuestVars: 0x12, Flags: 0, CompleteCount: 0),
			template,
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 26, 8, 30, 0, TimeSpan.Zero),
			CreateOptions("UTC"),
			rewardProjection);

		Assert.True(guardPlan.Planned);
		Assert.False(guardPlan.IsLive);
		Assert.Equal(QuestDialogAutoRewardGuardStatus.Planned, guardPlan.Status);
		Assert.True(guardPlan.StaticMetadata?.HasRewards);
		Assert.Equal(1, rewardProjection.RewardGroupCount);
		Assert.True(rewardProjection.HasNonItemRewards);
		Assert.False(rewardProjection.HasItemRewards);
		Assert.True(operationPlan.Applied);
		Assert.Equal("COMPLETE", operationPlan.QuestState?.Status);
		Assert.All(operationPlan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.DoesNotContain(operationPlan.Descriptors, descriptor => descriptor.Action == QuestFinishOperationAction.RewardMutationPlaceholder);
		Assert.Contains(operationPlan.Descriptors, descriptor =>
			descriptor.Action == QuestFinishOperationAction.NonItemRewardProjection
			&& descriptor.RewardNonItemProjection?.Action == QuestFinishRewardNonItemAction.Kinah
			&& descriptor.Count == 100);
		Assert.Contains(operationPlan.Descriptors, descriptor =>
			descriptor.Action == QuestFinishOperationAction.NonItemRewardProjection
			&& descriptor.RewardNonItemProjection?.Action == QuestFinishRewardNonItemAction.Experience
			&& descriptor.Count == 400);
		Assert.Contains(operationPlan.Descriptors, descriptor =>
			descriptor.Action == QuestFinishOperationAction.NonItemRewardProjection
			&& descriptor.RewardNonItemProjection?.Action == QuestFinishRewardNonItemAction.AbyssPoints
			&& descriptor.Count == 50);
		Assert.Contains(operationPlan.Descriptors, descriptor =>
			descriptor.Action == QuestFinishOperationAction.NonItemRewardProjectionWarning
			&& descriptor.RewardNonItemProjectionWarning?.FieldName == "extend_stigma");
		Assert.Contains(operationPlan.Descriptors, descriptor =>
			descriptor.Action == QuestFinishOperationAction.NonItemRewardProjectionWarning
			&& descriptor.RewardNonItemProjectionWarning?.FieldName == "ccheck");
		Assert.Contains(operationPlan.Descriptors, descriptor => descriptor.Action == QuestFinishOperationAction.NonItemRewardPlaceholder);
		Assert.Contains(operationPlan.Descriptors, descriptor => descriptor.Action == QuestFinishOperationAction.QuestStateMutation);
	}

	private static int IndexOf(
		IReadOnlyList<QuestFinishOperationDescriptor> descriptors,
		QuestFinishOperationAction action)
	{
		for (var index = 0; index < descriptors.Count; index++)
		{
			if (descriptors[index].Action == action)
				return index;
		}

		return -1;
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
