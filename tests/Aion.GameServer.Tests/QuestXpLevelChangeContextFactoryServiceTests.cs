using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class QuestXpLevelChangeContextFactoryServiceTests
{
	[Fact]
	public void CreateContext_BuildsJavaLevelChangeSubPlansFromSnapshotInputs()
	{
		var player = CreatePlayer(level: 65);
		var input = new QuestXpLevelChangeContextFactoryInput(
			FromLevel: 65,
			ToLevel: 65,
			MaxStats: new PlayerLevelChangeUpgradeStats(250, 230, 120),
			QuestLevelChangedRegistrations: [new QuestLevelChangedRegistration(1001, "ELYOS")],
			WorldInstance: null,
			NearbyQuestTemplates: null,
			GuidesEnabled: true,
			IsSpawned: true,
			GuideHtmlTemplates: [new GuideHtmlTemplateSummary("level 65", 65, "RANGER", "ELYOS")],
			SkillTree: null,
			SkillTemplates: null,
			IsDaeva: true,
			HasEffectController: true,
			BonusPackReceivedPlayerId: 0,
			BonusPackStoreReceivingPlayerSucceeded: true,
			FactionPackAccountCreationLocalTime: new DateTime(2020, 9, 14, 0, 0, 0),
			FactionPackReceivedPlayerId: 0,
			FactionPackStoreReceivingPlayerSucceeded: true,
			ItemTemplates: new ItemTemplateTable(CreateFactionRewardTemplates()),
			StarterKitEnabled: true);

		var context = QuestXpLevelChangeContextFactoryService.CreateContext(player, input);
		var xpPlan = QuestXpRewardPlan.CreateApplied(
			player.ObjectId,
			rewardXp: 1,
			appliedBaseXp: 1,
			reposeUsed: 0,
			reposeBonus: 0,
			salvationBonus: 0,
			finalRewardXp: 1,
			previousExp: 64_999,
			currentExp: 65_000,
			previousLevel: 64,
			currentLevel: 65,
			previousReposeEnergy: 0,
			currentReposeEnergy: 0,
			maxReposeEnergy: 0,
			npcName: null,
			QuestXpRewardMessageKind.Plain,
			[QuestXpRewardPacketIntent.XpSystemMessage],
			RequiresAscensionLimitMessage: false);

		var executionPlan = QuestXpExecutionPlanService.CreatePlan(xpPlan, context);

		Assert.Equal(PlayerLevelChangeUpgradePlanStatus.Planned, context.UpgradePlayerPlan!.Status);
		Assert.Equal(QuestLevelChangedCallbackPlanStatus.Applied, context.QuestLevelChangedCallbackPlan!.Status);
		Assert.Equal(NearbyQuestRefreshPlanStatus.NoWorldInstance, context.NearbyQuestRefreshPlan!.Status);
		Assert.Equal(GuideHtmlLevelChangePlanStatus.Planned, context.GuideHtmlLevelChangePlan!.Status);
		Assert.Equal(SkillAutoLearnPlanStatus.BlockedMissingSkillTree, context.SkillAutoLearnPlan!.Status);
		Assert.Equal(CustomLevelRewardPlanStatus.Planned, context.BonusPackPlan!.Status);
		Assert.Equal(CustomLevelRewardPlanStatus.Planned, context.FactionPackPlan!.Status);
		Assert.Equal(StarterKitLevelChangePlanStatus.NoMatchingRewards, context.StarterKitLevelChangePlan!.Status);
		Assert.Equal(
		[
			QuestXpExecutionAction.UpgradePlayerLifeStats,
			QuestXpExecutionAction.NpcFactionLevelUp,
			QuestXpExecutionAction.QuestLevelChangedCallbacks,
			QuestXpExecutionAction.NearbyQuestRefresh,
			QuestXpExecutionAction.GuideHtml,
			QuestXpExecutionAction.SkillAutoLearn,
			QuestXpExecutionAction.BonusPackReward,
			QuestXpExecutionAction.FactionPackReward,
			QuestXpExecutionAction.StarterKitReward,
		], executionPlan.LevelChangeSubPlans.Select(subPlan => subPlan.Action));
		Assert.All(executionPlan.LevelChangeSubPlans, subPlan => Assert.False(subPlan.IsLive));
	}

	[Fact]
	public void CreateContext_RecordsGuardedSubPlansWhenDependenciesAreMissing()
	{
		var input = new QuestXpLevelChangeContextFactoryInput(FromLevel: 10, ToLevel: 10);

		var context = QuestXpLevelChangeContextFactoryService.CreateContext(player: null, input);

		Assert.Equal(PlayerLevelChangeUpgradePlanStatus.MissingPlayer, context.UpgradePlayerPlan!.Status);
		Assert.Equal(NpcFactionLevelUpPlanStatus.MissingSnapshot, context.NpcFactionLevelUpPlan!.Status);
		Assert.Equal(QuestLevelChangedCallbackPlanStatus.MissingRegistrations, context.QuestLevelChangedCallbackPlan!.Status);
		Assert.Equal(NearbyQuestRefreshPlanStatus.NoWorldInstance, context.NearbyQuestRefreshPlan!.Status);
		Assert.Equal(GuideHtmlLevelChangePlanStatus.MissingPlayer, context.GuideHtmlLevelChangePlan!.Status);
		Assert.Equal(SkillAutoLearnPlanStatus.MissingPlayer, context.SkillAutoLearnPlan!.Status);
		Assert.Equal(CustomLevelRewardPlanStatus.MissingPlayer, context.BonusPackPlan!.Status);
		Assert.Equal(CustomLevelRewardPlanStatus.MissingPlayer, context.FactionPackPlan!.Status);
		Assert.Equal(StarterKitLevelChangePlanStatus.MissingPlayer, context.StarterKitLevelChangePlan!.Status);
	}

	private static Player CreatePlayer(int level)
	{
		return new Player
		{
			ObjectId = 4701,
			AccountId = 3301,
			Name = "Contextual",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = level,
			LifeStats = new PlayerLifeStats(100, 100, 100),
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
		};
	}

	private static ItemTemplateSummary[] CreateFactionRewardTemplates()
	{
		return
		[
			CreateTemplate(186000236),
			CreateTemplate(162002030),
			CreateTemplate(162000023),
			CreateTemplate(166000195),
			CreateTemplate(169630007),
			CreateTemplate(188053526),
		];
	}

	private static ItemTemplateSummary CreateTemplate(int itemId)
	{
		return new ItemTemplateSummary(itemId, $"Item {itemId}", 0, 0, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 100, 0, 0);
	}
}
