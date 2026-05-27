using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services.ToyPet;

public enum PetFeedPacketMetadataBridgeStatus
{
	NoOperations,
	Constructed,
	PartiallyConstructed,
	Blocked,
}

public enum PetFeedPacketMetadataResultStatus
{
	Constructed,
	SkippedNonPacketOperation,
	BlockedItemUnlockPacket,
	BlockedEmotionContext,
	BlockedSystemMessageContext,
}

public enum PetFeedUnlockPacketStorageKind
{
	Unknown,
	Cube,
	Warehouse,
	LegionWarehouse,
	AccountWarehouse,
}

public sealed record PetFeedUnlockPacketContext(
	PetFeedUnlockPacketStorageKind StorageKind,
	InventoryItem? Item = null,
	ItemTemplateSummary? Template = null,
	int CubeItemsCount = 0,
	int NpcExpands = 0,
	int QuestExpands = 0,
	int ItemExpands = 0,
	int StorageItemsCount = 0);

public sealed record PetFeedSupplementalPacketContext(
	int? PlayerObjectId = null,
	PlayerCreatureState PlayerCreatureState = PlayerCreatureState.Active,
	string? PetName = null,
	string? ItemName = null,
	PetFeedUnlockPacketContext? UnlockPacketContext = null);

public sealed record PetFeedPacketMetadataBridgeRequest(
	PetFeedServiceOperationPlan Plan,
	int FeedProgressData,
	int RefeedDelaySeconds = 0,
	PetFeedSupplementalPacketContext? SupplementalContext = null);

public sealed record PetFeedPacketMetadataResult(
	PetFeedServiceOperation Operation,
	PetFeedPacketMetadataResultStatus Status,
	GameServerPacket? Packet,
	string Notes,
	IReadOnlyList<GameServerPacket>? PacketSequence = null)
{
	public IReadOnlyList<GameServerPacket> Packets => PacketSequence ?? (Packet is null ? [] : [Packet]);
}

public sealed record PetFeedPacketMetadataBridgeResult(
	PetFeedPacketMetadataBridgeStatus Status,
	IReadOnlyList<PetFeedPacketMetadataResult> Results,
	bool ExecutesLivePackets,
	bool IsLive,
	bool IsJavaRuntimeParity);

public sealed class PetFeedPacketMetadataBridge
{
	public PetFeedPacketMetadataBridgeResult Construct(PetFeedPacketMetadataBridgeRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(request.Plan);

		if (request.Plan.Operations.Count == 0)
		{
			return new PetFeedPacketMetadataBridgeResult(
				PetFeedPacketMetadataBridgeStatus.NoOperations,
				Results: [],
				ExecutesLivePackets: false,
				IsLive: false,
				IsJavaRuntimeParity: false);
		}

		var results = request.Plan.Operations
			.Select(operation => Construct(operation, request.FeedProgressData, request.RefeedDelaySeconds, request.SupplementalContext))
			.ToArray();

		var constructed = results.Count(result => result.Status == PetFeedPacketMetadataResultStatus.Constructed);
		var blocked = results.Length - constructed - results.Count(result => result.Status == PetFeedPacketMetadataResultStatus.SkippedNonPacketOperation);
		var status = constructed switch
		{
			0 when blocked == 0 => PetFeedPacketMetadataBridgeStatus.NoOperations,
			0 => PetFeedPacketMetadataBridgeStatus.Blocked,
			_ when blocked > 0 => PetFeedPacketMetadataBridgeStatus.PartiallyConstructed,
			_ => PetFeedPacketMetadataBridgeStatus.Constructed,
		};

		return new PetFeedPacketMetadataBridgeResult(
			status,
			results,
			ExecutesLivePackets: false,
			IsLive: false,
			IsJavaRuntimeParity: false);
	}

