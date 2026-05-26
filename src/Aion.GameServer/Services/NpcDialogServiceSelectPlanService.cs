namespace Aion.GameServer.Services;

public enum NpcDialogServiceSelectStatus
{
	QuestEngineOrNextPage,
	BuyTradeList,
	BuyUnavailable,
	DialogWindow,
	UnsupportedDialogWindowAction,
	ServiceDispatch,
	TradeInList,
	TradeInUnavailable,
	SellItemWindow,
}

public enum NpcDialogServiceDescriptorKind
{
	QuestEngineDialog,
	DialogWindowNextPage,
	SystemMessageDoesNotSellItem,
	TradeListPacket,
	DialogWindowFromAction,
	LegionDisbandRequest,
	LegionRecreate,
	ExperienceRecoveryRequest,
	PvpTeleport,
	AirlineService,
	CraftSkillLearn,
	CubeExpansion,
	WarehouseExpansion,
	LegionWarehouseOpen,
	PlasticSurgery,
	AutoGroupOrDialogWindow,
	NpcFactionJoin,
	NpcFactionLeave,
	RepurchasePacket,
	PetDialogPacket,
	ItemChargeRequest,
	TradeInListPacket,
	SellItemPacket,
	CraftStatusRelinquish,
	StudioRecreate,
}

public sealed record NpcDialogServiceSelectInput(
	NpcDialogServiceFallbackDescriptor Fallback,
	bool NpcSupportsAction = true,
	bool HasTradeList = false,
	bool HasSellableTradeGoods = false,
	int VendorBuyModifier = 100,
	int TradeSellPriceRate = 100,
	bool HasTradeInList = false,
	SmTradeListPacketPlan? TradeListPacketPlan = null);

public sealed record NpcDialogServiceDescriptor(
	NpcDialogServiceDescriptorKind Kind,
	int TargetObjectId,
	int DialogActionId,
	string JavaSource,
	bool IsLive = false,
	int? QuestId = null,
	int? ExtendedRewardIndex = null,
	int? PriceModifier = null,
	SmTradeListPacketPlan? TradeListPacketPlan = null);

public sealed record NpcDialogServiceSelectPlan(
	NpcDialogServiceSelectStatus Status,
	string JavaSource,
	bool IsLive,
	IReadOnlyList<NpcDialogServiceDescriptor> Descriptors,
	bool CallsQuestEngine = false,
	bool SendsDialogWindow = false,
	string? AuditReason = null);

public static class NpcDialogServiceSelectPlanService
{
	private const int UseObject = -1;
	private const int Buy = 2;
	private const int Sell = 3;
	private const int DepositCharWarehouse = 26;
	private const int OpenVendor = 33;
	private const int Recovery = 35;
	private const int AirlineService = 44;
	private const int GatherSkillLevelUp = 45;
	private const int CombineSkillLevelUp = 46;
	private const int ExtendInventory = 47;
	private const int ExtendCharWarehouse = 48;
	private const int OpenLegionWarehouse = 53;
	private const int CloseLegionWarehouse = 56;
	private const int CombineTask = 58;
	private const int ExchangeCoin = 59;
	private const int EditCharacterAll = 61;
	private const int EditCharacterGender = 62;
	private const int MatchMaker = 63;
	private const int InstanceEntry = 65;
	private const int CompoundWeapon = 66;
	private const int DecompoundWeapon = 67;
	private const int FactionJoin = 68;
	private const int FactionSeparate = 69;
	private const int BuyAgain = 70;
	private const int PetAdopt = 71;
	private const int PetAbandon = 72;
	private const int HousingBuild = 73;
	private const int HousingDestruct = 74;
	private const int ChargeItemSingle = 75;
	private const int ChargeItemMulti = 76;
	private const int TradeIn = 78;
	private const int GiveupCraftExpert = 79;
	private const int GiveupCraftMaster = 80;
	private const int HousingPersonalAuction = 84;
	private const int PetHAdopt = 92;
	private const int PetHAbandon = 93;
	private const int ChargeItemSingle2 = 94;
	private const int ChargeItemMulti2 = 95;
	private const int HousingRecreatePersonalInstance = 96;
	private const int TownChallenge = 100;
	private const int TradeSellList = 103;
	private const int OpenInstanceRecruit = 105;
	private const int ItemUpgrade = 109;
	private const int OpenStigmaEnchant = 125;

