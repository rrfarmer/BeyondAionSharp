using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class QuestXpExecutionPlanServiceTests
{
	[Fact]
	public void CreatePlan_StagesJavaLevelChangeSideEffectsBeforeStatAndXpPackets()
	{
		var service = CreateRewardService();
		var xpPlan = service.CreateXpRewardPlan(
			CreatePlayer(level: 9, exp: 8_900, reposeEnergy: 20),
			CreateLinearExperienceTable(),
			rewardXp: 400,
			npcName: "level quest",
			salvationPercent: 10);

		var plan = QuestXpExecutionPlanService.CreatePlan(xpPlan);

		Assert.Equal(QuestXpExecutionPlanStatus.Applied, plan.Status);
		Assert.True(plan.Applied);
		Assert.True(plan.WouldRunSetExpMutationBranch);
		Assert.True(plan.LevelChanged);
		Assert.Equal(10, plan.CurrentLevel);
		Assert.Equal(10, plan.MinNewLevel);
		Assert.Equal(
		[
			QuestXpExecutionAction.SetExp,
			QuestXpExecutionAction.RatioUpdate,
			QuestXpExecutionAction.StatsTemplateUpdate,
			QuestXpExecutionAction.MaxReposeUpdate,
			QuestXpExecutionAction.SalvationReset,
			QuestXpExecutionAction.UpgradePlayerLifeStats,
			QuestXpExecutionAction.VisualStatsUpdate,
			QuestXpExecutionAction.TeamStatUpdate,
			QuestXpExecutionAction.LegionMemberUpdate,
			QuestXpExecutionAction.LevelUpAnimationBroadcast,
			QuestXpExecutionAction.NpcFactionLevelUp,
			QuestXpExecutionAction.QuestLevelChangedCallbacks,
			QuestXpExecutionAction.NearbyQuestRefresh,
			QuestXpExecutionAction.GuideHtml,
			QuestXpExecutionAction.SkillAutoLearn,
			QuestXpExecutionAction.BonusPackReward,
			QuestXpExecutionAction.FactionPackReward,
			QuestXpExecutionAction.StarterKitReward,
			QuestXpExecutionAction.StatUpdateExpPacket,
			QuestXpExecutionAction.XpSystemMessagePacket,
		], plan.Descriptors.Select(descriptor => descriptor.Action));
		Assert.All(plan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.Equal([1400342], plan.XpSystemMessagePackets.Select(packet => packet.MessageId));
		Assert.Contains(
			plan.Descriptors,
			descriptor => descriptor.Action == QuestXpExecutionAction.LevelUpAnimationBroadcast
				&& descriptor.Notes!.Contains("LEVEL_UP id is 0", StringComparison.Ordinal));
	}

	[Fact]
	public void CreatePlan_KeepsNoLevelChangePlanInJavaPacketOrder()
	{
		var service = CreateRewardService();
		var xpPlan = service.CreateXpRewardPlan(
			CreatePlayer(level: 15, exp: 14_000, reposeEnergy: 50),
			CreateLinearExperienceTable(),
			rewardXp: 100,
			salvationPercent: 10);

		var plan = QuestXpExecutionPlanService.CreatePlan(xpPlan);

		Assert.False(plan.LevelChanged);
		Assert.Equal(15, plan.MinNewLevel);
		Assert.Equal(
		[
			QuestXpExecutionAction.SetExp,
			QuestXpExecutionAction.StatUpdateExpPacket,
			QuestXpExecutionAction.XpSystemMessagePacket,
		], plan.Descriptors.Select(descriptor => descriptor.Action));
		Assert.Equal([1400350], plan.XpSystemMessagePackets.Select(packet => packet.MessageId));
	}

	[Fact]
	public void CreatePlan_AppendsAscensionWarningAfterXpMessageAndSkipsGuardedPlans()
	{
		var service = CreateRewardService();
		var table = CreateLinearExperienceTable();
		var ascensionPlan = service.CreateXpRewardPlan(
			CreatePlayer(level: 9, exp: 8_900),
			table,
			rewardXp: 500,
			isDaeva: false);
		var skippedXp = service.CreateXpRewardPlan(
			CreatePlayer(level: 9, exp: 8_900),
			table,
			rewardXp: 0);

		var ascension = QuestXpExecutionPlanService.CreatePlan(ascensionPlan);
		var skipped = QuestXpExecutionPlanService.CreatePlan(skippedXp);

		Assert.Equal(
		[
			QuestXpExecutionAction.SetExp,
			QuestXpExecutionAction.StatUpdateExpPacket,
			QuestXpExecutionAction.XpSystemMessagePacket,
			QuestXpExecutionAction.AscensionLimitSystemMessage,
		], ascension.Descriptors.Select(descriptor => descriptor.Action));
		Assert.Equal([1370002, 1400545], ascension.XpSystemMessagePackets.Select(packet => packet.MessageId));
		Assert.Equal(QuestXpExecutionPlanStatus.Skipped, skipped.Status);
		Assert.Empty(skipped.Descriptors);
		Assert.Empty(skipped.XpSystemMessagePackets);
	}

	private static QuestRewardService CreateRewardService()
	{
		return new QuestRewardService(
			new WorldNpcResourceStatsService(
				new WorldNpcLifeStatsService(new WorldNpcDeathDropWorkflowService(null!, null!)),
				connectionRegistry: null,
				new PlayerVisualStatsUpdateService()),
			new GameServerOptions
			{
				Rates = new GameServerRateOptions
				{
					XpQuestRates = [1f],
				},
			});
	}

	private static Player CreatePlayer(int level, long exp, long reposeEnergy = 0)
	{
		return new Player
		{
			ObjectId = 4401,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = level,
			Exp = exp,
			ReposeEnergy = reposeEnergy,
			IsOnline = true,
			AccountMembership = 0,
			AbyssRank = PlayerAbyssRank.Default(),
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
			LifeStats = new PlayerLifeStats(100, 100, 100),
		};
	}

	private static PlayerExperienceTable CreateLinearExperienceTable()
	{
		return new PlayerExperienceTable(Enumerable.Range(0, 70).Select(level => (long)level * 1000).ToArray());
	}
}
