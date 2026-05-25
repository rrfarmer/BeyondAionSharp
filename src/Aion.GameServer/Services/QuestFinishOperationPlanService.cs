using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum QuestFinishOperationAction
{
	RewardMutationPlaceholder,
	RewardGroupCorrection,
	ItemRewardProjection,
	ItemRewardProjectionWarning,
	ItemRewardPlaceholder,
	NonItemRewardProjection,
	NonItemRewardSideEffectPlan,
	NonItemRewardProjectionWarning,
	NonItemRewardPlaceholder,
	ChallengeTaskCompletionPlaceholder,
	RemoveQuestWorkItemsPlaceholder,
	QuestStateMutation,
	QuestUpdatePacket,
	QuestCompletedCallback,
	NpcFactionCompletion,
	NearbyQuestRefresh,
	DeferredQuestPersistence,
	DeferredNpcFactionPersistence,
}

public sealed record QuestFinishOperationDescriptor(
	int Order,
	QuestFinishOperationAction Action,
	string JavaSource,
	bool IsLive,
	int? ItemId = null,
	long? Count = null,
	QuestFinishRewardItemProjectionDescriptor? RewardItemProjection = null,
	QuestFinishRewardItemProjectionWarningDescriptor? RewardItemProjectionWarning = null,
	QuestFinishRewardNonItemProjectionDescriptor? RewardNonItemProjection = null,
	QuestXpRewardPlan? XpRewardPlan = null,
	QuestTitleRewardPlan? TitleRewardPlan = null,
	QuestExpansionRewardPlan? ExpansionRewardPlan = null,
	QuestGpRewardResult? GpRewardPlan = null,
	QuestFinishRewardNonItemProjectionWarningDescriptor? RewardNonItemProjectionWarning = null,
	QuestCompletionCallbackDescriptor? CompletionCallbackOperation = null,
	QuestPersistenceOperationDescriptor? QuestPersistenceOperation = null,
	NpcFactionPersistenceOperationDescriptor? NpcFactionPersistenceOperation = null);

public sealed record QuestFinishRewardSideEffectContext(
	Player? Player,
	TitleTemplateTable? TitleTemplates = null,
	int? CubeExpansionLimit = null,
	PlayerExperienceTable? ExperienceTable = null,
	string? TargetNpcName = null,
	bool NoExp = false,
	int QuestXpBoostStat = 100,
	bool HasLegionBonus = false,
	byte SalvationPercent = 0,
	bool IsDaeva = true);

public sealed record QuestFinishOperationPlan(
	QuestFinishStateMutationStatus Status,
	PlayerQuestState? QuestState,
	PlayerNpcFactionsSnapshot NpcFactions,
	IReadOnlyList<QuestFinishOperationDescriptor> Descriptors)
{
	public bool Applied => Status == QuestFinishStateMutationStatus.Applied;
}

