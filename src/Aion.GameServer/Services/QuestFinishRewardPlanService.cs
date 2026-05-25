using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum QuestFinishRewardGroupCorrectionStatus
{
	Unchanged,
	ClearedMissingRewards,
	ClampedOutOfRange,
	DefaultedFirstRewardGroup,
	IgnoredNonRewardState,
}

public enum QuestFinishRewardOperationAction
{
	RewardGroupCorrection,
	ItemRewardPlaceholder,
	NonItemRewardPlaceholder,
	ChallengeTaskCompletionPlaceholder,
	RemoveQuestWorkItemsPlaceholder,
}

public enum QuestFinishRewardItemSource
{
	RegularFixed,
	RegularSelectable,
	ClassSelectable,
	ExtendedFixed,
	ExtendedSelectable,
}

public enum QuestFinishRewardItemProjectionWarning
{
	RewardGroupOutOfRange,
	RegularSelectableOutOfRange,
	ClassSelectableOutOfRange,
	PlayerClassMissing,
	ExtendedRewardIndexMissing,
	ExtendedSelectableOutOfRange,
	BonusHandlerNotProjected,
}

public sealed record QuestFinishRewardWorkItem(int ItemId, long Count = 1);

public sealed record QuestFinishRewardItem(int ItemId, long Count = 1);

public sealed record QuestFinishRewardGroupProjection(
	int RewardGroupIndex,
	IReadOnlyList<QuestFinishRewardItem>? FixedRewardItems = null,
	IReadOnlyList<QuestFinishRewardItem>? SelectableRewardItems = null)
{
	public IReadOnlyList<QuestFinishRewardItem> FixedRewardItems { get; init; } = FixedRewardItems ?? [];
	public IReadOnlyList<QuestFinishRewardItem> SelectableRewardItems { get; init; } = SelectableRewardItems ?? [];
}

public sealed record QuestFinishRewardItemProjectionInput(
	int QuestId,
	int DialogActionId,
	int? ExtendedRewardIndex,
	int CompleteCount,
	int RewardRepeatCount,
	int? RewardGroup,
	string? PlayerClass = null);

public sealed record QuestFinishRewardItemTemplateProjection(
	IReadOnlyList<QuestFinishRewardGroupProjection>? RewardGroups = null,
	QuestFinishRewardGroupProjection? ExtendedRewards = null,
	IReadOnlyDictionary<string, IReadOnlyList<QuestFinishRewardItem>>? ClassSelectableRewards = null,
	bool SingleTimeClassReward = false,
	bool ClassRewardOnEveryRepeat = false,
	bool HasBonus = false)
{
	public IReadOnlyList<QuestFinishRewardGroupProjection> RewardGroups { get; init; } = RewardGroups ?? [];
	public IReadOnlyDictionary<string, IReadOnlyList<QuestFinishRewardItem>> ClassSelectableRewards { get; init; } =
		ClassSelectableRewards ?? new Dictionary<string, IReadOnlyList<QuestFinishRewardItem>>(StringComparer.Ordinal);
}

public sealed record QuestFinishRewardItemProjectionDescriptor(
	int Order,
	QuestFinishRewardItemSource Source,
	string JavaSource,
	bool IsLive,
	int ItemId,
	long Count,
	int? RewardGroupIndex = null,
	int? SelectableIndex = null,
	string? PlayerClass = null);

public sealed record QuestFinishRewardItemProjectionWarningDescriptor(
	QuestFinishRewardItemProjectionWarning Warning,
	string JavaSource,
	int? RewardGroupIndex = null,
	int? SelectableIndex = null,
	string? PlayerClass = null);

public sealed record QuestFinishRewardItemProjectionPlan(
	IReadOnlyList<QuestFinishRewardItemProjectionDescriptor> Items,
	IReadOnlyList<QuestFinishRewardItemProjectionWarningDescriptor> Warnings);

public sealed record QuestFinishRewardTemplateProjection(
	int? RewardGroupCount = null,
	bool HasItemRewards = false,
	bool HasNonItemRewards = false,
	bool IsChallengeTask = false,
	IReadOnlyList<QuestFinishRewardWorkItem>? WorkItems = null)
{
	public IReadOnlyList<QuestFinishRewardWorkItem> WorkItems { get; init; } = WorkItems ?? [];
}

public sealed record QuestFinishRewardGroupCorrectionResult(
	PlayerQuestState QuestState,
	QuestFinishRewardGroupCorrectionStatus Status,
	int? OriginalRewardGroup);

public sealed record QuestFinishRewardOperationDescriptor(
	int Order,
	QuestFinishRewardOperationAction Action,
	string JavaSource,
	bool IsLive,
	int? ItemId = null,
	long? Count = null);

