using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public static class TamperingActionExecutionPlanService
{
	public const int DelayMilliseconds = 5000;

	public static TamperingActionStartPlan CreateStartPlan(int playerObjectId, int sourceItemObjectId, int sourceItemId)
	{
		// Java parity: model/templates/item/actions/TamperingAction.act start animation.
		return new TamperingActionStartPlan(
			DelayMilliseconds,
			new SmItemUsageAnimation(playerObjectId, sourceItemObjectId, sourceItemId, DelayMilliseconds, 0, 0));
	}

	public static TamperingActionMutationPlan CreateMutationPlan(
		InventoryItem targetItem,
		ItemTemplateSummary targetTemplate,
		byte membershipLevel,
		IReadOnlyList<float>? tamperingChances,
		bool enableEnchantAnnounce,
		string playerName,
		Func<float>? nextChanceRoll = null,
		Func<int, int, int>? nextInclusiveRandom = null)
	{
		// Java parity: model/templates/item/actions/TamperingAction.act post-consume success/fail branch.
		var successChance = CalculateChance(targetItem, targetTemplate, membershipLevel, tamperingChances);
		var chanceRoll = nextChanceRoll?.Invoke() ?? Random.Shared.NextSingle() * 100f;
		if (chanceRoll < successChance)
		{
			var mutation = TamperingMutationService.SetTemperingLevel(
				targetItem,
				targetTemplate,
				targetItem.Tempering + 1,
				nextInclusiveRandom);
			var announcementPacket = enableEnchantAnnounce && mutation.UpdatedItem.Tempering == 10
				? SmSystemMessage.ItemAuthorizeSucceededMax(playerName, targetTemplate.GetClientName() ?? targetTemplate.Name, mutation.UpdatedItem.Tempering)
				: null;
			return new TamperingActionMutationPlan(
				TamperingActionMutationStatus.Succeeded,
				mutation.UpdatedItem,
				SmSystemMessage.ItemAuthorizeSucceeded(targetTemplate.GetClientName() ?? targetTemplate.Name, mutation.UpdatedItem.Tempering),
				announcementPacket);
		}

		var resetMutation = TamperingMutationService.SetTemperingLevel(
			targetItem,
			targetTemplate,
			0,
			nextInclusiveRandom);
		var isDestroyedPlume = targetTemplate.IsPlume;
		return new TamperingActionMutationPlan(
			isDestroyedPlume ? TamperingActionMutationStatus.FailedDestroyed : TamperingActionMutationStatus.FailedReset,
			resetMutation.UpdatedItem,
			isDestroyedPlume
				? SmSystemMessage.ItemAuthorizeFailedTShirt(targetTemplate.GetClientName() ?? targetTemplate.Name)
				: SmSystemMessage.ItemAuthorizeFailed(targetTemplate.GetClientName() ?? targetTemplate.Name),
			null);
	}

	public static float CalculateChance(
		InventoryItem targetItem,
		ItemTemplateSummary targetTemplate,
		byte membershipLevel,
		IReadOnlyList<float>? tamperingChances)
	{
		// Java parity: model/templates/item/actions/TamperingAction.calculateChance.
		if (targetItem.Tempering == 0)
			return 100f;

		if (targetTemplate.IsPlume)
			return Math.Max(25f, 100f - (targetItem.Tempering * 10f));

		if (tamperingChances == null || tamperingChances.Count == 0)
			return 1f;

		return tamperingChances[Math.Min(tamperingChances.Count - 1, membershipLevel)];
	}
}

public sealed record TamperingActionStartPlan(
	int DelayMilliseconds,
	SmItemUsageAnimation BroadcastPacket);

public enum TamperingActionMutationStatus
{
	Succeeded,
	FailedReset,
	FailedDestroyed,
}

public sealed record TamperingActionMutationPlan(
	TamperingActionMutationStatus Status,
	InventoryItem TargetItemUpdate,
	SmSystemMessage ResultMessage,
	SmSystemMessage? AnnouncementPacket);
