using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

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
				MutationPacketSend: null,
				EquipmentRankLimitChange: null,
				AbyssSkillUpdate: null);
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
				MutationPacketSend: null,
				EquipmentRankLimitChange: null,
				AbyssSkillUpdate: null);
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
				MutationPacketSend: null,
				EquipmentRankLimitChange: null,
				AbyssSkillUpdate: null);
		}

		var mutationPacketPlan = CreateMutationPacketPlanWithAbyssPointsPackets(
			remainingPacketPlan,
			liveMutation.AbyssPointsPlan);
		var mutationSend = await SendMutationPacketsWithAbyssRankBroadcastAsync(
			playerObjectId,
			player,
			mutationPacketPlan,
			liveMutation.AbyssPointsPlan,
			itemTemplates,
			connectionRegistry,
			cancellationToken);
		return new ItemPurificationLiveExecutionResult(
			mutationSend.SendResult.Succeeded
				? ItemPurificationLiveExecutionStatus.Ready
				: ItemPurificationLiveExecutionStatus.MutationPacketSendNotReady,
			bridge,
			successSend,
			liveMutation,
			mutationSend.SendResult,
			mutationSend.EquipmentRankLimitChange,
			mutationSend.AbyssSkillUpdate);
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

	private static async ValueTask<ItemPurificationMutationSendResult> SendMutationPacketsWithAbyssRankBroadcastAsync(
		int playerObjectId,
		Player player,
		ItemPurificationPacketPlan? packetPlan,
		AbyssPointsAddPlan? abyssPointsPlan,
		ItemTemplateTable itemTemplates,
		IGameClientConnectionRegistry? connectionRegistry,
		CancellationToken cancellationToken)
	{
		if (packetPlan == null)
			return new ItemPurificationMutationSendResult(
				ItemPurificationPacketSendResult.Failed(ItemPurificationPacketSendStatus.MissingPacketPlan),
				EquipmentRankLimitChange: null,
				AbyssSkillUpdate: null);
		if (!packetPlan.Succeeded)
			return new ItemPurificationMutationSendResult(
				ItemPurificationPacketSendResult.Failed(ItemPurificationPacketSendStatus.PacketPlanNotReady),
				EquipmentRankLimitChange: null,
				AbyssSkillUpdate: null);
		if (packetPlan.Operations.Count == 0)
			return new ItemPurificationMutationSendResult(
				ItemPurificationPacketSendResult.Failed(ItemPurificationPacketSendStatus.NoOperations),
				EquipmentRankLimitChange: null,
				AbyssSkillUpdate: null);

		var concreteOperations = new List<ItemPurificationPacketOperation>();
		var skippedMetadataOperations = new List<ItemPurificationPacketOperation>();
		var packets = new List<GameServerPacket>();
		var sentCount = 0;
		EquipmentChangeResult? equipmentRankLimitChange = null;
		AbyssSkillUpdateResult? abyssSkillUpdate = null;

		for (var index = 0; index < packetPlan.Operations.Count; index++)
		{
			var operation = packetPlan.Operations[index];
			if (operation.ConcretePacket == null)
			{
				skippedMetadataOperations.Add(operation);
				continue;
			}

			concreteOperations.Add(operation);
			packets.Add(operation.ConcretePacket);
			if (connectionRegistry != null)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (await connectionRegistry.SendPacketToPlayerAsync(playerObjectId, operation.ConcretePacket))
					sentCount++;

				if (operation.Type == ItemPurificationPacketOperationType.AbyssPointsUpdate
					&& IsLastAbyssPointsUpdate(packetPlan.Operations, index)
					&& abyssPointsPlan?.RankUpdatePacket != null)
				{
					cancellationToken.ThrowIfCancellationRequested();
					await connectionRegistry.BroadcastToVisiblePlayersAsync(
						player.Position,
						player.ObjectId,
						abyssPointsPlan.RankUpdatePacket);
				}
			}

			if (operation.Type == ItemPurificationPacketOperationType.AbyssPointsUpdate
				&& IsLastAbyssPointsUpdate(packetPlan.Operations, index)
				&& abyssPointsPlan?.ShouldCheckRankLimitItems == true)
			{
				var rankLimitChange = EquipmentService.CheckRankLimitItems(player, itemTemplates);
				if (rankLimitChange.Changed || rankLimitChange.RankLimitedUnequipMessages.Count > 0)
				{
					player.InventoryItems = rankLimitChange.InventoryItems;
					equipmentRankLimitChange = rankLimitChange;
					sentCount += await SendEquipmentRankLimitPacketsAsync(
						playerObjectId,
						player,
						rankLimitChange,
						itemTemplates,
						connectionRegistry,
						packets,
						cancellationToken);
				}
			}

			if (operation.Type == ItemPurificationPacketOperationType.AbyssPointsUpdate
				&& IsLastAbyssPointsUpdate(packetPlan.Operations, index)
				&& abyssPointsPlan?.ShouldUpdateAbyssSkills == true)
			{
				var skillUpdate = AbyssSkillService.UpdateSkills(player);
				if (skillUpdate.Changed)
				{
					player.Skills = skillUpdate.Skills;
					abyssSkillUpdate = skillUpdate;
					sentCount += await SendAbyssSkillPacketsAsync(
						playerObjectId,
						skillUpdate,
						connectionRegistry,
						packets,
						cancellationToken);
				}
			}
		}

		return new ItemPurificationMutationSendResult(
			new ItemPurificationPacketSendResult(
				ItemPurificationPacketSendStatus.Ready,
				packetPlan.Status,
				concreteOperations,
				packets,
				skippedMetadataOperations,
				sentCount),
			equipmentRankLimitChange,
			abyssSkillUpdate);
	}

	private static async ValueTask<int> SendEquipmentRankLimitPacketsAsync(
		int playerObjectId,
		Player player,
		EquipmentChangeResult rankLimitChange,
		ItemTemplateTable itemTemplates,
		IGameClientConnectionRegistry? connectionRegistry,
		ICollection<GameServerPacket> packets,
		CancellationToken cancellationToken)
	{
		var sentCount = 0;
		foreach (var update in rankLimitChange.InventoryUpdateItems)
		{
			if (itemTemplates.GetItemTemplate(update.ItemId) is not { } template)
				continue;

			var packet = new SmInventoryUpdateItem(update, template, SmInventoryUpdateItem.EquipUnequip);
			packets.Add(packet);
			if (connectionRegistry != null)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (await connectionRegistry.SendPacketToPlayerAsync(playerObjectId, packet))
					sentCount++;
			}
		}

		foreach (var itemName in rankLimitChange.RankLimitedUnequipMessages)
		{
			var packet = SmSystemMessage.UnequipRankItem(itemName);
			packets.Add(packet);
			if (connectionRegistry != null)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (await connectionRegistry.SendPacketToPlayerAsync(playerObjectId, packet))
					sentCount++;
			}
		}

		if (rankLimitChange.BroadcastAppearance && connectionRegistry != null)
		{
			cancellationToken.ThrowIfCancellationRequested();
			await connectionRegistry.BroadcastToVisiblePlayersAsync(
				player.Position,
				player.ObjectId,
				new SmUpdatePlayerAppearance(player),
				includeSourcePlayer: true);
		}

		return sentCount;
	}

	private static async ValueTask<int> SendAbyssSkillPacketsAsync(
		int playerObjectId,
		AbyssSkillUpdateResult skillUpdate,
		IGameClientConnectionRegistry? connectionRegistry,
		ICollection<GameServerPacket> packets,
		CancellationToken cancellationToken)
	{
		var sentCount = 0;
		foreach (var removedSkill in skillUpdate.RemovedSkills)
		{
			var packet = new SmSkillRemove(removedSkill);
			packets.Add(packet);
			if (connectionRegistry != null)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (await connectionRegistry.SendPacketToPlayerAsync(playerObjectId, packet))
					sentCount++;
			}
		}

		foreach (var addedSkill in skillUpdate.AddedSkills)
		{
			var packet = new SmSkillList([addedSkill], 1300050);
			packets.Add(packet);
			if (connectionRegistry != null)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (await connectionRegistry.SendPacketToPlayerAsync(playerObjectId, packet))
					sentCount++;
			}
		}

		return sentCount;
	}

	private static bool IsLastAbyssPointsUpdate(
		IReadOnlyList<ItemPurificationPacketOperation> operations,
		int index)
	{
		return index == operations.Count - 1
			|| operations[index + 1].Type != ItemPurificationPacketOperationType.AbyssPointsUpdate;
	}
}