	private static PetFeedPacketMetadataResult Construct(
		PetFeedServiceOperation operation,
		int feedProgressData,
		int refeedDelaySeconds,
		PetFeedSupplementalPacketContext? supplementalContext)
	{
		return operation.Kind switch
		{
			PetFeedServiceOperationKind.SendPetFeedProgressPacket => ConstructSmPet(
				operation,
				subType: 2,
				feedProgressData,
				itemObjectId: operation.ItemObjectId ?? 0,
				count: operation.Count ?? 0,
				refeedDelaySeconds),
			PetFeedServiceOperationKind.SendPetFeedEndPacket => ConstructSmPet(
				operation,
				subType: 5,
				feedProgressData,
				itemObjectId: 0,
				count: 0,
				refeedDelaySeconds),
			PetFeedServiceOperationKind.SendPetRewardItemPacket => ConstructSmPet(
				operation,
				subType: 6,
				feedProgressData,
				itemObjectId: operation.ItemId ?? 0,
				count: 0,
				refeedDelaySeconds),
			PetFeedServiceOperationKind.SendPetRefeedPacket => ConstructSmPet(
				operation,
				subType: 7,
				feedProgressData,
				itemObjectId: 0,
				count: 0,
				refeedDelaySeconds),
			PetFeedServiceOperationKind.UnlockFoodItem => ConstructFoodItemUnlock(operation, supplementalContext),
			PetFeedServiceOperationKind.SendEndFeedingEmotion => ConstructEndFeedingEmotion(operation, supplementalContext),
			PetFeedServiceOperationKind.SendFoodNotLovedSystemMessage => ConstructFoodNotLovedSystemMessage(operation, supplementalContext),
			_ => new PetFeedPacketMetadataResult(
				operation,
				PetFeedPacketMetadataResultStatus.SkippedNonPacketOperation,
				Packet: null,
				Notes: "Operation is not a packet-construction boundary."),
		};
	}

	private static PetFeedPacketMetadataResult ConstructSmPet(
		PetFeedServiceOperation operation,
		int subType,
		int feedProgressData,
		int itemObjectId,
		int count,
		int refeedDelaySeconds)
	{
		// Java parity: network/aion/serverpackets/SM_PET FOOD subtypes are constructed but never sent here.
		return new PetFeedPacketMetadataResult(
			operation,
			PetFeedPacketMetadataResultStatus.Constructed,
			SmPet.Food(new SmPetFoodSnapshot(subType, feedProgressData, itemObjectId, count, refeedDelaySeconds)),
			Notes: $"Constructed non-sending SmPet FOOD subtype {subType} metadata.");
	}

	private static PetFeedPacketMetadataResult ConstructFoodItemUnlock(
		PetFeedServiceOperation operation,
		PetFeedSupplementalPacketContext? supplementalContext)
	{
		var context = supplementalContext?.UnlockPacketContext;
		if (context is null)
		{
			return Blocked(
				operation,
				PetFeedPacketMetadataResultStatus.BlockedItemUnlockPacket,
				"Item unlock packet/service boundary needs supplied storage and item/template snapshot context.");
		}

		if (context.StorageKind != PetFeedUnlockPacketStorageKind.Cube)
			return ConstructWarehouseItemUnlock(operation, context);

		if (context.Item is null || context.Template is null)
		{
			return Blocked(
				operation,
				PetFeedPacketMetadataResultStatus.BlockedItemUnlockPacket,
				"Normal-cube unlock metadata needs supplied item and template snapshots.");
		}

		// Java parity: ItemPacketService.sendItemUnlockPacket -> sendStorageUpdatePacket(CUBE, item, ALL_SLOT)
		// emits SM_INVENTORY_ADD_ITEM first, then SM_CUBE_UPDATE.cubeSize(StorageType.CUBE, player).
		var inventoryAdd = SmInventoryAddItem.CreateAllSlot(context.Item, context.Template);
		var cubeUpdate = SmCubeUpdate.CubeSizeSnapshot(
			context.CubeItemsCount,
			context.NpcExpands,
			context.QuestExpands,
			context.ItemExpands);
		return new PetFeedPacketMetadataResult(
			operation,
			PetFeedPacketMetadataResultStatus.Constructed,
			inventoryAdd,
			Notes: "Constructed non-sending normal-cube unlock metadata: SmInventoryAddItem ALL_SLOT followed by SmCubeUpdate.",
			PacketSequence: [inventoryAdd, cubeUpdate]);
	}

