using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

public sealed class StorageExpansionNpcService
{
	private const int KinahItemId = 182400001;
	private const int CubeStorageId = 0;
	private const int WarehouseExpansionLimit = 11;

	public StorageExpansionRequestPlan RequestCubeExpansion(
		Player player,
		IWorldNpcObject npc,
		StorageExpansionTemplateSummary? template,
		int cubeExpansionLimit,
		int npcCubeExpandsSizeLimit)
	{
		// Java parity: services/CubeExpandService.expandCube.
		if (template == null)
			return StorageExpansionRequestPlan.NotHandled(StorageExpansionRequestStatus.MissingTemplate);
		if (!CanExpandCube(player, cubeExpansionLimit))
			return StorageExpansionRequestPlan.Failed(StorageExpansionRequestStatus.CannotExpand, SmSystemMessage.InventoryCantExtendMore());

		var targetNpcExpands = (player.GetCommonData().GetNpcExpands()) + 1;
		if (targetNpcExpands < template.MinExpansionLevel)
		{
			return StorageExpansionRequestPlan.Failed(
				StorageExpansionRequestStatus.BelowTemplateMinLevel,
				SmSystemMessage.InventoryCantExtendBelowNpcMinimum(
					GetNpcL10n(npc),
					template.MinExpansionLevel - 1));
		}

		var maxExpansionLevel = Math.Min(template.MaxExpansionLevel, npcCubeExpandsSizeLimit);
		var price = template.GetPrice(targetNpcExpands);
		if (price == null || targetNpcExpands > maxExpansionLevel)
		{
			return StorageExpansionRequestPlan.Failed(
				StorageExpansionRequestStatus.AboveTemplateMaxLevel,
				SmSystemMessage.InventoryCantExtendAboveNpcMaximum(GetNpcL10n(npc), maxExpansionLevel));
		}

		return RegisterQuestion(player, npc, InventoryExpansionStorage.Cube, targetNpcExpands, price.Value);
	}

	public StorageExpansionRequestPlan RequestWarehouseExpansion(
		Player player,
		IWorldNpcObject npc,
		StorageExpansionTemplateSummary? template)
	{
		// Java parity: services/WarehouseService.expandWarehouse.
		if (template == null)
			return StorageExpansionRequestPlan.NotHandled(StorageExpansionRequestStatus.MissingTemplate);
		if (!CanExpandWarehouse(player))
			return StorageExpansionRequestPlan.Failed(StorageExpansionRequestStatus.CannotExpand, SmSystemMessage.WarehouseCantExtendMore());

		var targetNpcExpands = (player.GetCommonData().GetWhNpcExpands()) + 1;
		if (targetNpcExpands < template.MinExpansionLevel)
		{
			return StorageExpansionRequestPlan.Failed(
				StorageExpansionRequestStatus.BelowTemplateMinLevel,
				SmSystemMessage.WarehouseCantExtendBelowNpcMinimum(
					GetNpcL10n(npc),
					template.MinExpansionLevel - 1));
		}

		var price = template.GetPrice(targetNpcExpands);
		if (price == null || targetNpcExpands > template.MaxExpansionLevel)
		{
			return StorageExpansionRequestPlan.Failed(
				StorageExpansionRequestStatus.AboveTemplateMaxLevel,
				SmSystemMessage.WarehouseCantExtendAboveNpcMaximum(GetNpcL10n(npc), template.MaxExpansionLevel));
		}

		return RegisterQuestion(player, npc, InventoryExpansionStorage.Warehouse, targetNpcExpands, price.Value);
	}

