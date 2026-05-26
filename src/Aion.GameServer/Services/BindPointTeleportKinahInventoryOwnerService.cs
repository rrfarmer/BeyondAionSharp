using System.Collections.Concurrent;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum BindPointTeleportKinahInventoryOwnerMutationStatus
{
	NotEnoughKinah,
	ContinueWithoutMutation,
	AppliedMutation,
}

public enum BindPointTeleportKinahInventoryOwnerRollbackStatus
{
	NoMutationToRollback,
	RestoredOriginalKinah,
}

public sealed record BindPointTeleportKinahInventoryOwnerMutationResult(
	BindPointTeleportKinahInventoryOwnerMutationStatus Status,
	int PlayerObjectId,
	long RequiredPrice,
	long OriginalKinah,
	long? RemainingKinah,
	InventoryItem? OriginalKinahItem,
	InventoryItem? UpdatedKinahItem,
	IReadOnlyList<InventoryItem> InventoryBeforeMutation,
	IReadOnlyList<InventoryItem> InventoryAfterMutation,
	bool ShouldSendNotEnoughFee,
	bool ShouldEmitInventoryUpdatePacket,
	bool ShouldContinueScheduledTeleport,
	int? InventoryUpdateType,
	string JavaSource,
	bool IsLive);

public sealed record BindPointTeleportKinahInventoryOwnerRollbackResult(
	BindPointTeleportKinahInventoryOwnerRollbackStatus Status,
	BindPointTeleportKinahInventoryOwnerMutationResult MutationResult,
	IReadOnlyList<InventoryItem> InventoryAfterRollback,
	bool RestoredOriginalKinah,
	string JavaSource,
	bool IsLive);

public sealed class BindPointTeleportKinahInventoryOwnerService
{
	private readonly ConcurrentDictionary<int, object> _playerLocks = new();

	public BindPointTeleportKinahInventoryOwnerMutationResult TryApplyScheduledDecrease(
		Player player,
		long requiredPrice)
	{
		var syncRoot = _playerLocks.GetOrAdd(player.ObjectId, _ => new object());
		lock (syncRoot)
		{
			// Java parity: PlayerStorage.tryDecreaseKinah checks cube Kinah, mutates the Kinah item
			// when amount > 0, keeps zero-count Kinah, and prepares DEC_KINAH_FLY update metadata.
			// C# adds a per-player critical section as a conservative safety boundary.
			var inventoryBefore = player.InventoryItems.ToList();
			var kinahItem = inventoryBefore.FirstOrDefault(item =>
				item.ItemId == BindPointTeleportScheduledKinahMutationPlanService.KinahItemId
				&& item.Location == BindPointTeleportScheduledKinahMutationPlanService.CubeStorageId);
			var currentKinah = kinahItem?.Count ?? 0;

			if (requiredPrice <= 0)
			{
				return new BindPointTeleportKinahInventoryOwnerMutationResult(
					BindPointTeleportKinahInventoryOwnerMutationStatus.ContinueWithoutMutation,
					player.ObjectId,
					requiredPrice,
					currentKinah,
					RemainingKinah: currentKinah,
					kinahItem,
					UpdatedKinahItem: null,
					inventoryBefore,
					InventoryAfterMutation: inventoryBefore,
					ShouldSendNotEnoughFee: false,
					ShouldEmitInventoryUpdatePacket: false,
					ShouldContinueScheduledTeleport: true,
					InventoryUpdateType: null,
					"Storage.tryDecreaseKinah succeeds for non-positive amount and Storage.decreaseKinah amount > 0 guard prevents item mutation",
					IsLive: false);
			}

			if (kinahItem == null || currentKinah < requiredPrice)
			{
				return new BindPointTeleportKinahInventoryOwnerMutationResult(
					BindPointTeleportKinahInventoryOwnerMutationStatus.NotEnoughKinah,
					player.ObjectId,
					requiredPrice,
					currentKinah,
					RemainingKinah: null,
					kinahItem,
					UpdatedKinahItem: null,
					inventoryBefore,
					InventoryAfterMutation: inventoryBefore,
					ShouldSendNotEnoughFee: true,
					ShouldEmitInventoryUpdatePacket: false,
					ShouldContinueScheduledTeleport: false,
					InventoryUpdateType: null,
					"BindPointTeleportService scheduled task -> tryDecreaseKinah failed -> send STR_CANNOT_MOVE_TO_AIRPORT_NOT_ENOUGH_FEE and return",
					IsLive: false);
			}

			var updatedKinah = CopyInventoryItem(kinahItem, currentKinah - requiredPrice);
			var inventoryAfter = inventoryBefore
				.Select(item => item.ObjectId == updatedKinah.ObjectId ? updatedKinah : item)
				.ToArray();
			player.InventoryItems = inventoryAfter;

			return new BindPointTeleportKinahInventoryOwnerMutationResult(
				BindPointTeleportKinahInventoryOwnerMutationStatus.AppliedMutation,
				player.ObjectId,
				requiredPrice,
				currentKinah,
				RemainingKinah: updatedKinah.Count,
				kinahItem,
				updatedKinah,
				inventoryBefore,
				inventoryAfter,
				ShouldSendNotEnoughFee: false,
				ShouldEmitInventoryUpdatePacket: true,
				ShouldContinueScheduledTeleport: true,
				SmInventoryUpdateItem.DecreaseKinahFly,
				"Storage.tryDecreaseKinah -> decreaseKinah(price, DEC_KINAH_FLY) keeps zero-count Kinah and prepares SM_INVENTORY_UPDATE_ITEM",
				IsLive: false);
		}
	}