	private static PetFeedPacketMetadataResult ConstructWarehouseItemUnlock(
		PetFeedServiceOperation operation,
		PetFeedUnlockPacketContext context)
	{
		if (context.StorageKind is PetFeedUnlockPacketStorageKind.LegionWarehouse)
		{
			return Blocked(
				operation,
				PetFeedPacketMetadataResultStatus.BlockedItemUnlockPacket,
				"Legion warehouse unlock metadata is blocked until SM_LEGION_EDIT kinah behavior and legion storage snapshots are modeled.");
		}

		if (context.StorageKind is not (PetFeedUnlockPacketStorageKind.Warehouse or PetFeedUnlockPacketStorageKind.AccountWarehouse))
		{
			return Blocked(
				operation,
				PetFeedPacketMetadataResultStatus.BlockedItemUnlockPacket,
				$"Storage kind {context.StorageKind} is not supported by the rejected-food unlock metadata boundary.");
		}

		if (context.Item is null || context.Template is null)
		{
			return Blocked(
				operation,
				PetFeedPacketMetadataResultStatus.BlockedItemUnlockPacket,
				"Warehouse unlock metadata needs supplied item and template snapshots.");
		}

		var warehouseType = context.StorageKind == PetFeedUnlockPacketStorageKind.Warehouse
			? SmWarehouseAddItem.RegularWarehouse
			: SmWarehouseAddItem.AccountWarehouse;
		var warehouseAdd = SmWarehouseAddItem.CreateAllSlot(warehouseType, context.Item, context.Template);
		var cubeUpdate = context.StorageKind == PetFeedUnlockPacketStorageKind.Warehouse
			? SmCubeUpdate.RegularWarehouseSizeSnapshot(context.StorageItemsCount, context.NpcExpands, context.QuestExpands)
			: SmCubeUpdate.AccountWarehouseSize();

		// Java parity: ItemPacketService.sendStorageUpdatePacket non-cube default emits SM_WAREHOUSE_ADD_ITEM
		// followed by SM_CUBE_UPDATE.cubeSize(storageType, player).
		return new PetFeedPacketMetadataResult(
			operation,
			PetFeedPacketMetadataResultStatus.Constructed,
			warehouseAdd,
			Notes: $"Constructed non-sending {context.StorageKind} unlock metadata: SmWarehouseAddItem ALL_SLOT followed by SmCubeUpdate.",
			PacketSequence: [warehouseAdd, cubeUpdate]);
	}

	private static PetFeedPacketMetadataResult ConstructEndFeedingEmotion(
		PetFeedServiceOperation operation,
		PetFeedSupplementalPacketContext? supplementalContext)
	{
		if (supplementalContext?.PlayerObjectId is not { } playerObjectId)
		{
			return Blocked(
				operation,
				PetFeedPacketMetadataResultStatus.BlockedEmotionContext,
				"SM_EMOTION EndFeeding needs a supplied player object id.");
		}

		// Java parity: PetService.checkFeeding sends new SM_EMOTION(player, END_FEEDING, 0, player.getObjectId()).
		var player = new Player
		{
			ObjectId = playerObjectId,
			CreatureState = supplementalContext.PlayerCreatureState,
		};
		return new PetFeedPacketMetadataResult(
			operation,
			PetFeedPacketMetadataResultStatus.Constructed,
			new SmEmotion(player, EmotionType.EndFeeding, emotion: 0, targetObjectId: playerObjectId),
			Notes: "Constructed non-sending SmEmotion EndFeeding metadata from supplied player context.");
	}

	private static PetFeedPacketMetadataResult ConstructFoodNotLovedSystemMessage(
		PetFeedServiceOperation operation,
		PetFeedSupplementalPacketContext? supplementalContext)
	{
		if (supplementalContext?.PetName is not { Length: > 0 } petName
			|| supplementalContext.ItemName is not { Length: > 0 } itemName)
		{
			return Blocked(
				operation,
				PetFeedPacketMetadataResultStatus.BlockedSystemMessageContext,
				"Rejected-food system message needs supplied pet name and localized item name context.");
		}

		// Java parity: SM_SYSTEM_MESSAGE.STR_MSG_TOYPET_FEED_FOOD_NOT_LOVEFLAVOR(pet.getName(), item.getItemTemplate().getL10n()).
		return new PetFeedPacketMetadataResult(
			operation,
			PetFeedPacketMetadataResultStatus.Constructed,
			new SmSystemMessage(1400618, petName, itemName),
			Notes: "Constructed non-sending rejected-food system-message metadata from supplied names.");
	}

	private static PetFeedPacketMetadataResult Blocked(
		PetFeedServiceOperation operation,
		PetFeedPacketMetadataResultStatus status,
		string notes)
	{
		return new PetFeedPacketMetadataResult(operation, status, Packet: null, notes);
	}
}