public static class QuestFinishOperationPlanService
{
	public static QuestFinishOperationPlan CreatePlan(
		PlayerQuestState? questState,
		NearbyQuestTemplateSummary template,
		PlayerNpcFactionsSnapshot npcFactions,
		DateTimeOffset now,
		GameServerOptions options,
		QuestFinishRewardTemplateProjection? rewardProjection = null,
		QuestCompletionCallbackPlan? callbackPlan = null,
		QuestPersistencePlan? questPersistencePlan = null,
		NpcFactionPersistencePlan? npcFactionPersistencePlan = null,
		QuestFinishRewardSideEffectContext? rewardSideEffectContext = null)
	{
		var guard = QuestFinishStateMutationService.ApplyRewardCompletion(questState, template, now, options);
		if (!guard.Applied)
		{
			return new QuestFinishOperationPlan(
				guard.Status,
				guard.QuestState,
				npcFactions,
				Array.Empty<QuestFinishOperationDescriptor>());
		}

		var descriptors = new List<QuestFinishOperationDescriptor>();
		var nextOrder = 1;
		var stateInput = questState!;

		if (rewardProjection is null)
		{
			// Java parity breadcrumb: QuestService.finishQuest computes/adds rewards and removes
			// work items before mutating QuestState. These are deliberately descriptors only.
			descriptors.Add(new(nextOrder++, QuestFinishOperationAction.RewardMutationPlaceholder, "QuestService.finishQuest rewards", IsLive: false));
			descriptors.Add(new(nextOrder++, QuestFinishOperationAction.RemoveQuestWorkItemsPlaceholder, "QuestService.removeQuestWorkItems", IsLive: false));
		}
		else
		{
			var rewardPlan = QuestFinishRewardPlanService.CreatePlan(stateInput, rewardProjection);
			stateInput = rewardPlan.QuestState;
			foreach (var rewardDescriptor in rewardPlan.Descriptors)
			{
				if (rewardDescriptor.Action == QuestFinishRewardOperationAction.ItemRewardPlaceholder)
				{
					AddDetailedRewardItemProjectionDescriptors(
						descriptors,
						ref nextOrder,
						stateInput,
						rewardProjection);
				}
				else if (rewardDescriptor.Action == QuestFinishRewardOperationAction.NonItemRewardPlaceholder)
				{
					AddDetailedRewardNonItemProjectionDescriptors(
						descriptors,
						ref nextOrder,
						template,
						rewardProjection,
						rewardSideEffectContext,
						options);
				}

				descriptors.Add(new QuestFinishOperationDescriptor(
					nextOrder++,
					MapRewardAction(rewardDescriptor.Action),
					rewardDescriptor.JavaSource,
					rewardDescriptor.IsLive,
					rewardDescriptor.ItemId,
					rewardDescriptor.Count));
			}
		}

		var mutation = QuestFinishStateMutationService.ApplyRewardCompletion(stateInput, template, now, options);
		descriptors.Add(new(nextOrder++, QuestFinishOperationAction.QuestStateMutation, "QuestState.setStatus/setQuestVar/setNextRepeatTime", IsLive: false));
		descriptors.Add(new(nextOrder++, QuestFinishOperationAction.QuestUpdatePacket, "SM_QUEST_ACTION(ActionType.UPDATE, qs)", IsLive: false));
		if (callbackPlan is null)
		{
			descriptors.Add(new(nextOrder++, QuestFinishOperationAction.QuestCompletedCallback, "QuestEngine.onQuestCompleted", IsLive: false));
		}
		else
		{
			foreach (var callbackDescriptor in callbackPlan.Descriptors)
			{
				descriptors.Add(new QuestFinishOperationDescriptor(
					nextOrder++,
					QuestFinishOperationAction.QuestCompletedCallback,
					callbackDescriptor.HandlerJavaSource,
					callbackDescriptor.IsLive,
					CompletionCallbackOperation: callbackDescriptor));
			}
		}

		var plannedNpcFactions = npcFactions;
		if (template.NpcFactionId != 0)
		{
			var nextReset = NpcFactionDailyResetService.GetNextResetEpochSeconds(now, options);
			var factionCompletion = npcFactions.CompleteActiveQuest(template.IsMentorQuest, nextReset);
			plannedNpcFactions = factionCompletion.Snapshot;
			descriptors.Add(new QuestFinishOperationDescriptor(
				nextOrder++,
				QuestFinishOperationAction.NpcFactionCompletion,
				"NpcFactions.completeQuest",
				IsLive: false));
		}

		descriptors.Add(new QuestFinishOperationDescriptor(
			nextOrder++,
			QuestFinishOperationAction.NearbyQuestRefresh,
			"PlayerController.updateNearbyQuests",
			IsLive: false));
		if (questPersistencePlan is null)
		{
			descriptors.Add(new QuestFinishOperationDescriptor(
				nextOrder++,
				QuestFinishOperationAction.DeferredQuestPersistence,
				"PlayerService.storePlayer -> PlayerQuestListDAO.store",
				IsLive: false));
		}
		else
		{
			foreach (var questPersistenceDescriptor in questPersistencePlan.Descriptors)
			{
				descriptors.Add(new QuestFinishOperationDescriptor(
					nextOrder++,
					QuestFinishOperationAction.DeferredQuestPersistence,
					questPersistenceDescriptor.JavaSource,
					questPersistenceDescriptor.IsLive,
					QuestPersistenceOperation: questPersistenceDescriptor));
			}
		}

		if (npcFactionPersistencePlan is null)
		{
			if (template.NpcFactionId != 0)
			{
				descriptors.Add(new QuestFinishOperationDescriptor(
					nextOrder,
					QuestFinishOperationAction.DeferredNpcFactionPersistence,
					"PlayerService.storePlayer -> PlayerNpcFactionsDAO.storeNpcFactions",
					IsLive: false));
			}
		}
		else
		{
			foreach (var npcFactionPersistenceDescriptor in npcFactionPersistencePlan.Descriptors)
			{
				descriptors.Add(new QuestFinishOperationDescriptor(
					nextOrder++,
					QuestFinishOperationAction.DeferredNpcFactionPersistence,
					npcFactionPersistenceDescriptor.JavaSource,
					npcFactionPersistenceDescriptor.IsLive,
					NpcFactionPersistenceOperation: npcFactionPersistenceDescriptor));
			}
		}

		return new QuestFinishOperationPlan(
			mutation.Status,
			mutation.QuestState,
			plannedNpcFactions,
			descriptors);
	}

