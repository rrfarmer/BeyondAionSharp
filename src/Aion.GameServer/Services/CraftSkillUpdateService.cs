using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

public sealed class CraftSkillUpdateService
{
	public const int DefaultMaxExpertCraftingSkills = 2;
	public const int DefaultMaxMasterCraftingSkills = 1;

	private const int KinahItemId = 182400001;
	private const int CubeStorageId = 0;

	private static readonly IReadOnlyDictionary<int, CraftProfession> ProfessionsByNpc =
		new Dictionary<int, CraftProfession>
		{
			[204096] = CraftProfession.Essencetapping,
			[830150] = CraftProfession.Essencetapping,
			[204257] = CraftProfession.Aethertapping,
			[830148] = CraftProfession.Aethertapping,
			[204100] = CraftProfession.Cooking,
			[830142] = CraftProfession.Cooking,
			[204104] = CraftProfession.Weaponsmithing,
			[830146] = CraftProfession.Weaponsmithing,
			[204106] = CraftProfession.Armorsmithing,
			[830144] = CraftProfession.Armorsmithing,
			[204110] = CraftProfession.Tailoring,
			[830136] = CraftProfession.Tailoring,
			[204102] = CraftProfession.Alchemy,
			[830138] = CraftProfession.Alchemy,
			[204108] = CraftProfession.Handicrafting,
			[830140] = CraftProfession.Handicrafting,
			[798452] = CraftProfession.Construction,
			[798456] = CraftProfession.Construction,
			[203780] = CraftProfession.Essencetapping,
			[830066] = CraftProfession.Essencetapping,
			[203782] = CraftProfession.Aethertapping,
			[830064] = CraftProfession.Aethertapping,
			[203784] = CraftProfession.Cooking,
			[830058] = CraftProfession.Cooking,
			[203788] = CraftProfession.Weaponsmithing,
			[830062] = CraftProfession.Weaponsmithing,
			[203790] = CraftProfession.Armorsmithing,
			[830060] = CraftProfession.Armorsmithing,
			[203793] = CraftProfession.Tailoring,
			[830052] = CraftProfession.Tailoring,
			[203786] = CraftProfession.Alchemy,
			[830054] = CraftProfession.Alchemy,
			[203792] = CraftProfession.Handicrafting,
			[830056] = CraftProfession.Handicrafting,
			[798450] = CraftProfession.Construction,
			[798454] = CraftProfession.Construction,
		};

	public CraftProfession? GetProfessionByNpc(IWorldNpcObject npc)
	{
		// Java parity: services/craft/CraftSkillUpdateService.getProfessionByNpc.
		return ProfessionsByNpc.TryGetValue(npc.TemplateId, out var profession) ? profession : null;
	}

	public CraftSkillLearnRequestPlan RequestLearnSkill(Player player, IWorldNpcObject npc, SkillTemplateTable skillTemplates)
	{
		// Java parity: services/craft/CraftSkillUpdateService.learnSkill.
		if (player.Level < 10)
			return CraftSkillLearnRequestPlan.NotHandled(CraftSkillLearnRequestStatus.TooLowLevel);

		if (GetProfessionByNpc(npc) is not { } profession)
			return CraftSkillLearnRequestPlan.NotHandled(CraftSkillLearnRequestStatus.UnknownProfessionNpc);

		var skillId = profession.GetSkillId();
		if (skillId == 0)
			return CraftSkillLearnRequestPlan.NotHandled(CraftSkillLearnRequestStatus.UnknownProfessionNpc);

		var currentSkill = player.Skills.FirstOrDefault(skill => skill.SkillId == skillId);
		var currentSkillLevel = currentSkill?.SkillLevel ?? 0;
		var price = profession.GetUpgradeCost(currentSkillLevel);
		if (price == null)
			return CraftSkillLearnRequestPlan.Failed(GetRankUpFailure(player.ObjectId, profession, currentSkillLevel));

		var targetSkillLevel = currentSkillLevel + 1;
		var professionName = GetProfessionName(profession, targetSkillLevel, skillTemplates);
		var request = new PendingCraftSkillLearnRequest(
			npc.ObjectId,
			npc.TemplateId,
			skillId,
			currentSkillLevel,
			targetSkillLevel,
			price.Value,
			professionName,
			SmQuestionWindow.CraftAddSkillConfirm);

		if (!player.ResponseRequester.PutRequest(
			SmQuestionWindow.CraftAddSkillConfirm,
			new QuestionResponseRequest(npc.ObjectId, QuestionResponseRequestKind.CraftSkillLearn, request)))
		{
			return CraftSkillLearnRequestPlan.NotHandled(CraftSkillLearnRequestStatus.DuplicateQuestion);
		}

		player.PendingCraftSkillLearnRequest = request;
		return CraftSkillLearnRequestPlan.Requested(
			request,
			new SmQuestionWindow(
				SmQuestionWindow.CraftAddSkillConfirm,
				senderObjectId: 0,
				rangeOrCooldownSeconds: 0,
				professionName,
				price.Value.ToString()));
	}