	public StorageExpansionResponsePlan HandleResponse(
		Player player,
		int questionId,
		int response,
		ItemTemplateTable itemTemplates)
	{
		// Java parity: CM_QUESTION_RESPONSE delegates to ResponseRequester.respond; accepting runs
		// CubeExpandService/WarehouseService RequestResponseHandler.acceptRequest.
		if (questionId != SmQuestionWindow.WarehouseExpandWarning)
			return StorageExpansionResponsePlan.NotHandled(StorageExpansionResponseStatus.WrongQuestion);

		var dispatch = player.ResponseRequester.Respond(questionId, response);
		if (dispatch?.Request.Kind != QuestionResponseRequestKind.StorageExpansion)
		{
			player.PendingStorageExpansionRequest = null;
			return StorageExpansionResponsePlan.NotHandled(StorageExpansionResponseStatus.NoPendingRequest);
		}

		var request = dispatch.Request.Payload as PendingStorageExpansionRequest ?? player.PendingStorageExpansionRequest;
		player.PendingStorageExpansionRequest = null;
		if (request == null)
			return StorageExpansionResponsePlan.NotHandled(StorageExpansionResponseStatus.NoPendingRequest);

		if (!dispatch.Accepted)
			return StorageExpansionResponsePlan.CreateHandled(StorageExpansionResponseStatus.Denied);

		var inventory = player.InventoryItems.ToList();
		var kinah = inventory.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId);
		if (kinah == null || kinah.Count < request.Price)
		{
			return StorageExpansionResponsePlan.CreateHandled(
				StorageExpansionResponseStatus.NotEnoughKinah,
				SmSystemMessage.WarehouseExpandNotEnoughMoney());
		}

		var updatedKinah = CopyInventoryItem(kinah, kinah.Count - request.Price);
		ReplaceInventoryItem(inventory, updatedKinah);
		player.InventoryItems = inventory.ToArray();

		var packets = new List<AionServerPacket>();
		if (itemTemplates.GetItemTemplate(KinahItemId) is { } kinahTemplate)
		{
			packets.Add(new SmInventoryUpdateItem(
				updatedKinah,
				kinahTemplate,
				request.Storage == InventoryExpansionStorage.Cube
					? SmInventoryUpdateItem.DecreaseKinahCube
					: SmInventoryUpdateItem.DecreaseKinahBuy));
		}

		switch (request.Storage)
		{
			case InventoryExpansionStorage.Cube:
				player.GetCommonData().SetNpcExpands(request.TargetNpcExpands);
				packets.Add(SmSystemMessage.InventorySizeExtended(InventoryExpansionService.CubeSlotsPerExpansion));
				packets.Add(SM_CUBE_UPDATE.CubeSize(Aion.GameServer.Model.Items.Storage.StorageType.CUBE, player));
				break;
			case InventoryExpansionStorage.Warehouse:
				player.GetCommonData().SetWhNpcExpands(request.TargetNpcExpands);
				packets.Add(SmSystemMessage.WarehouseSizeExtended(InventoryExpansionService.WarehouseSlotsPerExpansion));
				packets.AddRange(SmWarehouseInfo.CreateRegularWarehouseUpdatePackets(player, itemTemplates));
				break;
		}