	private static QuestFinishOperationAction MapRewardAction(QuestFinishRewardOperationAction action)
	{
		return action switch
		{
			QuestFinishRewardOperationAction.RewardGroupCorrection => QuestFinishOperationAction.RewardGroupCorrection,
			QuestFinishRewardOperationAction.ItemRewardPlaceholder => QuestFinishOperationAction.ItemRewardPlaceholder,
			QuestFinishRewardOperationAction.NonItemRewardPlaceholder => QuestFinishOperationAction.NonItemRewardPlaceholder,
			QuestFinishRewardOperationAction.ChallengeTaskCompletionPlaceholder => QuestFinishOperationAction.ChallengeTaskCompletionPlaceholder,
			QuestFinishRewardOperationAction.RemoveQuestWorkItemsPlaceholder => QuestFinishOperationAction.RemoveQuestWorkItemsPlaceholder,
			_ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
		};
	}

	private static void AddDetailedRewardItemProjectionDescriptors(
		ICollection<QuestFinishOperationDescriptor> descriptors,
		ref int nextOrder,
		PlayerQuestState questState,
		QuestFinishRewardTemplateProjection rewardProjection)
	{
		if (rewardProjection.ItemProjection is null)
		{
			return;
		}

		var projectionPlan = QuestFinishRewardPlanService.CreateRewardItemProjection(
			new QuestFinishRewardItemProjectionInput(
				questState.QuestId,
				rewardProjection.DialogActionId,
				rewardProjection.ExtendedRewardIndex,
				questState.CompleteCount,
				rewardProjection.RewardRepeatCount,
				questState.RewardGroup,
				rewardProjection.PlayerClass),
			rewardProjection.ItemProjection);

		foreach (var itemDescriptor in projectionPlan.Items)
		{
			descriptors.Add(new QuestFinishOperationDescriptor(
				nextOrder++,
				QuestFinishOperationAction.ItemRewardProjection,
				itemDescriptor.JavaSource,
				itemDescriptor.IsLive,
				itemDescriptor.ItemId,
				itemDescriptor.Count,
				RewardItemProjection: itemDescriptor));
		}

		foreach (var warning in projectionPlan.Warnings)
		{
			descriptors.Add(new QuestFinishOperationDescriptor(
				nextOrder++,
				QuestFinishOperationAction.ItemRewardProjectionWarning,
				warning.JavaSource,
				IsLive: false,
				RewardItemProjectionWarning: warning));
		}
	}

	private static void AddDetailedRewardNonItemProjectionDescriptors(
		ICollection<QuestFinishOperationDescriptor> descriptors,
		ref int nextOrder,
		NearbyQuestTemplateSummary template,
		QuestFinishRewardTemplateProjection rewardProjection,
		QuestFinishRewardSideEffectContext? rewardSideEffectContext,
		GameServerOptions options)
	{
		if (rewardProjection.NonItemProjection is null)
		{
			return;
		}

		var projectionPlan = QuestFinishRewardPlanService.CreateNonItemRewardProjection(
			new QuestFinishRewardNonItemProjectionInput(
				template.QuestId,
				template.QuestCategory,
				rewardProjection.TargetNpcId,
				rewardProjection.HasTargetNpcTemplate),
			rewardProjection.NonItemProjection);

		foreach (var nonItemDescriptor in projectionPlan.Descriptors)
		{
			descriptors.Add(new QuestFinishOperationDescriptor(
				nextOrder++,
				QuestFinishOperationAction.NonItemRewardProjection,
				nonItemDescriptor.JavaSource,
				nonItemDescriptor.IsLive,
				Count: nonItemDescriptor.Amount,
				RewardNonItemProjection: nonItemDescriptor));
			AddNonItemRewardSideEffectPlanDescriptor(
				descriptors,
				ref nextOrder,
				nonItemDescriptor,
				rewardSideEffectContext,
				options);
		}

		foreach (var warning in projectionPlan.Warnings)
		{
			descriptors.Add(new QuestFinishOperationDescriptor(
				nextOrder++,
				QuestFinishOperationAction.NonItemRewardProjectionWarning,
				warning.JavaSource,
				IsLive: false,
				RewardNonItemProjectionWarning: warning));
		}
	}

