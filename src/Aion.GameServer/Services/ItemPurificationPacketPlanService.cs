namespace Aion.GameServer.Services;

public static class ItemPurificationPacketPlanService
{
	public const int UpgradeSuccessMessageId = 1402579;
	public const int InventoryUpdatePacketOpcode = 29;
	public const int DeleteItemPacketOpcode = 28;
	public const int InventoryAddPacketOpcode = 27;
	public const int CubeUpdatePacketOpcode = 130;
	public const int DecreaseItemUseUpdateType = 0x16;
	public const int UseDeleteType = 0x17;
	public const int ItemCollectAddType = 0x19;

	public static ItemPurificationPacketPlan CreatePacketPlan(
		ItemPurificationApplicationPlan? applicationPlan,
		string sourceItemName,
		string targetItemName)
	{
		// Java parity: ItemPurificationService.isPurificationAllowed sends the success system message
		// before decreaseMaterials and upgradeItem cause storage/AP/item packet fanout.
		if (applicationPlan == null)
			return ItemPurificationPacketPlan.Failed(ItemPurificationPacketPlanStatus.MissingApplicationPlan);
		if (applicationPlan.Operations.Count == 0)
			return ItemPurificationPacketPlan.Failed(ItemPurificationPacketPlanStatus.ApplicationPlanUnavailable);

		var packets = new List<ItemPurificationPacketOperation>
		{
			new(
				ItemPurificationPacketOperationType.UpgradeSuccessSystemMessage,
				0,
				0,
				0,
				UpgradeSuccessMessageId,
				null,
				[sourceItemName, targetItemName]),
		};

		foreach (var operation in applicationPlan.Operations)
			AddPacketOperations(packets, operation);

		var status = applicationPlan.Succeeded
			? ItemPurificationPacketPlanStatus.Ready
			: ItemPurificationPacketPlanStatus.NeedsRuntimeInputs;
		return new ItemPurificationPacketPlan(status, packets);
	}

	private static void AddPacketOperations(
		List<ItemPurificationPacketOperation> packets,
		ItemPurificationApplicationOperation operation)
	{
		switch (operation.Type)
		{
			case ItemPurificationApplicationOperationType.UpdateMaterialItemCount:
			case ItemPurificationApplicationOperationType.UpdateBaseItemCount:
				packets.Add(InventoryUpdate(operation));
				break;
			case ItemPurificationApplicationOperationType.DeleteMaterialItem:
			case ItemPurificationApplicationOperationType.DeleteBaseItem:
				packets.Add(DeleteItem(operation));
				packets.Add(CubeSize(operation));
				break;
			case ItemPurificationApplicationOperationType.SpendAbyssPoints:
				packets.Add(AbyssPointsUpdate(operation));
				break;
			case ItemPurificationApplicationOperationType.PreserveKinahNoOp:
				packets.Add(KinahNoPacket(operation));
				break;
			case ItemPurificationApplicationOperationType.AddTargetItem:
				packets.Add(InventoryAdd(operation));
				packets.Add(CubeSize(operation));
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(operation), operation.Type, "Unsupported item purification operation.");
		}
	}

	private static ItemPurificationPacketOperation InventoryUpdate(ItemPurificationApplicationOperation operation)
	{
		return new ItemPurificationPacketOperation(
			ItemPurificationPacketOperationType.InventoryUpdateItem,
			InventoryUpdatePacketOpcode,
			operation.ObjectId,
			operation.ItemId,
			DecreaseItemUseUpdateType,
			operation.Type,
			Parameters: Array.Empty<string>());
	}

	private static ItemPurificationPacketOperation DeleteItem(ItemPurificationApplicationOperation operation)
	{
		return new ItemPurificationPacketOperation(
			ItemPurificationPacketOperationType.DeleteItem,
			DeleteItemPacketOpcode,
			operation.ObjectId,
			operation.ItemId,
			UseDeleteType,
			operation.Type,
			Parameters: Array.Empty<string>());
	}

	private static ItemPurificationPacketOperation InventoryAdd(ItemPurificationApplicationOperation operation)
	{
		return new ItemPurificationPacketOperation(
			ItemPurificationPacketOperationType.InventoryAddItem,
			InventoryAddPacketOpcode,
			operation.ObjectId,
			operation.ItemId,
			ItemCollectAddType,
			operation.Type,
			Parameters: Array.Empty<string>());
	}

	private static ItemPurificationPacketOperation CubeSize(ItemPurificationApplicationOperation operation)
	{
		return new ItemPurificationPacketOperation(
			ItemPurificationPacketOperationType.CubeSizeUpdate,
			CubeUpdatePacketOpcode,
			operation.ObjectId,
			operation.ItemId,
			0,
			operation.Type,
			Parameters: Array.Empty<string>());
	}

	private static ItemPurificationPacketOperation AbyssPointsUpdate(ItemPurificationApplicationOperation operation)
	{
		return new ItemPurificationPacketOperation(
			ItemPurificationPacketOperationType.AbyssPointsUpdate,
			0,
			operation.ObjectId,
			operation.ItemId,
			0,
			operation.Type,
			Parameters: Array.Empty<string>());
	}

	private static ItemPurificationPacketOperation KinahNoPacket(ItemPurificationApplicationOperation operation)
	{
		return new ItemPurificationPacketOperation(
			ItemPurificationPacketOperationType.KinahNoPacket,
			0,
			operation.ObjectId,
			operation.ItemId,
			0,
			operation.Type,
			Parameters: Array.Empty<string>());
	}
}

public sealed record ItemPurificationPacketPlan(
	ItemPurificationPacketPlanStatus Status,
	IReadOnlyList<ItemPurificationPacketOperation> Operations)
{
	public bool Succeeded => Status == ItemPurificationPacketPlanStatus.Ready;

	public static ItemPurificationPacketPlan Failed(ItemPurificationPacketPlanStatus status)
	{
		return new ItemPurificationPacketPlan(status, Array.Empty<ItemPurificationPacketOperation>());
	}
}

public sealed record ItemPurificationPacketOperation(
	ItemPurificationPacketOperationType Type,
	int PacketOpcode,
	int ObjectId,
	int ItemId,
	int Mask,
	ItemPurificationApplicationOperationType? SourceOperationType,
	IReadOnlyList<string> Parameters);

public enum ItemPurificationPacketPlanStatus
{
	Ready,
	MissingApplicationPlan,
	ApplicationPlanUnavailable,
	NeedsRuntimeInputs,
}

public enum ItemPurificationPacketOperationType
{
	UpgradeSuccessSystemMessage,
	InventoryUpdateItem,
	DeleteItem,
	CubeSizeUpdate,
	AbyssPointsUpdate,
	KinahNoPacket,
	InventoryAddItem,
}
