using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public static class ItemPurificationPersistentLiveExecutionService
{
	public static async ValueTask<ItemPurificationPersistentLiveExecutionResult> ExecuteAsync(
		int playerObjectId,
		Player? player,
		ItemPurificationHandlerPlan? handlerPlan,
		ItemTemplateTable itemTemplates,
		int npcExpands,
		int questExpands,
		int itemExpands,
		IGameClientConnectionRegistry? connectionRegistry,
		IPlayerEnterWorldRepository? repository,
		AbyssPointsAddOptions? abyssPointsOptions = null,
		int abyssTransformMinRank = AbyssSkillService.DefaultTransformMinRank,
		ItemRestrictionCleanupTable? itemRestrictionCleanups = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: CM_ITEM_PURIFICATION validates/sends success, mutates storage/AP, then
		// persistence later stores dirty inventory/rank state. This opt-in seam composes the
		// already-tested live execution and persistence payload without enabling automatic dispatch.
		if (repository == null)
			return ItemPurificationPersistentLiveExecutionResult.Failed(ItemPurificationPersistentLiveExecutionStatus.MissingRepository);

		var liveExecution = await ItemPurificationLiveExecutionService.ExecuteAsync(
			playerObjectId,
			player,
			handlerPlan,
			itemTemplates,
			npcExpands,
			questExpands,
			itemExpands,
			connectionRegistry,
			abyssPointsOptions,
			abyssTransformMinRank,
			itemRestrictionCleanups,
			questMutationNotifier: null,
			cancellationToken);
		if (!liveExecution.Succeeded)
		{
			return new ItemPurificationPersistentLiveExecutionResult(
				ItemPurificationPersistentLiveExecutionStatus.LiveExecutionNotReady,
				liveExecution,
				PersistencePlan: null,
				PersistenceSaved: false);
		}

		var persistencePlan = ItemPurificationPersistencePlanService.CreatePersistencePlan(
			handlerPlan?.Application,
			liveExecution.LiveMutation?.MutationPreview,
			liveExecution.LiveMutation?.AbyssPointsPlan);
		if (!persistencePlan.Succeeded)
		{
			return new ItemPurificationPersistentLiveExecutionResult(
				ItemPurificationPersistentLiveExecutionStatus.PersistencePlanNotReady,
				liveExecution,
				persistencePlan,
				PersistenceSaved: false);
		}

		var saved = await repository.SaveItemPurificationMutationAsync(
			playerObjectId,
			persistencePlan.MaterialItemUpdates,
			persistencePlan.DeletedMaterialItemObjectIds,
			persistencePlan.BaseItemUpdate,
			persistencePlan.DeletedBaseItemObjectId,
			persistencePlan.UpdatedTargetItems,
			persistencePlan.AddedTargetItems,
			persistencePlan.AbyssRank,
			cancellationToken);

		return new ItemPurificationPersistentLiveExecutionResult(
			saved
				? ItemPurificationPersistentLiveExecutionStatus.Ready
				: ItemPurificationPersistentLiveExecutionStatus.PersistenceSaveFailed,
			liveExecution,
			persistencePlan,
			saved);
	}
}

public sealed record ItemPurificationPersistentLiveExecutionResult(
	ItemPurificationPersistentLiveExecutionStatus Status,
	ItemPurificationLiveExecutionResult? LiveExecution,
	ItemPurificationPersistencePlan? PersistencePlan,
	bool PersistenceSaved)
{
	public bool Succeeded => Status == ItemPurificationPersistentLiveExecutionStatus.Ready;

	public static ItemPurificationPersistentLiveExecutionResult Failed(ItemPurificationPersistentLiveExecutionStatus status)
	{
		return new ItemPurificationPersistentLiveExecutionResult(
			status,
			LiveExecution: null,
			PersistencePlan: null,
			PersistenceSaved: false);
	}
}

public enum ItemPurificationPersistentLiveExecutionStatus
{
	Ready,
	MissingRepository,
	LiveExecutionNotReady,
	PersistencePlanNotReady,
	PersistenceSaveFailed,
}