public sealed record QuestFinishRewardOperationPlan(
	PlayerQuestState QuestState,
	IReadOnlyList<QuestFinishRewardOperationDescriptor> Descriptors,
	QuestFinishRewardGroupCorrectionStatus CorrectionStatus,
	int? OriginalRewardGroup);

public static class QuestFinishRewardPlanService
{
	private const int SelectedQuestReward1 = 8;
	private const int SelectedQuestReward15 = 22;
	private const int SelectedQuestNoReward = 23;
	private const string RewardGroupJavaSource = "game-server/src/com/aionemu/gameserver/services/QuestService.java#validateAndFixRewardGroup";
	private const string RewardItemJavaSource = "game-server/src/com/aionemu/gameserver/services/QuestService.java#getRewardItems";
	private const string GiveRewardJavaSource = "game-server/src/com/aionemu/gameserver/services/QuestService.java#giveReward";
	private const string ChallengeTaskJavaSource = "game-server/src/com/aionemu/gameserver/services/QuestService.java#finishQuest";
	private const string WorkItemJavaSource = "game-server/src/com/aionemu/gameserver/services/QuestService.java#removeQuestWorkItems";

	public static QuestFinishRewardOperationPlan CreatePlan(
		PlayerQuestState questState,
		QuestFinishRewardTemplateProjection template)
	{
		ArgumentNullException.ThrowIfNull(questState);
		ArgumentNullException.ThrowIfNull(template);

		var correction = CorrectRewardGroup(questState, template.RewardGroupCount);
		var descriptors = new List<QuestFinishRewardOperationDescriptor>();
		if (correction.Status is QuestFinishRewardGroupCorrectionStatus.IgnoredNonRewardState)
		{
			return new QuestFinishRewardOperationPlan(
				correction.QuestState,
				descriptors,
				correction.Status,
				correction.OriginalRewardGroup);
		}

		var order = 1;

		if (correction.Status is not QuestFinishRewardGroupCorrectionStatus.Unchanged)
		{
			descriptors.Add(new QuestFinishRewardOperationDescriptor(
				order++,
				QuestFinishRewardOperationAction.RewardGroupCorrection,
				RewardGroupJavaSource,
				IsLive: false));
		}

		if (template.HasItemRewards)
		{
			descriptors.Add(new QuestFinishRewardOperationDescriptor(
				order++,
				QuestFinishRewardOperationAction.ItemRewardPlaceholder,
				RewardItemJavaSource,
				IsLive: false));
		}

		if (template.HasNonItemRewards)
		{
			descriptors.Add(new QuestFinishRewardOperationDescriptor(
				order++,
				QuestFinishRewardOperationAction.NonItemRewardPlaceholder,
				GiveRewardJavaSource,
				IsLive: false));
		}

		if (template.IsChallengeTask)
		{
			descriptors.Add(new QuestFinishRewardOperationDescriptor(
				order++,
				QuestFinishRewardOperationAction.ChallengeTaskCompletionPlaceholder,
				ChallengeTaskJavaSource,
				IsLive: false));
		}

		foreach (var workItem in template.WorkItems)
		{
			descriptors.Add(new QuestFinishRewardOperationDescriptor(
				order++,
				QuestFinishRewardOperationAction.RemoveQuestWorkItemsPlaceholder,
				WorkItemJavaSource,
				IsLive: false,
				ItemId: workItem.ItemId,
				Count: workItem.Count));
		}

		return new QuestFinishRewardOperationPlan(
			correction.QuestState,
			descriptors,
			correction.Status,
			correction.OriginalRewardGroup);
	}

	public static QuestFinishRewardGroupCorrectionResult CorrectRewardGroup(
		PlayerQuestState questState,
		int? rewardGroupCount)
	{
		ArgumentNullException.ThrowIfNull(questState);

		if (!string.Equals(questState.Status, "REWARD", StringComparison.Ordinal))
		{
			return new QuestFinishRewardGroupCorrectionResult(
				questState,
				QuestFinishRewardGroupCorrectionStatus.IgnoredNonRewardState,
				questState.RewardGroup);
		}

		if (questState.RewardGroup is { } rewardGroup)
		{
			if (rewardGroupCount is null)
			{
				return new QuestFinishRewardGroupCorrectionResult(
					questState with { RewardGroup = null },
					QuestFinishRewardGroupCorrectionStatus.ClearedMissingRewards,
					rewardGroup);
			}

			if (rewardGroup < 0 || rewardGroup >= rewardGroupCount.Value)
			{
				return new QuestFinishRewardGroupCorrectionResult(
					questState with { RewardGroup = rewardGroupCount.Value - 1 },
					QuestFinishRewardGroupCorrectionStatus.ClampedOutOfRange,
					rewardGroup);
			}
		}
		else if (rewardGroupCount is > 0)
		{
			return new QuestFinishRewardGroupCorrectionResult(
				questState with { RewardGroup = 0 },
				QuestFinishRewardGroupCorrectionStatus.DefaultedFirstRewardGroup,
				null);
		}

		return new QuestFinishRewardGroupCorrectionResult(
			questState,
			QuestFinishRewardGroupCorrectionStatus.Unchanged,
			questState.RewardGroup);
	}

