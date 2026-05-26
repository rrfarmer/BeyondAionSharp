using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestFinishStaticRewardProjectionCompositionTests
{
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
