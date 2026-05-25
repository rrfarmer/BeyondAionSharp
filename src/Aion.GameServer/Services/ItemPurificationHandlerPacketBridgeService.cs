using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

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

	public static ItemPurificationHandlerMutationBridgeResult CreateConcretePacketPlanFromCurrentInventory(
		ItemPurificationHandlerPlan? handlerPlan,
		IReadOnlyList<InventoryItem> currentInventoryItems,
		ItemTemplateTable itemTemplates,
		int npcExpands,
		int questExpands,
		int itemExpands)
	{
		// Java parity: this composes the pre-live-mutation bridge for the same Storage mutation
		// order used by ItemPurificationService.decreaseMaterials and upgradeItem, but keeps the
		// C# handler side effect-free until persistence/runtime mutation is implemented.
		if (handlerPlan == null)
			return ItemPurificationHandlerMutationBridgeResult.Failed(ItemPurificationHandlerMutationBridgeStatus.MissingHandlerPlan);
		if (!handlerPlan.Application.Succeeded)
			return ItemPurificationHandlerMutationBridgeResult.Failed(ItemPurificationHandlerMutationBridgeStatus.ApplicationPlanNotReady);

		var mutationPreview = ItemPurificationMutationSnapshotService.CreatePreview(
			currentInventoryItems,
			handlerPlan.Application,
			npcExpands,
			questExpands,
			itemExpands);
		if (!mutationPreview.Succeeded)
		{
			return new ItemPurificationHandlerMutationBridgeResult(
				ItemPurificationHandlerMutationBridgeStatus.MutationSnapshotNotReady,
				mutationPreview,
				Bridge: null);
		}

		var bridge = CreateConcretePacketPlan(
			handlerPlan,
			mutationPreview.PostMutationInventoryItems,
			itemTemplates,
			mutationPreview.CubeSnapshotsByPacketOperationIndex);
		return new ItemPurificationHandlerMutationBridgeResult(
			bridge.Succeeded
				? ItemPurificationHandlerMutationBridgeStatus.Ready
				: ItemPurificationHandlerMutationBridgeStatus.PacketBridgeNotReady,
			mutationPreview,
			bridge);
	}

	public static async ValueTask<ItemPurificationHandlerPacketSendBridgeResult> SendConcretePacketsAsync(
		int playerObjectId,
		ItemPurificationHandlerPlan? handlerPlan,
		IReadOnlyList<InventoryItem> postMutationInventoryItems,
		ItemTemplateTable itemTemplates,
		IReadOnlyDictionary<int, ItemPurificationCubeSnapshot> cubeSnapshotsByPacketOperationIndex,
		IGameClientConnectionRegistry? connectionRegistry,
		CancellationToken cancellationToken = default)
	{
		// Java parity: this models the PacketSendUtility.sendPacket boundary after the
		// storage/AP/item operations have already produced concrete post-mutation snapshots.
		// It does not mutate inventory, persist, or synthesize AP rank packets.
		var bridge = CreateConcretePacketPlan(
			handlerPlan,
			postMutationInventoryItems,
			itemTemplates,
			cubeSnapshotsByPacketOperationIndex);
		if (!bridge.Succeeded || bridge.ConcretePacketPlan == null)
			return ItemPurificationHandlerPacketSendBridgeResult.FromBridge(bridge);

		var sendResult = await new ItemPurificationPacketSendAdapter(connectionRegistry).SendConcretePacketsAsync(
			playerObjectId,
			bridge.ConcretePacketPlan,
			cancellationToken);
		return new ItemPurificationHandlerPacketSendBridgeResult(
			sendResult.Succeeded
				? ItemPurificationHandlerPacketSendBridgeStatus.Ready
				: ItemPurificationHandlerPacketSendBridgeStatus.PacketSendNotReady,
			bridge,
			sendResult);
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

public sealed record ItemPurificationHandlerMutationBridgeResult(
	ItemPurificationHandlerMutationBridgeStatus Status,
	ItemPurificationMutationSnapshotPlan? MutationPreview,
	ItemPurificationHandlerPacketBridgeResult? Bridge)
{
	public bool Succeeded => Status == ItemPurificationHandlerMutationBridgeStatus.Ready;

	public static ItemPurificationHandlerMutationBridgeResult Failed(ItemPurificationHandlerMutationBridgeStatus status)
	{
		return new ItemPurificationHandlerMutationBridgeResult(status, MutationPreview: null, Bridge: null);
	}
}

public enum ItemPurificationHandlerMutationBridgeStatus
{
	Ready,
	MissingHandlerPlan,
	ApplicationPlanNotReady,
	MutationSnapshotNotReady,
	PacketBridgeNotReady,
}

public sealed record ItemPurificationHandlerPacketSendBridgeResult(
	ItemPurificationHandlerPacketSendBridgeStatus Status,
	ItemPurificationHandlerPacketBridgeResult? Bridge,
	ItemPurificationPacketSendResult? SendResult)
{
	public bool Succeeded => Status == ItemPurificationHandlerPacketSendBridgeStatus.Ready;

	public static ItemPurificationHandlerPacketSendBridgeResult FromBridge(ItemPurificationHandlerPacketBridgeResult bridge)
	{
		return new ItemPurificationHandlerPacketSendBridgeResult(
			bridge.Status switch
			{
				ItemPurificationHandlerPacketBridgeStatus.MissingHandlerPlan => ItemPurificationHandlerPacketSendBridgeStatus.MissingHandlerPlan,
				ItemPurificationHandlerPacketBridgeStatus.ApplicationPlanNotReady => ItemPurificationHandlerPacketSendBridgeStatus.ApplicationPlanNotReady,
				ItemPurificationHandlerPacketBridgeStatus.PacketInputsNotReady => ItemPurificationHandlerPacketSendBridgeStatus.PacketInputsNotReady,
				_ => ItemPurificationHandlerPacketSendBridgeStatus.PacketPlanNotReady,
			},
			bridge,
			SendResult: null);
	}
}

public enum ItemPurificationHandlerPacketSendBridgeStatus
{
	Ready,
	MissingHandlerPlan,
	ApplicationPlanNotReady,
	PacketInputsNotReady,
	PacketPlanNotReady,
	PacketSendNotReady,
}