	public static QuestFinishRewardItemProjectionPlan CreateRewardItemProjection(
		QuestFinishRewardItemProjectionInput input,
		QuestFinishRewardItemTemplateProjection template)
	{
		ArgumentNullException.ThrowIfNull(template);

		var descriptors = new List<QuestFinishRewardItemProjectionDescriptor>();
		var warnings = new List<QuestFinishRewardItemProjectionWarningDescriptor>();
		var order = 1;
		var isLastRepeat = input.CompleteCount == input.RewardRepeatCount - 1;

		if (template.ExtendedRewards is not null && isLastRepeat)
		{
			AddFixedItems(
				descriptors,
				ref order,
				template.ExtendedRewards.FixedRewardItems,
				QuestFinishRewardItemSource.ExtendedFixed,
				template.ExtendedRewards.RewardGroupIndex);

			if (input.DialogActionId == SelectedQuestNoReward && template.ExtendedRewards.SelectableRewardItems.Count > 0)
			{
				if (input.ExtendedRewardIndex is not { } extendedRewardIndex)
				{
					warnings.Add(new QuestFinishRewardItemProjectionWarningDescriptor(
						QuestFinishRewardItemProjectionWarning.ExtendedRewardIndexMissing,
						RewardItemJavaSource));
				}
				else
				{
					var selectedIndex = ResolveExtendedSelectableRewardIndex(
						extendedRewardIndex,
						template.ExtendedRewards.SelectableRewardItems.Count);
					if (selectedIndex is { } index)
					{
						AddSelectableItem(
							descriptors,
							ref order,
							template.ExtendedRewards.SelectableRewardItems[index],
							QuestFinishRewardItemSource.ExtendedSelectable,
							template.ExtendedRewards.RewardGroupIndex,
							index,
							playerClass: null);
					}
					else
					{
						warnings.Add(new QuestFinishRewardItemProjectionWarningDescriptor(
							QuestFinishRewardItemProjectionWarning.ExtendedSelectableOutOfRange,
							RewardItemJavaSource,
							RewardGroupIndex: template.ExtendedRewards.RewardGroupIndex,
							SelectableIndex: extendedRewardIndex - 8));
					}
				}
			}
		}

		if (input.RewardGroup is { } rewardGroup)
		{
			var rewards = template.RewardGroups.FirstOrDefault(group => group.RewardGroupIndex == rewardGroup);
			if (rewards is null)
			{
				warnings.Add(new QuestFinishRewardItemProjectionWarningDescriptor(
					QuestFinishRewardItemProjectionWarning.RewardGroupOutOfRange,
					RewardItemJavaSource,
					RewardGroupIndex: rewardGroup));
			}
			else
			{
				AddFixedItems(
					descriptors,
					ref order,
					rewards.FixedRewardItems,
					QuestFinishRewardItemSource.RegularFixed,
					rewards.RewardGroupIndex);

				var rewardIndex = GetRewardIndex(input.DialogActionId);
				if (rewardIndex >= 0)
				{
					if (UsesClassSelectableRewards(template, isLastRepeat))
					{
						AddClassSelectableItem(descriptors, warnings, ref order, input, template, rewardIndex);
					}
					else if (rewardIndex < rewards.SelectableRewardItems.Count)
					{
						AddSelectableItem(
							descriptors,
							ref order,
							rewards.SelectableRewardItems[rewardIndex],
							QuestFinishRewardItemSource.RegularSelectable,
							rewards.RewardGroupIndex,
							rewardIndex,
							playerClass: null);
					}
					else
					{
						warnings.Add(new QuestFinishRewardItemProjectionWarningDescriptor(
							QuestFinishRewardItemProjectionWarning.RegularSelectableOutOfRange,
							RewardItemJavaSource,
							RewardGroupIndex: rewards.RewardGroupIndex,
							SelectableIndex: rewardIndex));
					}
				}
				else if (input.DialogActionId == SelectedQuestNoReward && UsesClassSelectableRewards(template, isLastRepeat))
				{
					if (input.ExtendedRewardIndex is { } extendedRewardIndex)
					{
						AddClassSelectableItem(descriptors, warnings, ref order, input, template, extendedRewardIndex - 8);
					}
					else
					{
						warnings.Add(new QuestFinishRewardItemProjectionWarningDescriptor(
							QuestFinishRewardItemProjectionWarning.ExtendedRewardIndexMissing,
							RewardItemJavaSource,
							RewardGroupIndex: rewards.RewardGroupIndex));
					}
				}
			}
		}

		if (template.HasBonus)
		{
			warnings.Add(new QuestFinishRewardItemProjectionWarningDescriptor(
				QuestFinishRewardItemProjectionWarning.BonusHandlerNotProjected,
				RewardItemJavaSource));
		}

		return new QuestFinishRewardItemProjectionPlan(descriptors, warnings);
	}

