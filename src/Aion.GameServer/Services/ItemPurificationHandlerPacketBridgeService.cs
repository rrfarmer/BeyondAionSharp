using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class ItemPurificationHandlerPacketBridgeService
{
	public static ItemPurificationHandlerPacketBridgeResult CreateConcretePacketPlan(
		ItemPurificationHandlerPlan? handlerPlan,
		IReadOnlyList<InventoryItem> postMutationInventoryItems,
		ItemTemplateTable itemTemplates,
		IReadOnlyDictionary<int, ItemPurificationCubeSnapshot> cubeSnapshotsByPacketOperationIndex)
	{
		// Java parity: services/item/ItemPurificationService sends the success message before
		// storage/AP/item fanout. This bridge only assembles the concrete packet inputs from
		// already-mutated snapshots; it does not perform persistence or live mutation.
		if (handlerPlan == null)
			return ItemPurificationHandlerPacketBridgeResult.Failed(ItemPurificationHandlerPacketBridgeStatus.MissingHandlerPlan);
		if (!handlerPlan.Application.Succeeded)
			return ItemPurificationHandlerPacketBridgeResult.Failed(ItemPurificationHandlerPacketBridgeStatus.ApplicationPlanNotReady);

		var inputs = ItemPurificationPacketInputSnapshotService.CreateInputs(
			handlerPlan.Application,
			postMutationInventoryItems,
			itemTemplates,
			cubeSnapshotsByPacketOperationIndex);
		if (!inputs.Succeeded)
			return new ItemPurificationHandlerPacketBridgeResult(
				ItemPurificationHandlerPacketBridgeStatus.PacketInputsNotReady,
				inputs,
				ConcretePacketPlan: null);

		var successMessage = handlerPlan.PacketPlan.Operations.FirstOrDefault(operation =>
			operation.Type == ItemPurificationPacketOperationType.UpgradeSuccessSystemMessage);
		var sourceItemName = successMessage?.Parameters.ElementAtOrDefault(0) ?? string.Empty;
		var targetItemName = successMessage?.Parameters.ElementAtOrDefault(1) ?? string.Empty;
		var packetPlan = ItemPurificationPacketPlanService.CreatePacketPlan(
			handlerPlan.Application,
			sourceItemName,
			targetItemName,
			inputs.InventoryPacketInputs,
			inputs.CubePacketInputsByPacketOperationIndex);

		return new ItemPurificationHandlerPacketBridgeResult(
			packetPlan.Succeeded
				? ItemPurificationHandlerPacketBridgeStatus.Ready
				: ItemPurificationHandlerPacketBridgeStatus.PacketPlanNotReady,
			inputs,
			packetPlan);
	}
}

public sealed record ItemPurificationHandlerPacketBridgeResult(
	ItemPurificationHandlerPacketBridgeStatus Status,
	ItemPurificationPacketInputSnapshotResult? PacketInputs,
	ItemPurificationPacketPlan? ConcretePacketPlan)
{
	public bool Succeeded => Status == ItemPurificationHandlerPacketBridgeStatus.Ready;

	public static ItemPurificationHandlerPacketBridgeResult Failed(ItemPurificationHandlerPacketBridgeStatus status)
	{
		return new ItemPurificationHandlerPacketBridgeResult(status, PacketInputs: null, ConcretePacketPlan: null);
	}
}

public enum ItemPurificationHandlerPacketBridgeStatus
{
	Ready,
	MissingHandlerPlan,
	ApplicationPlanNotReady,
	PacketInputsNotReady,
	PacketPlanNotReady,
}