public sealed record ItemPurificationLiveExecutionResult(
	ItemPurificationLiveExecutionStatus Status,
	ItemPurificationHandlerMutationBridgeResult? HandlerBridge,
	ItemPurificationPacketSendResult? SuccessMessageSend,
	ItemPurificationLiveMutationResult? LiveMutation,
	ItemPurificationPacketSendResult? MutationPacketSend,
	EquipmentChangeResult? EquipmentRankLimitChange,
	AbyssSkillUpdateResult? AbyssSkillUpdate)
{
	public bool Succeeded => Status == ItemPurificationLiveExecutionStatus.Ready;

	public static ItemPurificationLiveExecutionResult Failed(ItemPurificationLiveExecutionStatus status)
	{
		return new ItemPurificationLiveExecutionResult(
			status,
			HandlerBridge: null,
			SuccessMessageSend: null,
			LiveMutation: null,
			MutationPacketSend: null,
			EquipmentRankLimitChange: null,
			AbyssSkillUpdate: null);
	}
}

internal sealed record ItemPurificationMutationSendResult(
	ItemPurificationPacketSendResult SendResult,
	EquipmentChangeResult? EquipmentRankLimitChange,
	AbyssSkillUpdateResult? AbyssSkillUpdate);

public enum ItemPurificationLiveExecutionStatus
{
	Ready,
	MissingPlayer,
	HandlerBridgeNotReady,
	SuccessMessageSendNotReady,
	LiveMutationNotReady,
	MutationPacketSendNotReady,
}