	public static int GetRewardIndex(int dialogActionId) =>
		dialogActionId >= SelectedQuestReward1 && dialogActionId <= SelectedQuestReward15
			? dialogActionId - SelectedQuestReward1
			: -1;

	private static bool UsesClassSelectableRewards(QuestFinishRewardItemTemplateProjection template, bool isLastRepeat) =>
		isLastRepeat && template.SingleTimeClassReward || template.ClassRewardOnEveryRepeat;

	private static int? ResolveExtendedSelectableRewardIndex(int extendedRewardIndex, int selectableRewardCount)
	{
		var minusEight = extendedRewardIndex - 8;
		if (minusEight >= 0 && minusEight < selectableRewardCount)
		{
			return minusEight;
		}

		var minusOne = extendedRewardIndex - 1;
		return minusOne >= 0 && minusOne < selectableRewardCount
			? minusOne
			: null;
	}

	private static void AddFixedItems(
		ICollection<QuestFinishRewardItemProjectionDescriptor> descriptors,
		ref int order,
		IReadOnlyList<QuestFinishRewardItem> fixedItems,
		QuestFinishRewardItemSource source,
		int rewardGroupIndex)
	{
		foreach (var item in fixedItems)
		{
			descriptors.Add(new QuestFinishRewardItemProjectionDescriptor(
				order++,
				source,
				RewardItemJavaSource,
				IsLive: false,
				ItemId: item.ItemId,
				Count: item.Count,
				RewardGroupIndex: rewardGroupIndex));
		}
	}

	private static void AddSelectableItem(
		ICollection<QuestFinishRewardItemProjectionDescriptor> descriptors,
		ref int order,
		QuestFinishRewardItem item,
		QuestFinishRewardItemSource source,
		int rewardGroupIndex,
		int selectableIndex,
		string? playerClass)
	{
		descriptors.Add(new QuestFinishRewardItemProjectionDescriptor(
			order++,
			source,
			RewardItemJavaSource,
			IsLive: false,
			ItemId: item.ItemId,
			Count: item.Count,
			RewardGroupIndex: rewardGroupIndex,
			SelectableIndex: selectableIndex,
			PlayerClass: playerClass));
	}

	private static void AddClassSelectableItem(
		ICollection<QuestFinishRewardItemProjectionDescriptor> descriptors,
		ICollection<QuestFinishRewardItemProjectionWarningDescriptor> warnings,
		ref int order,
		QuestFinishRewardItemProjectionInput input,
		QuestFinishRewardItemTemplateProjection template,
		int rewardIndex)
	{
		if (string.IsNullOrWhiteSpace(input.PlayerClass))
		{
			warnings.Add(new QuestFinishRewardItemProjectionWarningDescriptor(
				QuestFinishRewardItemProjectionWarning.PlayerClassMissing,
				RewardItemJavaSource,
				SelectableIndex: rewardIndex));
			return;
		}

		if (!template.ClassSelectableRewards.TryGetValue(input.PlayerClass, out var classRewards) ||
		    rewardIndex < 0 || rewardIndex >= classRewards.Count)
		{
			warnings.Add(new QuestFinishRewardItemProjectionWarningDescriptor(
				QuestFinishRewardItemProjectionWarning.ClassSelectableOutOfRange,
				RewardItemJavaSource,
				SelectableIndex: rewardIndex,
				PlayerClass: input.PlayerClass));
			return;
		}

		AddSelectableItem(
			descriptors,
			ref order,
			classRewards[rewardIndex],
			QuestFinishRewardItemSource.ClassSelectable,
			input.RewardGroup ?? -1,
			rewardIndex,
			input.PlayerClass);
	}
}
