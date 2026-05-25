using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public static class QuestXpExecutionPlanService
{
	public static QuestXpExecutionPlan CreatePlan(QuestXpRewardPlan xpRewardPlan)
	{
		ArgumentNullException.ThrowIfNull(xpRewardPlan);
		if (!xpRewardPlan.Applied)
			return QuestXpExecutionPlan.Skipped(xpRewardPlan);

		var descriptors = new List<QuestXpExecutionDescriptor>();
		var wouldRunSetExp = xpRewardPlan.CurrentExp != xpRewardPlan.PreviousExp
			|| xpRewardPlan.PreviousLevel == 0 && xpRewardPlan.CurrentExp == 0;

		descriptors.Add(new QuestXpExecutionDescriptor(
			QuestXpExecutionAction.SetExp,
			"PlayerCommonData.setExp",
			Notes: wouldRunSetExp
				? "Would mutate exp/level through Java setExp boundary."
				: "Java setExp would be called by addExp but would not enter its mutation/send branch because exp did not change."));

		if (xpRewardPlan.CurrentLevel != xpRewardPlan.PreviousLevel)
			AddLevelChangeDescriptors(descriptors);

		if (wouldRunSetExp)
		{
			descriptors.Add(new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.StatUpdateExpPacket,
				"PlayerCommonData.setExp -> SM_STATUPDATE_EXP",
				Notes: "Packet metadata only; future live execution must create it after level-change side effects and before XP system messages."));
		}

		var systemMessagePackets = QuestRewardService.CreateXpSystemMessagePackets(xpRewardPlan);
		if (systemMessagePackets.Count > 0)
		{
			descriptors.Add(new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.XpSystemMessagePacket,
				"PlayerCommonData.addExp -> SM_SYSTEM_MESSAGE XP reward",
				Notes: "Packet objects are created but not sent live."));
		}

		if (xpRewardPlan.RequiresAscensionLimitMessage)
		{
			descriptors.Add(new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.AscensionLimitSystemMessage,
				"PlayerCommonData.addExp -> STR_LEVEL_LIMIT_QUEST_NOT_FINISHED1",
				Notes: "Warning follows the XP gain message in Java order."));
		}

		var minNewLevel = xpRewardPlan.PreviousLevel < xpRewardPlan.CurrentLevel
			? xpRewardPlan.PreviousLevel + 1
			: xpRewardPlan.PreviousLevel > xpRewardPlan.CurrentLevel
				? xpRewardPlan.PreviousLevel - 1
				: xpRewardPlan.CurrentLevel;

		return new QuestXpExecutionPlan(
			QuestXpExecutionPlanStatus.Applied,
			xpRewardPlan.ObjectId,
			xpRewardPlan.PreviousExp,
			xpRewardPlan.CurrentExp,
			xpRewardPlan.PreviousLevel,
			xpRewardPlan.CurrentLevel,
			minNewLevel,
			wouldRunSetExp,
			xpRewardPlan.CurrentLevel != xpRewardPlan.PreviousLevel,
			descriptors,
			systemMessagePackets,
			xpRewardPlan);
	}

	private static void AddLevelChangeDescriptors(List<QuestXpExecutionDescriptor> descriptors)
	{
		// Java parity breadcrumb: PlayerController.onLevelChange order.
		descriptors.AddRange(
		[
			new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.RatioUpdate,
				"PlayerController.onLevelChange -> GameServer.updateRatio",
				Notes: "Conditional on GSConfig ratio limitation, account race count, and threshold crossing."),
			new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.StatsTemplateUpdate,
				"PlayerController.onLevelChange -> PlayerGameStats.updateStatsTemplate"),
			new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.MaxReposeUpdate,
				"PlayerController.onLevelChange -> PlayerCommonData.updateMaxRepose"),
			new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.SalvationReset,
				"PlayerController.onLevelChange -> PlayerCommonData.resetSalvationPoints"),
			new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.UpgradePlayerLifeStats,
				"PlayerController.upgradePlayer -> PlayerLifeStats.synchronizeWithMaxStats"),
			new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.VisualStatsUpdate,
				"PlayerController.upgradePlayer -> PlayerGameStats.updateStatsVisually"),
			new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.TeamStatUpdate,
				"PlayerController.upgradePlayer -> TeamStatUpdater.add",
				Notes: "Conditional on team/alliance membership."),
			new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.LegionMemberUpdate,
				"PlayerController.upgradePlayer -> LegionService.updateMemberInfo",
				Notes: "Conditional on legion membership."),
			new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.LevelUpAnimationBroadcast,
				"PlayerController.onLevelChange -> SM_ACTION_ANIMATION(ActionAnimation.LEVEL_UP, newLevel)",
				Notes: "Java ActionAnimation.LEVEL_UP id is 0; C# uses SmActionAnimation.LevelUp for the future broadcast packet."),
			new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.NpcFactionLevelUp,
				"PlayerController.onLevelChange -> NpcFactions.onLevelUp"),
			new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.QuestLevelChangedCallbacks,
				"PlayerController.onLevelChange -> QuestEngine.onLevelChanged"),
			new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.NearbyQuestRefresh,
				"PlayerController.onLevelChange -> updateNearbyQuests"),
			new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.GuideHtml,
				"PlayerController.onLevelChange -> HTMLService.sendGuideHtml",
				Notes: "Conditional on guide config and spawned state."),
			new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.SkillAutoLearn,
				"PlayerController.onLevelChange -> SkillLearnService.learnNewSkills"),
			new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.BonusPackReward,
				"PlayerController.onLevelChange -> BonusPackService.addPlayerCustomReward"),
			new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.FactionPackReward,
				"PlayerController.onLevelChange -> FactionPackService.addPlayerCustomReward"),
			new QuestXpExecutionDescriptor(
				QuestXpExecutionAction.StarterKitReward,
				"PlayerController.onLevelChange -> StarterKitService.onLevelUp",
				Notes: "Conditional on CustomConfig.ENABLE_STARTER_KIT."),
		]);
	}
}

