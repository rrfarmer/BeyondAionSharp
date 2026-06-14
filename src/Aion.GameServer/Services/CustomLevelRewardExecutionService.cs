using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class CustomLevelRewardExecutionService
{
	private readonly ICustomLevelRewardRepository _repository;

	public CustomLevelRewardExecutionService(ICustomLevelRewardRepository repository)
	{
		_repository = repository;
	}

	public async ValueTask<CustomLevelRewardExecutionResult> CreateBonusPackExecutionPlanAsync(
		Player? player,
		Func<int> nextObjectId,
		DateTime receivedTime,
		Aion.GameServer.Dataholders.ItemData? itemData,
		CancellationToken cancellationToken = default)
	{
		return await CreateExecutionPlanAsync(
			CustomLevelRewardPackKind.Bonus,
			player,
			accountCreationLocalTime: null,
			nextObjectId,
			receivedTime,
			itemData,
			cancellationToken);
	}

	public async ValueTask<CustomLevelRewardExecutionResult> CreateFactionPackExecutionPlanAsync(
		Player? player,
		DateTime accountCreationLocalTime,
		Func<int> nextObjectId,
		DateTime receivedTime,
		Aion.GameServer.Dataholders.ItemData? itemData,
		CancellationToken cancellationToken = default)
	{
		return await CreateExecutionPlanAsync(
			CustomLevelRewardPackKind.Faction,
			player,
			accountCreationLocalTime,
			nextObjectId,
			receivedTime,
			itemData,
			cancellationToken);
	}

	private async ValueTask<CustomLevelRewardExecutionResult> CreateExecutionPlanAsync(
		CustomLevelRewardPackKind kind,
		Player? player,
		DateTime? accountCreationLocalTime,
		Func<int> nextObjectId,
		DateTime receivedTime,
		Aion.GameServer.Dataholders.ItemData? itemData,
		CancellationToken cancellationToken)
	{
		// Java parity: BonusPackService.addPlayerCustomReward / FactionPackService.sendRewards
		// run static guards before DAO load/store, store the receiving player, then send one system mail per reward.
		var preReceiptPlan = CreateRewardPlan(kind, player, accountCreationLocalTime, receivedPlayerId: 0, storeReceivingPlayerSucceeded: true, itemData);
		if (IsPreReceiptSkip(preReceiptPlan.Status))
			return CustomLevelRewardExecutionResult.FromPlan(kind, CustomLevelRewardExecutionStatus.SkippedBeforeReceipt, preReceiptPlan);
		if (player == null)
			return CustomLevelRewardExecutionResult.FromPlan(kind, CustomLevelRewardExecutionStatus.SkippedBeforeReceipt, preReceiptPlan);

		var receiptKind = ToReceiptKind(kind);
		var receivedPlayerId = await _repository.LoadReceivingPlayerAsync(receiptKind, player.AccountId, cancellationToken);
		if (receivedPlayerId > 0)
		{
			var alreadyReceivedPlan = CreateRewardPlan(kind, player, accountCreationLocalTime, receivedPlayerId, storeReceivingPlayerSucceeded: true, itemData);
			return CustomLevelRewardExecutionResult.FromPlan(
				kind,
				CustomLevelRewardExecutionStatus.SkippedAlreadyReceived,
				alreadyReceivedPlan,
				receivedPlayerId,
				StoreReceivingPlayerSucceeded: null);
		}

		var storeSucceeded = await _repository.StoreReceivingPlayerAsync(receiptKind, player.AccountId, player.ObjectId, cancellationToken);
		var rewardPlan = CreateRewardPlan(kind, player, accountCreationLocalTime, receivedPlayerId, storeSucceeded, itemData);
		if (!storeSucceeded)
		{
			return CustomLevelRewardExecutionResult.FromPlan(
				kind,
				CustomLevelRewardExecutionStatus.SkippedStoreFailed,
				rewardPlan,
				receivedPlayerId,
				storeSucceeded);
		}

		var mailPlans = new List<SystemMailRewardPlan>();
		foreach (var descriptor in rewardPlan.Descriptors.Where(descriptor => descriptor.Status == CustomLevelRewardDescriptorStatus.PlannedSystemMail))
		{
			var preflight = SystemMailRewardPlanService.CreatePlan(
				player,
				descriptor,
				mailObjectId: 0,
				attachedItemObjectId: 0,
				receivedTime,
				itemData);
			if (!preflight.Applied)
			{
				mailPlans.Add(preflight);
				continue;
			}

			var attachedItemObjectId = descriptor.Reward.ItemId == 0 ? 0 : nextObjectId();
			var mailObjectId = nextObjectId();
			mailPlans.Add(SystemMailRewardPlanService.CreatePlan(
				player,
				descriptor,
				mailObjectId,
				attachedItemObjectId,
				receivedTime,
				itemData));
		}

		var status = rewardPlan.Status == CustomLevelRewardPlanStatus.NoDeliverableRewards
			? CustomLevelRewardExecutionStatus.NoDeliverableRewards
			: CustomLevelRewardExecutionStatus.PlannedMail;
		return CustomLevelRewardExecutionResult.FromPlan(kind, status, rewardPlan, receivedPlayerId, storeSucceeded, mailPlans);
	}

	private static CustomLevelRewardPlan CreateRewardPlan(
		CustomLevelRewardPackKind kind,
		Player? player,
		DateTime? accountCreationLocalTime,
		int receivedPlayerId,
		bool storeReceivingPlayerSucceeded,
		Aion.GameServer.Dataholders.ItemData? itemData)
	{
		return kind == CustomLevelRewardPackKind.Bonus
			? CustomLevelRewardPlanService.CreateBonusPackPlan(player, receivedPlayerId, storeReceivingPlayerSucceeded)
			: CustomLevelRewardPlanService.CreateFactionPackPlan(
				player,
				accountCreationLocalTime ?? CustomLevelRewardPlanService.ElyosMinCreationTime,
				receivedPlayerId,
				storeReceivingPlayerSucceeded,
				itemData);
	}

	private static bool IsPreReceiptSkip(CustomLevelRewardPlanStatus status)
	{
		return status is CustomLevelRewardPlanStatus.MissingPlayer
			or CustomLevelRewardPlanStatus.NoRewardsConfigured
			or CustomLevelRewardPlanStatus.SkippedWrongLevel
			or CustomLevelRewardPlanStatus.SkippedMailboxLimit
			or CustomLevelRewardPlanStatus.SkippedCreationBeforeWindow
			or CustomLevelRewardPlanStatus.SkippedCreationAfterWindow;
	}

	private static CustomLevelRewardReceiptKind ToReceiptKind(CustomLevelRewardPackKind kind)
	{
		return kind == CustomLevelRewardPackKind.Bonus
			? CustomLevelRewardReceiptKind.Bonus
			: CustomLevelRewardReceiptKind.Faction;
	}
}