		return StorageExpansionResponsePlan.Accepted(request, updatedKinah, packets);
	}

	private static StorageExpansionRequestPlan RegisterQuestion(
		Player player,
		IWorldNpcObject npc,
		InventoryExpansionStorage storage,
		int targetNpcExpands,
		int price)
	{
		var request = new PendingStorageExpansionRequest(
			npc.ObjectId,
			npc.TemplateId,
			storage,
			targetNpcExpands,
			price,
			SmQuestionWindow.WarehouseExpandWarning);

		if (!player.ResponseRequester.PutRequest(
			SmQuestionWindow.WarehouseExpandWarning,
			new QuestionResponseRequest(npc.ObjectId, QuestionResponseRequestKind.StorageExpansion, request)))
		{
			return StorageExpansionRequestPlan.NotHandled(StorageExpansionRequestStatus.DuplicateQuestion);
		}

		player.PendingStorageExpansionRequest = request;
		return StorageExpansionRequestPlan.Requested(
			request,
			new SmQuestionWindow(SmQuestionWindow.WarehouseExpandWarning, 0, 0, price.ToString()));
	}

	private static bool CanExpandCube(Player player, int cubeExpansionLimit)
	{
		var newExpansions = (player.GetCommonData().GetNpcExpands()) + (player.GetCommonData().GetQuestExpands()) + (player.GetCommonData().GetItemExpands()) + 1;
		return newExpansions >= 0 && newExpansions <= cubeExpansionLimit;
	}

	private static bool CanExpandWarehouse(Player player)
	{
		var newExpansions = (player.GetCommonData().GetWhNpcExpands()) + (player.GetCommonData().GetWhBonusExpands()) + 1;
		return newExpansions >= 0 && newExpansions <= WarehouseExpansionLimit;
	}

	private static string GetNpcL10n(IWorldNpcObject npc)
	{
		// Java parity: npc.getObjectTemplate().getL10n().
		return npc.Template.NameId > 0 ? ChatUtil.L10n(npc.Template.NameId) : npc.Template.Name;
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, long count)
	{
		return new InventoryItem
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
			ManaStones = item.ManaStones,
			FusionStones = item.FusionStones,
			Godstone = item.Godstone,
			IdianStone = item.IdianStone,
		};
	}

	private static void ReplaceInventoryItem(List<InventoryItem> inventory, InventoryItem item)
	{
		var index = inventory.FindIndex(current => current.ObjectId == item.ObjectId);
		if (index >= 0)
			inventory[index] = item;
		else
			inventory.Add(item);
	}
}

public sealed record StorageExpansionRequestPlan(
	bool Handled,
	StorageExpansionRequestStatus Status,
	PendingStorageExpansionRequest? Request,
	SmQuestionWindow? QuestionWindow,
	IReadOnlyList<AionServerPacket> Packets)
{
	public static StorageExpansionRequestPlan Requested(PendingStorageExpansionRequest request, SmQuestionWindow questionWindow)
	{
		return new StorageExpansionRequestPlan(true, StorageExpansionRequestStatus.Requested, request, questionWindow, Array.Empty<AionServerPacket>());
	}

	public static StorageExpansionRequestPlan Failed(StorageExpansionRequestStatus status, AionServerPacket packet)
	{
		return new StorageExpansionRequestPlan(true, status, null, null, [packet]);
	}

	public static StorageExpansionRequestPlan NotHandled(StorageExpansionRequestStatus status)
	{
		return new StorageExpansionRequestPlan(false, status, null, null, Array.Empty<AionServerPacket>());
	}
}

public enum StorageExpansionRequestStatus
{
	Requested,
	MissingTemplate,
	CannotExpand,
	BelowTemplateMinLevel,
	AboveTemplateMaxLevel,
	DuplicateQuestion,
}

public sealed record StorageExpansionResponsePlan(
	bool Handled,
	StorageExpansionResponseStatus Status,
	PendingStorageExpansionRequest? Request,
	InventoryItem? KinahItemUpdate,
	IReadOnlyList<AionServerPacket> Packets)
{
	public static StorageExpansionResponsePlan Accepted(
		PendingStorageExpansionRequest request,
		InventoryItem kinahItemUpdate,
		IReadOnlyList<AionServerPacket> packets)
	{
		return new StorageExpansionResponsePlan(true, StorageExpansionResponseStatus.Accepted, request, kinahItemUpdate, packets);
	}

	public static StorageExpansionResponsePlan CreateHandled(StorageExpansionResponseStatus status, params AionServerPacket[] packets)
	{
		return new StorageExpansionResponsePlan(true, status, null, null, packets);
	}

	public static StorageExpansionResponsePlan NotHandled(StorageExpansionResponseStatus status)
	{
		return new StorageExpansionResponsePlan(false, status, null, null, Array.Empty<AionServerPacket>());
	}
}

public enum StorageExpansionResponseStatus
{
	Accepted,
	Denied,
	WrongQuestion,
	NoPendingRequest,
	NotEnoughKinah,
}
