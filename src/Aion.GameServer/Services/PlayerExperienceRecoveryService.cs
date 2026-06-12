using System.Globalization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public static class PlayerExperienceRecoveryService
{
	private const int KinahItemId = 182400001;
	private const int CubeStorageId = 0;

	public static PlayerExperienceRecoveryDialogResult RequestDialog(Player player, int npcObjectId)
	{
		// Java parity: services/DialogService RECOVERY branch registers STR_ASK_RECOVER_EXPERIENCE
		// before sending SM_QUESTION_WINDOW.
		var recoverableExp = (player.GetCommonData().GetExpRecoverable());
		if (recoverableExp <= 0)
			return PlayerExperienceRecoveryDialogResult.WithPacket(
				PlayerExperienceRecoveryDialogStatus.NoRecoverableExperience,
				SmSystemMessage.DoNotHaveRecoverExperience());

		var price = CalculateRecoveryPrice(recoverableExp);
		var pending = new PendingExperienceRecoveryRequest(npcObjectId, recoverableExp, price);
		if (!player.ResponseRequester.PutRequest(
			SmQuestionWindow.AskRecoverExperience,
			new QuestionResponseRequest(npcObjectId, QuestionResponseRequestKind.ExperienceRecovery, pending)))
		{
			return PlayerExperienceRecoveryDialogResult.WithPacket(
				PlayerExperienceRecoveryDialogStatus.DuplicateQuestion,
				SmSystemMessage.CannotAskRecoverExperienceByOtherQuestion());
		}

		player.PendingExperienceRecoveryRequest = pending;
		return PlayerExperienceRecoveryDialogResult.Requested(
			new SmQuestionWindow(
				SmQuestionWindow.AskRecoverExperience,
				senderObjectId: 0,
				rangeOrCooldownSeconds: 0,
				price.ToString(CultureInfo.InvariantCulture)));
	}

	public static PlayerExperienceRecoveryResponseResult HandleResponse(
		Player player,
		int questionId,
		int response,
		ItemTemplateTable? itemTemplates = null,
		PlayerExperienceTable? experienceTable = null)
	{
		if (questionId != SmQuestionWindow.AskRecoverExperience)
			return PlayerExperienceRecoveryResponseResult.NotHandled();

		// Java parity: CM_QUESTION_RESPONSE delegates to ResponseRequester.respond, which removes
		// the RequestResponseHandler before invoking denyRequest/acceptRequest.
		var dispatch = player.ResponseRequester.Respond(questionId, response);
		if (dispatch?.Request.Kind != QuestionResponseRequestKind.ExperienceRecovery)
		{
			player.PendingExperienceRecoveryRequest = null;
			return PlayerExperienceRecoveryResponseResult.NotHandled();
		}

		var request = dispatch.Request.Payload as PendingExperienceRecoveryRequest
			?? player.PendingExperienceRecoveryRequest;
		player.PendingExperienceRecoveryRequest = null;
		if (request == null)
			return PlayerExperienceRecoveryResponseResult.NotHandled();

		if (!dispatch.Accepted)
		return PlayerExperienceRecoveryResponseResult.CreateHandled(PlayerExperienceRecoveryResponseStatus.Denied);

		var kinah = GetKinahItem(player);
		if (kinah == null || kinah.Count < request.Price)
		{
			return PlayerExperienceRecoveryResponseResult.CreateHandled(
				PlayerExperienceRecoveryResponseStatus.NotEnoughKinah,
				SmSystemMessage.NotEnoughKinah(request.Price));
		}

		var updatedKinah = CopyInventoryItem(kinah, kinah.Count - request.Price);
		ReplaceInventoryItem(player, updatedKinah);
		player.Exp += request.RecoverableExp;
		player.GetCommonData().SetRecoverableExp(0);

		var packets = new List<AionServerPacket>
		{
			SmSystemMessage.GetExp2(request.RecoverableExp),
			SmSystemMessage.SuccessRecoverExperience(),
		};
		if (experienceTable != null)
			packets.Add(new SmStatUpdateExp(player, experienceTable));

		var kinahTemplate = itemTemplates?.GetItemTemplate(KinahItemId);
		if (kinahTemplate != null)
			packets.Add(new SmInventoryUpdateItem(updatedKinah, kinahTemplate, SmInventoryUpdateItem.DecreaseKinahBuy));

		// Java also removes SPECIAL2 soul-sickness effects and resets death count here; those
		// runtime models are not present in this C# slice yet.
		return PlayerExperienceRecoveryResponseResult.CreateHandled(
			PlayerExperienceRecoveryResponseStatus.Recovered,
			packets.ToArray());
	}

	public static int CalculateRecoveryPrice(long recoverableExp)
	{
		// Java parity: final double factor = (expLost < 1000000 ? 0.25 - (0.00000015 * expLost) : 0.1).
		var factor = recoverableExp < 1_000_000
			? 0.25 - (0.00000015d * recoverableExp)
			: 0.1d;
		return (int)(recoverableExp * factor);
	}

	private static InventoryItem? GetKinahItem(Player player)
	{
		return player.InventoryItems
			.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId);
	}

	private static void ReplaceInventoryItem(Player player, InventoryItem updatedItem)
	{
		var items = player.InventoryItems.ToList();
		var index = items.FindIndex(item => item.ObjectId == updatedItem.ObjectId);
		if (index >= 0)
			items[index] = updatedItem;
		player.InventoryItems = items;
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
			ManaStones = item.ManaStones,
			FusionStones = item.FusionStones,
			Godstone = item.Godstone,
			IdianStone = item.IdianStone,
		};
	}
}

public sealed record PlayerExperienceRecoveryDialogResult(
	PlayerExperienceRecoveryDialogStatus Status,
	SmQuestionWindow? QuestionWindow,
	AionServerPacket? ResponsePacket)
{
	public static PlayerExperienceRecoveryDialogResult Requested(SmQuestionWindow questionWindow)
	{
		return new PlayerExperienceRecoveryDialogResult(
			PlayerExperienceRecoveryDialogStatus.Requested,
			questionWindow,
			null);
	}

	public static PlayerExperienceRecoveryDialogResult WithPacket(
		PlayerExperienceRecoveryDialogStatus status,
		AionServerPacket packet)
	{
		return new PlayerExperienceRecoveryDialogResult(status, null, packet);
	}
}

public enum PlayerExperienceRecoveryDialogStatus
{
	Requested,
	NoRecoverableExperience,
	DuplicateQuestion,
}

public sealed record PlayerExperienceRecoveryResponseResult(
	bool Handled,
	PlayerExperienceRecoveryResponseStatus Status,
	IReadOnlyList<AionServerPacket> Packets)
{
	public static PlayerExperienceRecoveryResponseResult NotHandled()
	{
		return new PlayerExperienceRecoveryResponseResult(
			false,
			PlayerExperienceRecoveryResponseStatus.NotHandled,
			Array.Empty<AionServerPacket>());
	}

	public static PlayerExperienceRecoveryResponseResult CreateHandled(
		PlayerExperienceRecoveryResponseStatus status,
		params AionServerPacket[] packets)
	{
		return new PlayerExperienceRecoveryResponseResult(true, status, packets);
	}
}

public enum PlayerExperienceRecoveryResponseStatus
{
	NotHandled,
	Denied,
	NotEnoughKinah,
	Recovered,
}
