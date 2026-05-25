using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public static class QuestXpExecutionPlanService
{
	public static QuestXpExecutionPlan CreatePlan(QuestXpRewardPlan xpRewardPlan)
	{
		return CreatePlan(xpRewardPlan, levelChangeContext: null);
	}

	public static QuestXpExecutionPlan CreatePlan(
		QuestXpRewardPlan xpRewardPlan,
		QuestXpLevelChangeCompositionContext? levelChangeContext)
	{
		ArgumentNullException.ThrowIfNull(xpRewardPlan);
		if (!xpRewardPlan.Applied)
			return QuestXpExecutionPlan.Skipped(xpRewardPlan);

		var descriptors = new List<QuestXpExecutionDescriptor>();
		var levelChangeSubPlans = Array.Empty<QuestXpLevelChangeSubPlanDescriptor>();
		var wouldRunSetExp = xpRewardPlan.CurrentExp != xpRewardPlan.PreviousExp
			|| xpRewardPlan.PreviousLevel == 0 && xpRewardPlan.CurrentExp == 0;

		descriptors.Add(new QuestXpExecutionDescriptor(
			QuestXpExecutionAction.SetExp,
			"PlayerCommonData.setExp",
			Notes: wouldRunSetExp
				? "Would mutate exp/level through Java setExp boundary."
				: "Java setExp would be called by addExp but would not enter its mutation/send branch because exp did not change."));

		if (xpRewardPlan.CurrentLevel != xpRewardPlan.PreviousLevel)
		{
			AddLevelChangeDescriptors(descriptors);
			levelChangeSubPlans = ComposeLevelChangeSubPlans(levelChangeContext);
		}

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
			levelChangeSubPlans,
			systemMessagePackets,
			xpRewardPlan);
	}

	private static QuestXpLevelChangeSubPlanDescriptor[] ComposeLevelChangeSubPlans(
		QuestXpLevelChangeCompositionContext? context)
	{
		if (context == null)
			return [];

		var subPlans = new List<QuestXpLevelChangeSubPlanDescriptor>();
		if (context.UpgradePlayerPlan != null)
		{
			subPlans.Add(new QuestXpLevelChangeSubPlanDescriptor(
				QuestXpExecutionAction.UpgradePlayerLifeStats,
				nameof(PlayerLevelChangeUpgradePlanService),
				context.UpgradePlayerPlan.Status.ToString(),
				context.UpgradePlayerPlan.Applied,
				context.UpgradePlayerPlan.Descriptors.Count,
				context.UpgradePlayerPlan.Descriptors.Count(descriptor => descriptor.Status == PlayerLevelChangeUpgradeDescriptorStatus.Planned),
				"PlayerController.upgradePlayer"));
		}

		if (context.NpcFactionLevelUpPlan != null)
		{
			subPlans.Add(new QuestXpLevelChangeSubPlanDescriptor(
				QuestXpExecutionAction.NpcFactionLevelUp,
				nameof(NpcFactionLevelUpPlanService),
				context.NpcFactionLevelUpPlan.Status.ToString(),
				context.NpcFactionLevelUpPlan.Applied,
				context.NpcFactionLevelUpPlan.Descriptors.Count,
				context.NpcFactionLevelUpPlan.Descriptors.Count(descriptor => descriptor.Status == NpcFactionLevelUpDescriptorStatus.PlannedLeaveByLevelLimit),
				"PlayerController.onLevelChange -> NpcFactions.onLevelUp"));
		}

		if (context.QuestLevelChangedCallbackPlan != null)
		{
			subPlans.Add(new QuestXpLevelChangeSubPlanDescriptor(
				QuestXpExecutionAction.QuestLevelChangedCallbacks,
				nameof(QuestLevelChangedCallbackPlanService),
				context.QuestLevelChangedCallbackPlan.Status.ToString(),
				context.QuestLevelChangedCallbackPlan.Applied,
				context.QuestLevelChangedCallbackPlan.Descriptors.Count,
				context.QuestLevelChangedCallbackPlan.Descriptors.Count(descriptor => descriptor.Status == QuestLevelChangedCallbackDescriptorStatus.PlannedDispatch),
				"PlayerController.onLevelChange -> QuestEngine.onLevelChanged"));
		}

		if (context.NearbyQuestRefreshPlan != null)
		{
			subPlans.Add(new QuestXpLevelChangeSubPlanDescriptor(
				QuestXpExecutionAction.NearbyQuestRefresh,
				nameof(NearbyQuestRefreshPlanService),
				context.NearbyQuestRefreshPlan.Status.ToString(),
				context.NearbyQuestRefreshPlan.WouldSendPacket,
				context.NearbyQuestRefreshPlan.Markers.Count,
				context.NearbyQuestRefreshPlan.Markers.Count,
				"PlayerController.onLevelChange -> updateNearbyQuests"));
		}

		if (context.GuideHtmlLevelChangePlan != null)
		{
			subPlans.Add(new QuestXpLevelChangeSubPlanDescriptor(
				QuestXpExecutionAction.GuideHtml,
				nameof(GuideHtmlLevelChangePlanService),
				context.GuideHtmlLevelChangePlan.Status.ToString(),
				context.GuideHtmlLevelChangePlan.Applied,
				context.GuideHtmlLevelChangePlan.Descriptors.Count,
				context.GuideHtmlLevelChangePlan.PlannedGuideCount,
				"PlayerController.onLevelChange -> HTMLService.sendGuideHtml"));
		}

		if (context.SkillAutoLearnPlan != null)
		{
			subPlans.Add(new QuestXpLevelChangeSubPlanDescriptor(
				QuestXpExecutionAction.SkillAutoLearn,
				"SkillLearnService.CreateAutoLearnPlan",
				context.SkillAutoLearnPlan.Status.ToString(),
				context.SkillAutoLearnPlan.Applied,
				context.SkillAutoLearnPlan.Descriptors.Count,
				context.SkillAutoLearnPlan.Descriptors.Count(descriptor =>
					descriptor.Status is SkillAutoLearnDescriptorStatus.PlannedAdd
						or SkillAutoLearnDescriptorStatus.PlannedUpgrade
						or SkillAutoLearnDescriptorStatus.PlannedRemove),
				"PlayerController.onLevelChange -> SkillLearnService.learnNewSkills"));
		}

		if (context.BonusPackExecutionResult != null)
			subPlans.Add(CreateCustomRewardExecutionSubPlan(context.BonusPackExecutionResult, QuestXpExecutionAction.BonusPackReward));
		else if (context.BonusPackPlan != null)
			subPlans.Add(CreateCustomRewardSubPlan(context.BonusPackPlan, QuestXpExecutionAction.BonusPackReward));
		if (context.FactionPackExecutionResult != null)
			subPlans.Add(CreateCustomRewardExecutionSubPlan(context.FactionPackExecutionResult, QuestXpExecutionAction.FactionPackReward));
		else if (context.FactionPackPlan != null)
			subPlans.Add(CreateCustomRewardSubPlan(context.FactionPackPlan, QuestXpExecutionAction.FactionPackReward));

		if (context.StarterKitLevelChangePlan != null)
		{
			subPlans.Add(new QuestXpLevelChangeSubPlanDescriptor(
				QuestXpExecutionAction.StarterKitReward,
				nameof(StarterKitLevelChangePlanService),
				context.StarterKitLevelChangePlan.Status.ToString(),
				context.StarterKitLevelChangePlan.Applied,
				context.StarterKitLevelChangePlan.Descriptors.Count,
				context.StarterKitLevelChangePlan.Descriptors.Count(descriptor => descriptor.Status == StarterKitLevelChangeDescriptorStatus.PlannedSystemMail),
				"PlayerController.onLevelChange -> StarterKitService.onLevelUp"));
		}

		return subPlans.ToArray();
	}

	private static QuestXpLevelChangeSubPlanDescriptor CreateCustomRewardSubPlan(
		CustomLevelRewardPlan plan,
		QuestXpExecutionAction action)
	{
		return new QuestXpLevelChangeSubPlanDescriptor(
			action,
			nameof(CustomLevelRewardPlanService),
			plan.Status.ToString(),
			plan.Applied,
			plan.Descriptors.Count,
			plan.Descriptors.Count(descriptor => descriptor.Status == CustomLevelRewardDescriptorStatus.PlannedSystemMail),
			action == QuestXpExecutionAction.BonusPackReward
				? "PlayerController.onLevelChange -> BonusPackService.addPlayerCustomReward"
				: "PlayerController.onLevelChange -> FactionPackService.addPlayerCustomReward");
	}

	private static QuestXpLevelChangeSubPlanDescriptor CreateCustomRewardExecutionSubPlan(
		CustomLevelRewardExecutionResult result,
		QuestXpExecutionAction action)
	{
		return new QuestXpLevelChangeSubPlanDescriptor(
			action,
			nameof(CustomLevelRewardExecutionService),
			result.Status.ToString(),
			result.Applied,
			result.RewardPlan.Descriptors.Count,
			result.MailPlans.Count(mailPlan => mailPlan.Status == SystemMailRewardPlanStatus.Planned),
			action == QuestXpExecutionAction.BonusPackReward
				? "PlayerController.onLevelChange -> BonusPackService.addPlayerCustomReward"
				: "PlayerController.onLevelChange -> FactionPackService.addPlayerCustomReward",
			IsLive: result.IsLiveReceiptBoundary || result.IsLiveMailBoundary);
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
	IReadOnlyList<QuestXpLevelChangeSubPlanDescriptor> LevelChangeSubPlans,
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
			Array.Empty<QuestXpLevelChangeSubPlanDescriptor>(),
			Array.Empty<SmSystemMessage>(),
			xpRewardPlan);
	}
}