public sealed record QuestXpExecutionPlan(
	QuestXpExecutionPlanStatus Status,
	int ObjectId,
	long PreviousExp,
	long CurrentExp,
	int PreviousLevel,
	int CurrentLevel,
	int MinNewLevel,
	bool WouldRunSetExpMutationBranch,
	bool LevelChanged,
	IReadOnlyList<QuestXpExecutionDescriptor> Descriptors,
	IReadOnlyList<SmSystemMessage> XpSystemMessagePackets,
	QuestXpRewardPlan XpRewardPlan)
{
	public bool Applied => Status == QuestXpExecutionPlanStatus.Applied;

	public static QuestXpExecutionPlan Skipped(QuestXpRewardPlan xpRewardPlan)
	{
		return new QuestXpExecutionPlan(
			QuestXpExecutionPlanStatus.Skipped,
			xpRewardPlan.ObjectId,
			xpRewardPlan.PreviousExp,
			xpRewardPlan.CurrentExp,
			xpRewardPlan.PreviousLevel,
			xpRewardPlan.CurrentLevel,
			xpRewardPlan.CurrentLevel,
			WouldRunSetExpMutationBranch: false,
			LevelChanged: false,
			Array.Empty<QuestXpExecutionDescriptor>(),
			Array.Empty<SmSystemMessage>(),
			xpRewardPlan);
	}
}

public sealed record QuestXpExecutionDescriptor(
	QuestXpExecutionAction Action,
	string JavaSource,
	bool IsLive = false,
	string? Notes = null);

public enum QuestXpExecutionPlanStatus
{
	Applied,
	Skipped,
}

public enum QuestXpExecutionAction
{
	SetExp,
	RatioUpdate,
	StatsTemplateUpdate,
	MaxReposeUpdate,
	SalvationReset,
	UpgradePlayerLifeStats,
	VisualStatsUpdate,
	TeamStatUpdate,
	LegionMemberUpdate,
	LevelUpAnimationBroadcast,
	NpcFactionLevelUp,
	QuestLevelChangedCallbacks,
	NearbyQuestRefresh,
	GuideHtml,
	SkillAutoLearn,
	BonusPackReward,
	FactionPackReward,
	StarterKitReward,
	StatUpdateExpPacket,
	XpSystemMessagePacket,
	AscensionLimitSystemMessage,
}