	public CraftSkillLearnResponsePlan HandleResponse(
		Player player,
		int questionId,
		int response,
		ItemTemplateTable itemTemplates)
	{
		// Java parity: CM_QUESTION_RESPONSE delegates to ResponseRequester.respond; accepting runs
		// CraftSkillUpdateService RequestResponseHandler.acceptRequest.
		if (questionId != SmQuestionWindow.CraftAddSkillConfirm)
			return CraftSkillLearnResponsePlan.NotHandled(CraftSkillLearnResponseStatus.WrongQuestion);

		var dispatch = player.ResponseRequester.Respond(questionId, response);
		if (dispatch?.Request.Kind != QuestionResponseRequestKind.CraftSkillLearn)
		{
			player.PendingCraftSkillLearnRequest = null;
			return CraftSkillLearnResponsePlan.NotHandled(CraftSkillLearnResponseStatus.NoPendingRequest);
		}

		var request = dispatch.Request.Payload as PendingCraftSkillLearnRequest ?? player.PendingCraftSkillLearnRequest;
		player.PendingCraftSkillLearnRequest = null;
		if (request == null)
			return CraftSkillLearnResponsePlan.NotHandled(CraftSkillLearnResponseStatus.NoPendingRequest);

		if (!dispatch.Accepted)
			return CraftSkillLearnResponsePlan.CreateHandled(CraftSkillLearnResponseStatus.Denied);

		var inventory = player.InventoryItems.ToList();
		var kinah = inventory.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId);
		if (kinah == null || kinah.Count < request.Price)
		{
			return CraftSkillLearnResponsePlan.CreateHandled(
				CraftSkillLearnResponseStatus.NotEnoughKinah,
				SmSystemMessage.NotEnoughMoney());
		}

		var updatedKinah = CopyInventoryItem(kinah, kinah.Count - request.Price);
		ReplaceInventoryItem(inventory, updatedKinah);
		var updatedSkills = player.Skills.ToList();
		var existingIndex = updatedSkills.FindIndex(skill => skill.SkillId == request.SkillId);
		var updatedSkill = new PlayerSkill
		{
			SkillId = request.SkillId,
			SkillLevel = request.TargetSkillLevel,
			SkillType = existingIndex >= 0 ? updatedSkills[existingIndex].SkillType : 0,
			CurrentXp = existingIndex >= 0 ? updatedSkills[existingIndex].CurrentXp : 0,
		};
		var isNew = existingIndex < 0;
		if (existingIndex >= 0)
			updatedSkills[existingIndex] = updatedSkill;
		else
			updatedSkills.Add(updatedSkill);

		player.InventoryItems = inventory.ToArray();
		player.Skills = updatedSkills.ToArray();

		var packets = new List<AionServerPacket>();
		if (itemTemplates.GetItemTemplate(KinahItemId) is { } kinahTemplate)
			packets.Add(new SmInventoryUpdateItem(updatedKinah, kinahTemplate, SmInventoryUpdateItem.DecreaseKinahLearn));
		packets.Add(new SmSkillList([updatedSkill], SkillLearnServiceMessages.GetMessageId(updatedSkill, isNew)));