public sealed record QuestXpExecutionDescriptor(
	QuestXpExecutionAction Action,
	string JavaSource,
	bool IsLive = false,
	string? Notes = null);

public sealed record QuestXpLevelChangeCompositionContext(
	PlayerLevelChangeUpgradePlan? UpgradePlayerPlan = null,
	NpcFactionLevelUpPlan? NpcFactionLevelUpPlan = null,
	QuestLevelChangedCallbackPlan? QuestLevelChangedCallbackPlan = null,
	NearbyQuestRefreshPlan? NearbyQuestRefreshPlan = null,
	GuideHtmlLevelChangePlan? GuideHtmlLevelChangePlan = null,
	SkillAutoLearnPlan? SkillAutoLearnPlan = null,
	CustomLevelRewardPlan? BonusPackPlan = null,
	CustomLevelRewardExecutionResult? BonusPackExecutionResult = null,
	CustomLevelRewardPlan? FactionPackPlan = null,
	CustomLevelRewardExecutionResult? FactionPackExecutionResult = null,
	StarterKitLevelChangePlan? StarterKitLevelChangePlan = null);

public sealed record QuestXpLevelChangeSubPlanDescriptor(
	QuestXpExecutionAction Action,
	string CSharpPlan,
	string PlanStatus,
	bool Applied,
	int DescriptorCount,
	int PlannedDescriptorCount,
	string JavaSource,
	bool IsLive = false);

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