	public BindPointTeleportKinahInventoryOwnerRollbackResult RollbackScheduledDecrease(
		Player player,
		BindPointTeleportKinahInventoryOwnerMutationResult mutationResult)
	{
		var syncRoot = _playerLocks.GetOrAdd(player.ObjectId, _ => new object());
		lock (syncRoot)
		{
			if (mutationResult.Status != BindPointTeleportKinahInventoryOwnerMutationStatus.AppliedMutation
				|| mutationResult.OriginalKinahItem == null)
			{
				return new BindPointTeleportKinahInventoryOwnerRollbackResult(
					BindPointTeleportKinahInventoryOwnerRollbackStatus.NoMutationToRollback,
					mutationResult,
					player.InventoryItems.ToArray(),
					RestoredOriginalKinah: false,
					"C# scheduled bind-point Kinah owner rollback skipped because no in-memory mutation was applied",
					IsLive: false);
			}

			var inventoryAfterRollback = player.InventoryItems
				.Select(item => item.ObjectId == mutationResult.OriginalKinahItem.ObjectId
					? mutationResult.OriginalKinahItem
					: item)
				.ToArray();
			player.InventoryItems = inventoryAfterRollback;

			return new BindPointTeleportKinahInventoryOwnerRollbackResult(
				BindPointTeleportKinahInventoryOwnerRollbackStatus.RestoredOriginalKinah,
				mutationResult,
				inventoryAfterRollback,
				RestoredOriginalKinah: true,
				"C# scheduled bind-point Kinah owner restored the original Kinah snapshot after a later persistence or send failure",
				IsLive: false);
		}
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, long count)
	{
		var copy = new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = count,
			Color = item.Color,
			ColorExpires = item.ColorExpires,
			Creator = item.Creator,
			ExpireTime = item.ExpireTime,
			ActivationCount = item.ActivationCount,
			OwnerId = item.OwnerId,
			IsEquipped = item.IsEquipped,
			IsSoulBound = item.IsSoulBound,
			Slot = item.Slot,
			Location = item.Location,
			Enchant = item.Enchant,
			EnchantBonus = item.EnchantBonus,
			ItemSkin = item.ItemSkin,
			FusionedItem = item.FusionedItem,
			OptionalSocket = item.OptionalSocket,
			OptionalFusionSocket = item.OptionalFusionSocket,
			Charge = item.Charge,
			TuneCount = item.TuneCount,
			RandomBonus = item.RandomBonus,
			FusionRandomBonus = item.FusionRandomBonus,
			Tempering = item.Tempering,
			PackCount = item.PackCount,
			IsAmplified = item.IsAmplified,
			BuffSkill = item.BuffSkill,
			RandomPlumeBonus = item.RandomPlumeBonus,
		};
		copy.ManaStones = item.ManaStones;
		copy.FusionStones = item.FusionStones;
		copy.Godstone = item.Godstone;
		copy.IdianStone = item.IdianStone;
		return copy;
	}
}
