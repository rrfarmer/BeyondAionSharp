using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public static class ItemPurificationLiveExecutionService
{
	public static async ValueTask<ItemPurificationLiveExecutionResult> ExecuteAsync(
		int playerObjectId,
		Player? player,
		ItemPurificationHandlerPlan? handlerPlan,
		ItemTemplateTable itemTemplates,
		int npcExpands,
		int questExpands,
		int itemExpands,
		IGameClientConnectionRegistry? connectionRegistry,
		AbyssPointsAddOptions? abyssPointsOptions = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: CM_ITEM_PURIFICATION sends the upgrade-success system message from
		// isPurificationAllowed before decreaseMaterials mutates storage/AP and upgradeItem adds
		// the target. This composition preserves that observable send order while keeping
		// persistence, quest hooks, and automatic handler invocation outside this boundary.
		if (player == null)
			return ItemPurificationLiveExecutionResult.Failed(ItemPurificationLiveExecutionStatus.MissingPlayer);

		var bridge = ItemPurificationHandlerPacketBridgeService.CreateConcretePacketPlanFromCurrentInventory(
			handlerPlan,
			player.InventoryItems,
			itemTemplates,
			npcExpands,
			questExpands,
			itemExpands);
		if (!bridge.Succeeded || bridge.Bridge?.ConcretePacketPlan == null)
		{
			return new ItemPurificationLiveExecutionResult(
				ItemPurificationLiveExecutionStatus.HandlerBridgeNotReady,
				bridge,
				SuccessMessageSend: null,
				LiveMutation: null,
				MutationPacketSend: null);
		}

		var packetPlan = bridge.Bridge.ConcretePacketPlan;
		var successMessagePlan = new ItemPurificationPacketPlan(
			packetPlan.Status,
			packetPlan.Operations.Take(1).ToArray());
		var remainingPacketPlan = new ItemPurificationPacketPlan(
			packetPlan.Status,
			packetPlan.Operations.Skip(1).ToArray());
		var sendAdapter = new ItemPurificationPacketSendAdapter(connectionRegistry);

		var successSend = await sendAdapter.SendConcretePacketsAsync(
			playerObjectId,
			successMessagePlan,
			cancellationToken);
		if (!successSend.Succeeded)
		{
			return new ItemPurificationLiveExecutionResult(
				ItemPurificationLiveExecutionStatus.SuccessMessageSendNotReady,
				bridge,
				successSend,
				LiveMutation: null,
				MutationPacketSend: null);
		}

		var liveMutation = ItemPurificationLiveMutationService.Apply(
			player,
			handlerPlan?.Application,
			npcExpands,
			questExpands,
			itemExpands,
			abyssPointsOptions);
		if (!liveMutation.Succeeded)
		{
			return new ItemPurificationLiveExecutionResult(
				ItemPurificationLiveExecutionStatus.LiveMutationNotReady,
				bridge,
				successSend,
				liveMutation,
				MutationPacketSend: null);
		}

		var mutationPacketPlan = CreateMutationPacketPlanWithAbyssPointsPackets(
			remainingPacketPlan,
			liveMutation.AbyssPointsPlan);
		var mutationSend = await sendAdapter.SendConcretePacketsAsync(
			playerObjectId,
			mutationPacketPlan,
			cancellationToken);
		return new ItemPurificationLiveExecutionResult(
			mutationSend.Succeeded
				? ItemPurificationLiveExecutionStatus.Ready
				: ItemPurificationLiveExecutionStatus.MutationPacketSendNotReady,
			bridge,
			successSend,
			liveMutation,
			mutationSend);
	}

	private static ItemPurificationPacketPlan CreateMutationPacketPlanWithAbyssPointsPackets(
		ItemPurificationPacketPlan packetPlan,
		AbyssPointsAddPlan? abyssPointsPlan)
	{
		// Java parity: ItemPurificationService.decreaseMaterials calls
		// AbyssPointsService.addAp at the AP operation point, which sends AP system/rank packets.
		// Rank-change broadcast and deeper side effects remain modeled metadata for later units.
		if (abyssPointsPlan?.Applied != true || abyssPointsPlan.PlayerPackets.Count == 0)
			return packetPlan;

		var operations = new List<ItemPurificationPacketOperation>();
		foreach (var operation in packetPlan.Operations)
		{
			if (operation.Type != ItemPurificationPacketOperationType.AbyssPointsUpdate)
			{
				operations.Add(operation);
				continue;
			}

			foreach (var packet in abyssPointsPlan.PlayerPackets)
			{
				operations.Add(operation with
				{
					ConcretePacket = packet,
				});
			}
		}

		return new ItemPurificationPacketPlan(packetPlan.Status, operations);
	}
}

public sealed record ItemPurificationLiveExecutionResult(
	ItemPurificationLiveExecutionStatus Status,
	ItemPurificationHandlerMutationBridgeResult? HandlerBridge,
	ItemPurificationPacketSendResult? SuccessMessageSend,
	ItemPurificationLiveMutationResult? LiveMutation,
	ItemPurificationPacketSendResult? MutationPacketSend)
{
	public bool Succeeded => Status == ItemPurificationLiveExecutionStatus.Ready;

	public static ItemPurificationLiveExecutionResult Failed(ItemPurificationLiveExecutionStatus status)
	{
		return new ItemPurificationLiveExecutionResult(
			status,
			HandlerBridge: null,
			SuccessMessageSend: null,
			LiveMutation: null,
			MutationPacketSend: null);
	}
}

public enum ItemPurificationLiveExecutionStatus
{
	Ready,
	MissingPlayer,
	HandlerBridgeNotReady,
	SuccessMessageSendNotReady,
	LiveMutationNotReady,
	MutationPacketSendNotReady,
}
