using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class QuestXpLevelChangeContextFactoryService
{
	public static QuestXpLevelChangeCompositionContext CreateContext(
		Player? player,
		QuestXpLevelChangeContextFactoryInput input)
	{
		// Java parity breadcrumb: PlayerController.onLevelChange builds these side effects in this order after upgradePlayer.
		return new QuestXpLevelChangeCompositionContext(
			UpgradePlayerPlan: PlayerLevelChangeUpgradePlanService.CreatePlan(player, input.MaxStats),
			NpcFactionLevelUpPlan: NpcFactionLevelUpPlanService.CreatePlan(player?.NpcFactions, input.ToLevel, input.NpcFactionTable),
			QuestLevelChangedCallbackPlan: QuestLevelChangedCallbackPlanService.CreatePlan(
				player?.Race,
				input.QuestLevelChangedRegistrations,
				player?.Quests),
			NearbyQuestRefreshPlan: player == null
				? NearbyQuestRefreshPlan.NoWorldInstance()
				: NearbyQuestRefreshPlanService.CreatePlan(player, input.WorldInstance, input.NearbyQuestTemplates),
			GuideHtmlLevelChangePlan: GuideHtmlLevelChangePlanService.CreatePlan(
				player,
				input.GuidesEnabled,
				input.IsSpawned,
				input.FromLevel,
				input.ToLevel,
				input.GuideHtmlTemplates),
			SkillAutoLearnPlan: SkillLearnService.CreateAutoLearnPlan(
				player,
				input.SkillTree,
				input.SkillTemplates,
				input.FromLevel,
				input.ToLevel,
				input.IsDaeva,
				input.HasEffectController,
				input.IsSpawned),
			BonusPackPlan: CustomLevelRewardPlanService.CreateBonusPackPlan(
				player,
				input.BonusPackReceivedPlayerId,
				input.BonusPackStoreReceivingPlayerSucceeded),
			BonusPackExecutionResult: input.BonusPackExecutionResult,
			FactionPackPlan: CustomLevelRewardPlanService.CreateFactionPackPlan(
				player,
				input.FactionPackAccountCreationLocalTime,
				input.FactionPackReceivedPlayerId,
				input.FactionPackStoreReceivingPlayerSucceeded,
				input.ItemTemplates),
			FactionPackExecutionResult: input.FactionPackExecutionResult,
			StarterKitLevelChangePlan: StarterKitLevelChangePlanService.CreatePlan(
				player,
				input.StarterKitEnabled,
				input.FromLevel,
				input.ToLevel));
	}
}

public sealed record QuestXpLevelChangeContextFactoryInput(
	int FromLevel,
	int ToLevel,
	PlayerLevelChangeUpgradeStats? MaxStats = null,
	NpcFactionTable? NpcFactionTable = null,
	IEnumerable<QuestLevelChangedRegistration>? QuestLevelChangedRegistrations = null,
	WorldMapInstanceRuntimeState? WorldInstance = null,
	NearbyQuestTemplateTable? NearbyQuestTemplates = null,
	bool GuidesEnabled = false,
	bool IsSpawned = false,
	IEnumerable<GuideHtmlTemplateSummary>? GuideHtmlTemplates = null,
	SkillTreeTable? SkillTree = null,
	SkillTemplateTable? SkillTemplates = null,
	bool IsDaeva = true,
	bool HasEffectController = false,
	int BonusPackReceivedPlayerId = 0,
	bool BonusPackStoreReceivingPlayerSucceeded = false,
	CustomLevelRewardExecutionResult? BonusPackExecutionResult = null,
	DateTime FactionPackAccountCreationLocalTime = default,
	int FactionPackReceivedPlayerId = 0,
	bool FactionPackStoreReceivingPlayerSucceeded = false,
	CustomLevelRewardExecutionResult? FactionPackExecutionResult = null,
	ItemTemplateTable? ItemTemplates = null,
	bool StarterKitEnabled = false);