public sealed record CustomLevelRewardExecutionResult(
	CustomLevelRewardPackKind Kind,
	CustomLevelRewardExecutionStatus Status,
	CustomLevelRewardPlan RewardPlan,
	int? ReceivedPlayerId,
	bool? StoreReceivingPlayerSucceeded,
	IReadOnlyList<SystemMailRewardPlan> MailPlans,
	bool IsLiveReceiptBoundary,
	bool IsLiveMailBoundary,
	string JavaSource)
{
	public bool Applied => Status == CustomLevelRewardExecutionStatus.PlannedMail;

	public static CustomLevelRewardExecutionResult FromPlan(
		CustomLevelRewardPackKind kind,
		CustomLevelRewardExecutionStatus status,
		CustomLevelRewardPlan rewardPlan,
		int? ReceivedPlayerId = null,
		bool? StoreReceivingPlayerSucceeded = null,
		IReadOnlyList<SystemMailRewardPlan>? MailPlans = null)
	{
		return new CustomLevelRewardExecutionResult(
			kind,
			status,
			rewardPlan,
			ReceivedPlayerId,
			StoreReceivingPlayerSucceeded,
			MailPlans ?? Array.Empty<SystemMailRewardPlan>(),
			IsLiveReceiptBoundary: ReceivedPlayerId.HasValue || StoreReceivingPlayerSucceeded.HasValue,
			IsLiveMailBoundary: false,
			JavaSource: kind == CustomLevelRewardPackKind.Bonus
				? "game-server/src/com/aionemu/gameserver/services/BonusPackService.java#addPlayerCustomReward"
				: "game-server/src/com/aionemu/gameserver/services/FactionPackService.java#sendRewards");
	}
}

public enum CustomLevelRewardExecutionStatus
{
	PlannedMail,
	SkippedBeforeReceipt,
	SkippedAlreadyReceived,
	SkippedStoreFailed,
	NoDeliverableRewards,
}