		return CraftSkillLearnResponsePlan.Accepted(request, updatedKinah, updatedSkill, packets);
	}

	public int GetTotalExpertCraftingSkills(Player player)
	{
		// Java parity: services/craft/CraftSkillUpdateService.getTotalExpertCraftingSkills.
		return CountCraftingSkills(player, skillLevel => skillLevel > 399 && skillLevel <= 499);
	}

	public int GetTotalMasterCraftingSkills(Player player)
	{
		// Java parity: services/craft/CraftSkillUpdateService.getTotalMasterCraftingSkills.
		return CountCraftingSkills(player, skillLevel => skillLevel > 499);
	}

	public CraftSkillLimitResult CanLearnMoreExpertCraftingSkill(
		Player player,
		int maxExpertCraftingSkills = DefaultMaxExpertCraftingSkills)
	{
		// Java parity: services/craft/CraftSkillUpdateService.canLearnMoreExpertCraftingSkill.
		var current = GetTotalExpertCraftingSkills(player) + GetTotalMasterCraftingSkills(player);
		return current < maxExpertCraftingSkills
			? CraftSkillLimitResult.CreateAllowed(current, maxExpertCraftingSkills)
			: CraftSkillLimitResult.CreateBlocked(
				current,
				maxExpertCraftingSkills,
				$"You can only be an expert in {maxExpertCraftingSkills} professions.");
	}

	public CraftSkillLimitResult CanLearnMoreMasterCraftingSkill(
		Player player,
		int maxMasterCraftingSkills = DefaultMaxMasterCraftingSkills)
	{
		// Java parity: services/craft/CraftSkillUpdateService.canLearnMoreMasterCraftingSkill.
		var current = GetTotalMasterCraftingSkills(player);
		return current < maxMasterCraftingSkills
			? CraftSkillLimitResult.CreateAllowed(current, maxMasterCraftingSkills)
			: CraftSkillLimitResult.CreateBlocked(
				current,
				maxMasterCraftingSkills,
				$"You can only be a master in {maxMasterCraftingSkills} professions.");
	}

	private static int CountCraftingSkills(Player player, Func<int, bool> levelPredicate)
	{
		var count = 0;
		foreach (var profession in Enum.GetValues<CraftProfession>())
		{
			if (!profession.IsCrafting())
				continue;
			var skillId = profession.GetSkillId();
			var skill = player.Skills.FirstOrDefault(playerSkill => playerSkill.SkillId == skillId);
			if (skill != null && levelPredicate(skill.SkillLevel))
				count++;
		}
		return count;
	}

	private static CraftPacketIntent GetRankUpFailure(int playerObjectId, CraftProfession profession, int skillLevel)
	{
		if (skillLevel > profession.GetMaxUpgradableLevel())
			return new CraftPacketIntent(playerObjectId, SmSystemMessage.DontRankUpGathering());
		if (skillLevel == 399)
			return new CraftPacketIntent(playerObjectId, SmSystemMessage.CraftCantExtendMoney());
		if (skillLevel == 499)
			return new CraftPacketIntent(playerObjectId, SmSystemMessage.CraftCantExtendGrandMaster());
		return new CraftPacketIntent(playerObjectId, SmSystemMessage.DontRankUp());
	}

	private static string GetProfessionName(CraftProfession profession, int targetSkillLevel, SkillTemplateTable skillTemplates)
	{
		var clientName = skillTemplates.GetSkillTemplate(profession.GetSkillId())?.GetClientName()
			?? profession.GetSkillId().ToString();
		return targetSkillLevel == 1
			? clientName
			: string.Concat(GetSkillGrade(targetSkillLevel), " ", clientName);
	}

	private static string GetSkillGrade(int skillLevel)
	{
		return skillLevel switch
		{
			<= 99 => ChatUtil.L10n(900797),
			<= 199 => ChatUtil.L10n(900798),
			<= 299 => ChatUtil.L10n(900799),
			<= 399 => ChatUtil.L10n(900800),
			<= 449 => ChatUtil.L10n(900801),
			<= 499 => ChatUtil.L10n(902027),
			_ => ChatUtil.L10n(902028),
		};
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

	private static void ReplaceInventoryItem(List<InventoryItem> inventory, InventoryItem item)
	{
		var index = inventory.FindIndex(current => current.ObjectId == item.ObjectId);
		if (index >= 0)
			inventory[index] = item;
		else
			inventory.Add(item);
	}
}

public enum CraftProfession
{
	Essencetapping,
	Aethertapping,
	Cooking,
	Weaponsmithing,
	Armorsmithing,
	Tailoring,
	Alchemy,
	Handicrafting,
	Construction,
}