	private static void AddNonItemRewardSideEffectPlanDescriptor(
		ICollection<QuestFinishOperationDescriptor> descriptors,
		ref int nextOrder,
		QuestFinishRewardNonItemProjectionDescriptor nonItemDescriptor,
		QuestFinishRewardSideEffectContext? rewardSideEffectContext,
		GameServerOptions options)
	{
		if (rewardSideEffectContext is null)
		{
			return;
		}

		switch (nonItemDescriptor.Action)
		{
			case QuestFinishRewardNonItemAction.Experience:
				if (rewardSideEffectContext.ExperienceTable is null)
				{
					return;
				}

				var xpPlan = QuestRewardService.CreateXpRewardPlanFromRates(
					rewardSideEffectContext.Player,
					rewardSideEffectContext.ExperienceTable,
					nonItemDescriptor.Amount,
					options.Rates.XpQuestRates,
					rewardSideEffectContext.TargetNpcName,
					rewardSideEffectContext.NoExp,
					rewardSideEffectContext.QuestXpBoostStat,
					rewardSideEffectContext.HasLegionBonus,
					rewardSideEffectContext.SalvationPercent,
					rewardSideEffectContext.IsDaeva);
				descriptors.Add(new QuestFinishOperationDescriptor(
					nextOrder++,
					QuestFinishOperationAction.NonItemRewardSideEffectPlan,
					xpPlan.JavaSource,
					IsLive: false,
					Count: nonItemDescriptor.Amount,
					RewardNonItemProjection: nonItemDescriptor,
					XpRewardPlan: xpPlan));
				break;
			case QuestFinishRewardNonItemAction.Title:
				if (rewardSideEffectContext.TitleTemplates is null)
				{
					return;
				}

				var titlePlan = QuestRewardSideEffectPlanService.CreateTitleRewardPlan(
					rewardSideEffectContext.Player,
					checked((int)nonItemDescriptor.Amount),
					rewardSideEffectContext.TitleTemplates);
				descriptors.Add(new QuestFinishOperationDescriptor(
					nextOrder++,
					QuestFinishOperationAction.NonItemRewardSideEffectPlan,
					titlePlan.JavaSource,
					IsLive: false,
					Count: nonItemDescriptor.Amount,
					RewardNonItemProjection: nonItemDescriptor,
					TitleRewardPlan: titlePlan));
				break;
			case QuestFinishRewardNonItemAction.CubeExpansion:
				var cubePlan = QuestRewardSideEffectPlanService.CreateCubeExpansionPlan(
					rewardSideEffectContext.Player,
					rewardSideEffectContext.CubeExpansionLimit ?? options.Custom.CubeExpansionLimit);
				descriptors.Add(new QuestFinishOperationDescriptor(
					nextOrder++,
					QuestFinishOperationAction.NonItemRewardSideEffectPlan,
					cubePlan.JavaSource,
					IsLive: false,
					Count: nonItemDescriptor.Amount,
					RewardNonItemProjection: nonItemDescriptor,
					ExpansionRewardPlan: cubePlan));
				break;
			case QuestFinishRewardNonItemAction.WarehouseExpansion:
				var warehousePlan = QuestRewardSideEffectPlanService.CreateWarehouseExpansionPlan(
					rewardSideEffectContext.Player);
				descriptors.Add(new QuestFinishOperationDescriptor(
					nextOrder++,
					QuestFinishOperationAction.NonItemRewardSideEffectPlan,
					warehousePlan.JavaSource,
					IsLive: false,
					Count: nonItemDescriptor.Amount,
					RewardNonItemProjection: nonItemDescriptor,
					ExpansionRewardPlan: warehousePlan));
				break;
			case QuestFinishRewardNonItemAction.GloryPoints:
				var gpPlan = QuestRewardSideEffectPlanService.CreateGpRewardPlan(
					rewardSideEffectContext.Player,
					checked((int)nonItemDescriptor.Amount),
					options.Rates.GpRates);
				descriptors.Add(new QuestFinishOperationDescriptor(
					nextOrder++,
					QuestFinishOperationAction.NonItemRewardSideEffectPlan,
					gpPlan.GloryPointsPlan?.JavaSource ?? "QuestService.giveReward -> GloryPointsService.addGp",
					IsLive: false,
					Count: nonItemDescriptor.Amount,
					RewardNonItemProjection: nonItemDescriptor,
					GpRewardPlan: gpPlan));
				break;
		}
	}
}