	public static NpcDialogServiceSelectPlan CreatePlan(NpcDialogServiceSelectInput input)
	{
		// Java parity breadcrumb: services/DialogService.onDialogSelect and
		// handleQuestDialogueOrSendNextPage. This planner is descriptor-only and
		// does not call QuestEngine, PacketSendUtility, TeleportService, or live services.
		var fallback = input.Fallback;
		if (fallback.QuestId != 0 || fallback.DialogActionId is UseObject or ExchangeCoin)
		{
			return QuestEngineOrNextPagePlan(
				fallback,
				"DialogService.handleQuestDialogueOrSendNextPage -> QuestEngine.onDialog then SM_DIALOG_WINDOW");
		}

		return fallback.DialogActionId switch
		{
			Buy => CreateBuyPlan(input),
			DepositCharWarehouse or OpenVendor or OpenStigmaEnchant or CloseLegionWarehouse or CombineTask
				or OpenInstanceRecruit or InstanceEntry or CompoundWeapon or DecompoundWeapon or HousingBuild
				or HousingDestruct or HousingPersonalAuction or ChargeItemSingle or ChargeItemSingle2
				or ItemUpgrade or TownChallenge => CreateDialogWindowPlan(fallback, input.NpcSupportsAction),
			6 => ServicePlan(fallback, NpcDialogServiceDescriptorKind.LegionDisbandRequest, "LegionService.requestDisbandLegion"),
			7 => ServicePlan(fallback, NpcDialogServiceDescriptorKind.LegionRecreate, "LegionService.recreateLegion"),
			Recovery => ServicePlan(fallback, NpcDialogServiceDescriptorKind.ExperienceRecoveryRequest, "DialogService RECOVERY RequestResponseHandler"),
			36 or 37 => ServicePlan(fallback, NpcDialogServiceDescriptorKind.PvpTeleport, "DialogService ENTER_PVP/LEAVE_PVP TeleportService"),
			AirlineService => ServicePlan(fallback, NpcDialogServiceDescriptorKind.AirlineService, "DialogService AIRLINE_SERVICE TeleportService.showMap"),
			GatherSkillLevelUp or CombineSkillLevelUp => ServicePlan(fallback, NpcDialogServiceDescriptorKind.CraftSkillLearn, "CraftSkillUpdateService.learnSkill"),
			ExtendInventory => ServicePlan(fallback, NpcDialogServiceDescriptorKind.CubeExpansion, "CubeExpandService.expandCube"),
			ExtendCharWarehouse => ServicePlan(fallback, NpcDialogServiceDescriptorKind.WarehouseExpansion, "WarehouseService.expandWarehouse"),
			OpenLegionWarehouse => ServicePlan(fallback, NpcDialogServiceDescriptorKind.LegionWarehouseOpen, "LegionService.openLegionWarehouse"),
			EditCharacterAll or EditCharacterGender => ServicePlan(fallback, NpcDialogServiceDescriptorKind.PlasticSurgery, "SM_PLASTIC_SURGERY + setInEditMode"),
			MatchMaker => ServicePlan(fallback, NpcDialogServiceDescriptorKind.AutoGroupOrDialogWindow, "DialogService MATCH_MAKER AutoGroup or SM_DIALOG_WINDOW 1011"),
			FactionJoin => ServicePlan(fallback, NpcDialogServiceDescriptorKind.NpcFactionJoin, "player.getNpcFactions().enterGuild"),
			FactionSeparate => ServicePlan(fallback, NpcDialogServiceDescriptorKind.NpcFactionLeave, "player.getNpcFactions().leaveNpcFaction"),
			BuyAgain => ServicePlan(fallback, NpcDialogServiceDescriptorKind.RepurchasePacket, "SM_REPURCHASE"),
			PetAdopt or PetAbandon or PetHAdopt or PetHAbandon => ServicePlan(fallback, NpcDialogServiceDescriptorKind.PetDialogPacket, "SM_PET"),
			ChargeItemMulti or ChargeItemMulti2 => ServicePlan(fallback, NpcDialogServiceDescriptorKind.ItemChargeRequest, "ItemChargeService.startChargingEquippedItems"),
			TradeIn => CreateTradeInPlan(input),
			Sell or TradeSellList => ServicePlan(fallback, NpcDialogServiceDescriptorKind.SellItemPacket, "SM_SELL_ITEM", NpcDialogServiceSelectStatus.SellItemWindow),
			GiveupCraftExpert or GiveupCraftMaster => ServicePlan(fallback, NpcDialogServiceDescriptorKind.CraftStatusRelinquish, "RelinquishCraftStatus"),
			HousingRecreatePersonalInstance => ServicePlan(fallback, NpcDialogServiceDescriptorKind.StudioRecreate, "HousingService.recreatePlayerStudio"),
			_ => QuestEngineOrNextPagePlan(
				fallback,
				"DialogService.onDialogSelect default -> handleQuestDialogueOrSendNextPage"),
		};
	}

	private static NpcDialogServiceSelectPlan CreateBuyPlan(NpcDialogServiceSelectInput input)
	{
		if (!input.HasTradeList || !input.HasSellableTradeGoods)
		{
			return new NpcDialogServiceSelectPlan(
				NpcDialogServiceSelectStatus.BuyUnavailable,
				"DialogService BUY -> missing TradeListTemplate or no GoodsList allowed by legion level",
				IsLive: false,
				[
					CreateDescriptor(
						input.Fallback,
						NpcDialogServiceDescriptorKind.SystemMessageDoesNotSellItem,
						"SM_SYSTEM_MESSAGE.STR_BUY_SELL_HE_DOES_NOT_SELL_ITEM"),
				]);
		}

		return new NpcDialogServiceSelectPlan(
			NpcDialogServiceSelectStatus.BuyTradeList,
			"DialogService BUY -> SM_TRADELIST with PricesService.getVendorBuyModifier() * tradeModifier / 100",
			IsLive: false,
			[
				CreateDescriptor(
					input.Fallback,
					NpcDialogServiceDescriptorKind.TradeListPacket,
					"SM_TRADELIST",
					priceModifier: input.VendorBuyModifier * input.TradeSellPriceRate / 100,
					tradeListPacketPlan: input.TradeListPacketPlan),
			]);
	}