public static class CraftProfessionExtensions
{
	public static int GetSkillId(this CraftProfession profession)
	{
		return profession switch
		{
			CraftProfession.Essencetapping => 30002,
			CraftProfession.Aethertapping => 30003,
			CraftProfession.Cooking => 40001,
			CraftProfession.Weaponsmithing => 40002,
			CraftProfession.Armorsmithing => 40003,
			CraftProfession.Tailoring => 40004,
			CraftProfession.Alchemy => 40007,
			CraftProfession.Handicrafting => 40008,
			CraftProfession.Construction => 40010,
			_ => 0,
		};
	}

	public static bool IsCrafting(this CraftProfession profession)
	{
		var skillId = profession.GetSkillId();
		return skillId is >= 40001 and <= 40010;
	}

	public static int? GetUpgradeCost(this CraftProfession profession, int skillLevel)
	{
		return skillLevel switch
		{
			0 => 3500,
			99 => 17000,
			199 => 115000,
			299 => 460000,
			449 when profession.IsCrafting() => 6004900,
			_ => null,
		};
	}

	public static int GetMaxUpgradableLevel(this CraftProfession profession)
	{
		return profession.IsCrafting() ? 499 : 399;
	}
}

public sealed record CraftPacketIntent(int RecipientObjectId, AionServerPacket Packet);

public sealed record CraftSkillLimitResult(
	bool Allowed,
	int CurrentCount,
	int MaxCount,
	SmMessage? Message)
{
	public static CraftSkillLimitResult CreateAllowed(int currentCount, int maxCount)
	{
		return new CraftSkillLimitResult(true, currentCount, maxCount, null);
	}

	public static CraftSkillLimitResult CreateBlocked(int currentCount, int maxCount, string message)
	{
		return new CraftSkillLimitResult(false, currentCount, maxCount, new SmMessage(message));
	}
}

public sealed record CraftSkillLearnRequestPlan(
	bool Handled,
	CraftSkillLearnRequestStatus Status,
	PendingCraftSkillLearnRequest? Request,
	SmQuestionWindow? QuestionWindow,
	IReadOnlyList<CraftPacketIntent> PacketIntents)
{
	public static CraftSkillLearnRequestPlan Requested(PendingCraftSkillLearnRequest request, SmQuestionWindow questionWindow)
	{
		return new CraftSkillLearnRequestPlan(true, CraftSkillLearnRequestStatus.Requested, request, questionWindow, Array.Empty<CraftPacketIntent>());
	}

	public static CraftSkillLearnRequestPlan Failed(CraftPacketIntent intent)
	{
		return new CraftSkillLearnRequestPlan(true, CraftSkillLearnRequestStatus.NotUpgradable, null, null, [intent]);
	}

	public static CraftSkillLearnRequestPlan NotHandled(CraftSkillLearnRequestStatus status)
	{
		return new CraftSkillLearnRequestPlan(false, status, null, null, Array.Empty<CraftPacketIntent>());
	}
}

public enum CraftSkillLearnRequestStatus
{
	Requested,
	TooLowLevel,
	UnknownProfessionNpc,
	NotUpgradable,
	DuplicateQuestion,
}

public sealed record CraftSkillLearnResponsePlan(
	bool Handled,
	CraftSkillLearnResponseStatus Status,
	PendingCraftSkillLearnRequest? Request,
	InventoryItem? KinahItemUpdate,
	PlayerSkill? Skill,
	IReadOnlyList<AionServerPacket> Packets)
{
	public static CraftSkillLearnResponsePlan Accepted(
		PendingCraftSkillLearnRequest request,
		InventoryItem kinahItemUpdate,
		PlayerSkill skill,
		IReadOnlyList<AionServerPacket> packets)
	{
		return new CraftSkillLearnResponsePlan(true, CraftSkillLearnResponseStatus.Accepted, request, kinahItemUpdate, skill, packets);
	}

	public static CraftSkillLearnResponsePlan CreateHandled(CraftSkillLearnResponseStatus status, params AionServerPacket[] packets)
	{
		return new CraftSkillLearnResponsePlan(true, status, null, null, null, packets);
	}

	public static CraftSkillLearnResponsePlan NotHandled(CraftSkillLearnResponseStatus status)
	{
		return new CraftSkillLearnResponsePlan(false, status, null, null, null, Array.Empty<AionServerPacket>());
	}
}

public enum CraftSkillLearnResponseStatus
{
	Accepted,
	Denied,
	WrongQuestion,
	NoPendingRequest,
	NotEnoughKinah,
}