	private static NpcDialogServiceSelectPlan CreateDialogWindowPlan(
		NpcDialogServiceFallbackDescriptor fallback,
		bool npcSupportsAction)
	{
		if (!npcSupportsAction)
		{
			return new NpcDialogServiceSelectPlan(
				NpcDialogServiceSelectStatus.UnsupportedDialogWindowAction,
				"DialogService.sendDialogWindow -> !NpcTemplate.supportsAction(dialogActionId)",
				IsLive: false,
				Array.Empty<NpcDialogServiceDescriptor>(),
				AuditReason: "Java sendDialogWindow silently skips unsupported action");
		}

		return new NpcDialogServiceSelectPlan(
			NpcDialogServiceSelectStatus.DialogWindow,
			"DialogService.sendDialogWindow -> SM_DIALOG_WINDOW(npc.getObjectId(), DialogPage.getByActionId(dialogActionId).id())",
			IsLive: false,
			[
				CreateDescriptor(
					fallback,
					NpcDialogServiceDescriptorKind.DialogWindowFromAction,
					"SM_DIALOG_WINDOW from DialogPage.getByActionId",
					questId: 0),
			],
			SendsDialogWindow: true);
	}

	private static NpcDialogServiceSelectPlan CreateTradeInPlan(NpcDialogServiceSelectInput input)
	{
		var kind = input.HasTradeInList
			? NpcDialogServiceDescriptorKind.TradeInListPacket
			: NpcDialogServiceDescriptorKind.SystemMessageDoesNotSellItem;
		return new NpcDialogServiceSelectPlan(
			input.HasTradeInList ? NpcDialogServiceSelectStatus.TradeInList : NpcDialogServiceSelectStatus.TradeInUnavailable,
			input.HasTradeInList
				? "DialogService TRADE_IN -> SM_TRADE_IN_LIST"
				: "DialogService TRADE_IN -> missing trade-in list -> STR_BUY_SELL_HE_DOES_NOT_SELL_ITEM",
			IsLive: false,
			[
				CreateDescriptor(
					input.Fallback,
					kind,
					input.HasTradeInList ? "SM_TRADE_IN_LIST" : "SM_SYSTEM_MESSAGE.STR_BUY_SELL_HE_DOES_NOT_SELL_ITEM",
					priceModifier: input.HasTradeInList ? 100 : null),
			]);
	}

	private static NpcDialogServiceSelectPlan QuestEngineOrNextPagePlan(
		NpcDialogServiceFallbackDescriptor fallback,
		string javaSource)
	{
		return new NpcDialogServiceSelectPlan(
			NpcDialogServiceSelectStatus.QuestEngineOrNextPage,
			javaSource,
			IsLive: false,
			[
				CreateDescriptor(
					fallback,
					NpcDialogServiceDescriptorKind.QuestEngineDialog,
					"QuestEngine.getInstance().onDialog(new QuestEnv(...))",
					questId: fallback.QuestId,
					extendedRewardIndex: fallback.ExtendedRewardIndex),
				CreateDescriptor(
					fallback,
					NpcDialogServiceDescriptorKind.DialogWindowNextPage,
					"SM_DIALOG_WINDOW(npc.getObjectId(), dialogActionId, questId)",
					questId: fallback.QuestId),
			],
			CallsQuestEngine: true,
			SendsDialogWindow: true,
			AuditReason: "Dialog window descriptor is conditional on QuestEngine.onDialog returning false");
	}

	private static NpcDialogServiceSelectPlan ServicePlan(
		NpcDialogServiceFallbackDescriptor fallback,
		NpcDialogServiceDescriptorKind descriptorKind,
		string javaSource,
		NpcDialogServiceSelectStatus status = NpcDialogServiceSelectStatus.ServiceDispatch)
	{
		return new NpcDialogServiceSelectPlan(
			status,
			$"DialogService.onDialogSelect -> {javaSource}",
			IsLive: false,
			[
				CreateDescriptor(fallback, descriptorKind, javaSource),
			]);
	}

	private static NpcDialogServiceDescriptor CreateDescriptor(
		NpcDialogServiceFallbackDescriptor fallback,
		NpcDialogServiceDescriptorKind kind,
		string javaSource,
		int? questId = null,
		int? extendedRewardIndex = null,
		int? priceModifier = null,
		SmTradeListPacketPlan? tradeListPacketPlan = null)
	{
		return new NpcDialogServiceDescriptor(
			kind,
			fallback.TargetObjectId,
			fallback.DialogActionId,
			javaSource,
			IsLive: false,
			questId,
			extendedRewardIndex,
			priceModifier,
			tradeListPacketPlan);
	}
}
