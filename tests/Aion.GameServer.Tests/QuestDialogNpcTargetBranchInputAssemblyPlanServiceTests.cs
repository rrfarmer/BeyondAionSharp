using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestDialogNpcTargetBranchInputAssemblyPlanServiceTests
{
	private const int PlayerObjectId = 42;
	private const int TargetObjectId = 9001;
	private const int FunctionDialogAction = 33;
	private const int BuyAction = 2;

	[Fact]
	public void CreatePlan_DerivesFunctionDialogFromGlobalNpcTemplateTable()
	{
		var targetTemplate = CreateTemplate(203001);
		var templates = new NpcTemplateTable([targetTemplate, CreateTemplate(203002, [FunctionDialogAction])]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(targetTemplate),
			templates);

		Assert.True(plan.Input.IsFunctionDialog);
		Assert.False(plan.Input.NpcSupportsAction);
		Assert.Equal(QuestDialogNpcTargetBranchStatus.UnsupportedFunctionAction, plan.BranchPlan.Status);
		Assert.Null(plan.BranchPlan.Dispatch);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_DerivesUnknownDialogActionFromRegistryBeforeTargetBranching()
	{
		var targetTemplate = CreateTemplate(203001, [FunctionDialogAction]);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(targetTemplate, dialogActionId: 102),
			templates);

		Assert.False(plan.DialogActionName.IsKnown);
		Assert.False(plan.Input.DialogActionKnown);
		Assert.Equal(QuestDialogNpcTargetBranchStatus.UnknownDialogAction, plan.BranchPlan.Status);
		Assert.Null(plan.BranchPlan.Dispatch);
	}

	[Fact]
	public void CreatePlan_DerivesGeneratedSelectRangeAsKnownDialogAction()
	{
		var targetTemplate = CreateTemplate(203001);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(targetTemplate, dialogActionId: 1011),
			templates);

		Assert.True(plan.DialogActionName.IsKnown);
		Assert.True(plan.DialogActionName.NameIsExact);
		Assert.Equal("SELECT1", plan.DialogActionName.Name);
		Assert.True(plan.Input.DialogActionKnown);
	}

	[Fact]
	public void CreatePlan_DerivesNpcSupportFromTargetTemplate()
	{
		var targetTemplate = CreateTemplate(203001, [FunctionDialogAction]);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(targetTemplate),
			templates);

		Assert.True(plan.Input.IsFunctionDialog);
		Assert.True(plan.Input.NpcSupportsAction);
		Assert.Equal(QuestDialogNpcTargetBranchStatus.DispatchController, plan.BranchPlan.Status);
		Assert.NotNull(plan.BranchPlan.Dispatch);
	}

	[Fact]
	public void CreatePlan_KeepsInteractionAllowedAsExplicitDependency()
	{
		var targetTemplate = CreateTemplate(203001, [FunctionDialogAction]);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(targetTemplate, interactionAllowed: false),
			templates);

		Assert.True(plan.Input.IsFunctionDialog);
		Assert.True(plan.Input.NpcSupportsAction);
		Assert.False(plan.Input.InteractionAllowed);
		Assert.Equal(QuestDialogNpcTargetBranchStatus.InteractionNotAllowed, plan.BranchPlan.Status);
	}

	[Fact]
	public void CreatePlan_UsesInteractionPlanWhenProvided()
	{
		var targetTemplate = CreateTemplate(203001, [FunctionDialogAction]);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(
				targetTemplate,
				interactionAllowed: true,
				interactionInput: new NpcDialogInteractionAllowedInput(
					PlayerObjectId,
					SubDialogType: NpcSubDialogType.Level,
					SubDialogValue: 50,
					PlayerLevel: 49)),
			templates);

		Assert.NotNull(plan.InteractionPlan);
		Assert.False(plan.InteractionPlan.IsAllowed);
		Assert.False(plan.Input.InteractionAllowed);
		Assert.Equal(QuestDialogNpcTargetBranchStatus.InteractionNotAllowed, plan.BranchPlan.Status);
		Assert.Equal("tried to illegally use dialog action", plan.BranchPlan.AuditReason);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_AllowsWhenInteractionPlanAllows()
	{
		var targetTemplate = CreateTemplate(203001, [FunctionDialogAction]);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(
				targetTemplate,
				interactionAllowed: false,
				interactionInput: new NpcDialogInteractionAllowedInput(
					PlayerObjectId,
					SubDialogType: NpcSubDialogType.Level,
					SubDialogValue: 50,
					PlayerLevel: 50)),
			templates);

		Assert.NotNull(plan.InteractionPlan);
		Assert.True(plan.InteractionPlan.IsAllowed);
		Assert.True(plan.Input.InteractionAllowed);
		Assert.Equal(QuestDialogNpcTargetBranchStatus.DispatchController, plan.BranchPlan.Status);
	}

	[Fact]
	public void CreatePlan_ComposesControllerDispatchPlanWhenBranchDispatches()
	{
		var targetTemplate = CreateTemplate(203001, [FunctionDialogAction]);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(
				targetTemplate,
				controllerDispatchFacts: new QuestDialogNpcControllerDispatchFacts(
					IsInTalkRange: true,
					NpcAiHandledDialogSelect: false)),
			templates);

		Assert.Equal(QuestDialogNpcTargetBranchStatus.DispatchController, plan.BranchPlan.Status);
		var controllerPlan = Assert.IsType<NpcDialogControllerDispatchPlan>(plan.ControllerDispatchPlan);
		Assert.Equal(NpcDialogControllerDispatchStatus.DialogServiceFallback, controllerPlan.Status);
		Assert.True(controllerPlan.CallsNpcAi);
		Assert.True(controllerPlan.CallsDialogService);
		Assert.Equal(TargetObjectId, controllerPlan.Dispatch.TargetObjectId);
		Assert.NotNull(controllerPlan.DialogServiceFallback);
	}

	[Fact]
	public void CreatePlan_ComposesDialogServicePlanThroughControllerDispatchFacts()
	{
		var targetTemplate = CreateTemplate(203001, [BuyAction]);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(
				targetTemplate,
				questId: 0,
				dialogActionId: BuyAction,
				controllerDispatchFacts: new QuestDialogNpcControllerDispatchFacts(
					IsInTalkRange: true,
					NpcAiHandledDialogSelect: false,
					DialogServiceFacts: new NpcDialogServiceSelectFacts(
						NpcSupportsAction: true,
						HasTradeList: true,
						HasSellableTradeGoods: true,
						VendorBuyModifier: 125,
						TradeSellPriceRate: 80))),
			templates);

		var controllerPlan = Assert.IsType<NpcDialogControllerDispatchPlan>(plan.ControllerDispatchPlan);
		Assert.Equal(NpcDialogControllerDispatchStatus.DialogServiceFallback, controllerPlan.Status);
		var servicePlan = Assert.IsType<NpcDialogServiceSelectPlan>(controllerPlan.DialogServicePlan);
		Assert.Equal(NpcDialogServiceSelectStatus.BuyTradeList, servicePlan.Status);
		var descriptor = Assert.Single(servicePlan.Descriptors);
		Assert.Equal(NpcDialogServiceDescriptorKind.TradeListPacket, descriptor.Kind);
		Assert.Equal(TargetObjectId, descriptor.TargetObjectId);
		Assert.Equal(BuyAction, descriptor.DialogActionId);
		Assert.Equal(100, descriptor.PriceModifier);
	}

	[Fact]
	public void CreatePlan_ComposesDialogServiceFactsFromStaticTradeData()
	{
		var targetTemplate = CreateTemplate(203060, [BuyAction]);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(
				targetTemplate,
				questId: 0,
				dialogActionId: BuyAction,
				controllerDispatchFacts: new QuestDialogNpcControllerDispatchFacts(
					IsInTalkRange: true,
					NpcAiHandledDialogSelect: false),
				tradeListFactInput: new NpcDialogTradeListFactAdapterInput(
					NpcId: 203060,
					PlayerLegionLevel: 0,
					VendorBuyModifier: 125),
				limitedItemFactInput: new NpcDialogLimitedItemFactAdapterInput(
					NpcId: 203060,
					PlayerObjectId: PlayerObjectId,
					PlayerBuyCountsByItemId: new Dictionary<int, int> { [186000001] = 2 })),
			templates,
			CreateTradeLists(tradeLists: [new TradeListTemplateSummary(203060, [129], SellPriceRate: 80)]),
			CreateGoodsLists(new GoodsListSummary(
				129,
				SalesTime: "0 0 9 ? * MON",
				Items:
				[
					new GoodsListItemSummary(110100010),
					new GoodsListItemSummary(186000001, SellLimit: 5, BuyLimit: 3),
				])));

		var tradeListFactPlan = Assert.IsType<NpcDialogTradeListFactAdapterPlan>(plan.TradeListFactAdapterPlan);
		Assert.True(tradeListFactPlan.Facts.HasTradeList);
		Assert.True(tradeListFactPlan.Facts.HasSellableTradeGoods);
		var limitedItemFactPlan = Assert.IsType<NpcDialogLimitedItemFactAdapterPlan>(plan.LimitedItemFactAdapterPlan);
		Assert.Equal(
			[
				new SmTradeListLimitedItemSummary(186000001, BuyCount: 2, SellLimit: 5),
			],
			limitedItemFactPlan.PacketItems);
		var packetPlan = Assert.IsType<SmTradeListPacketPlan>(plan.TradeListPacketPlan);
		Assert.Equal(SmTradeListPacketPlanStatus.Ready, packetPlan.Status);
		Assert.Equal([129], packetPlan.TradeTabIds);
		Assert.Equal(limitedItemFactPlan.PacketItems, packetPlan.LimitedItems);
		Assert.Equal(100, packetPlan.BuyPriceModifier);
		Assert.True(packetPlan.ShowBuyTab);
		Assert.True(packetPlan.ShowSellTab);
		Assert.False(packetPlan.IsLive);
		var controllerPlan = Assert.IsType<NpcDialogControllerDispatchPlan>(plan.ControllerDispatchPlan);
		var servicePlan = Assert.IsType<NpcDialogServiceSelectPlan>(controllerPlan.DialogServicePlan);
		Assert.Equal(NpcDialogServiceSelectStatus.BuyTradeList, servicePlan.Status);
		var descriptor = Assert.Single(servicePlan.Descriptors);
		Assert.Equal(100, descriptor.PriceModifier);
		Assert.Same(packetPlan, descriptor.TradeListPacketPlan);
	}

	[Fact]
	public void CreatePlan_DoesNotCreateTradeListPacketPlanWhenNoGoodsAreSellable()
	{
		var targetTemplate = CreateTemplate(203060, [BuyAction]);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(
				targetTemplate,
				questId: 0,
				dialogActionId: BuyAction,
				controllerDispatchFacts: new QuestDialogNpcControllerDispatchFacts(
					IsInTalkRange: true,
					NpcAiHandledDialogSelect: false),
				tradeListFactInput: new NpcDialogTradeListFactAdapterInput(
					NpcId: 203060,
					PlayerLegionLevel: 0,
					VendorBuyModifier: 125)),
			templates,
			CreateTradeLists(tradeLists: [new TradeListTemplateSummary(203060, [129], SellPriceRate: 80)]),
			CreateGoodsLists(new GoodsListSummary(129, LegionLevel: 5)));

		var tradeListFactPlan = Assert.IsType<NpcDialogTradeListFactAdapterPlan>(plan.TradeListFactAdapterPlan);
		Assert.False(tradeListFactPlan.Facts.HasSellableTradeGoods);
		Assert.Null(plan.TradeListPacketPlan);
		var controllerPlan = Assert.IsType<NpcDialogControllerDispatchPlan>(plan.ControllerDispatchPlan);
		var servicePlan = Assert.IsType<NpcDialogServiceSelectPlan>(controllerPlan.DialogServicePlan);
		Assert.Equal(NpcDialogServiceSelectStatus.BuyUnavailable, servicePlan.Status);
		Assert.Equal(NpcDialogServiceDescriptorKind.SystemMessageDoesNotSellItem, Assert.Single(servicePlan.Descriptors).Kind);
	}

	[Fact]
	public void CreatePlan_PrefersExplicitDialogServiceFactsOverStaticTradeData()
	{
		var targetTemplate = CreateTemplate(203060, [BuyAction]);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(
				targetTemplate,
				questId: 0,
				dialogActionId: BuyAction,
				controllerDispatchFacts: new QuestDialogNpcControllerDispatchFacts(
					IsInTalkRange: true,
					NpcAiHandledDialogSelect: false,
					DialogServiceFacts: new NpcDialogServiceSelectFacts(
						HasTradeList: false,
						HasSellableTradeGoods: false)),
				tradeListFactInput: new NpcDialogTradeListFactAdapterInput(203060)),
			templates,
			CreateTradeLists(tradeLists: [new TradeListTemplateSummary(203060, [129])]),
			CreateGoodsLists(new GoodsListSummary(129)));

		Assert.Null(plan.TradeListFactAdapterPlan);
		Assert.Null(plan.TradeListPacketPlan);
		var controllerPlan = Assert.IsType<NpcDialogControllerDispatchPlan>(plan.ControllerDispatchPlan);
		var servicePlan = Assert.IsType<NpcDialogServiceSelectPlan>(controllerPlan.DialogServicePlan);
		Assert.Equal(NpcDialogServiceSelectStatus.BuyUnavailable, servicePlan.Status);
	}

	[Fact]
	public void CreatePlan_DoesNotDeriveStaticTradeFactsWhenControllerShortCircuits()
	{
		var targetTemplate = CreateTemplate(203060, [BuyAction]);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(
				targetTemplate,
				questId: 0,
				dialogActionId: BuyAction,
				controllerDispatchFacts: new QuestDialogNpcControllerDispatchFacts(
					IsInTalkRange: true,
					NpcAiHandledDialogSelect: true),
				tradeListFactInput: new NpcDialogTradeListFactAdapterInput(203060)),
			templates,
			CreateTradeLists(tradeLists: [new TradeListTemplateSummary(203060, [129])]),
			CreateGoodsLists(new GoodsListSummary(129)));

		Assert.Null(plan.TradeListFactAdapterPlan);
		Assert.Null(plan.TradeListPacketPlan);
		var controllerPlan = Assert.IsType<NpcDialogControllerDispatchPlan>(plan.ControllerDispatchPlan);
		Assert.Equal(NpcDialogControllerDispatchStatus.AiHandled, controllerPlan.Status);
		Assert.Null(controllerPlan.DialogServicePlan);
	}

	[Fact]
	public void CreatePlan_DoesNotComposeDialogServicePlanWhenControllerShortCircuits()
	{
		var targetTemplate = CreateTemplate(203001, [FunctionDialogAction]);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(
				targetTemplate,
				controllerDispatchFacts: new QuestDialogNpcControllerDispatchFacts(
					IsInTalkRange: true,
					NpcAiHandledDialogSelect: true,
					DialogServiceFacts: new NpcDialogServiceSelectFacts(
						HasTradeList: true,
						HasSellableTradeGoods: true))),
			templates);

		var controllerPlan = Assert.IsType<NpcDialogControllerDispatchPlan>(plan.ControllerDispatchPlan);
		Assert.Equal(NpcDialogControllerDispatchStatus.AiHandled, controllerPlan.Status);
		Assert.Null(controllerPlan.DialogServiceFallback);
		Assert.Null(controllerPlan.DialogServicePlan);
	}

	[Fact]
	public void CreatePlan_UsesControllerDispatchFactsAfterBranchDispatch()
	{
		var targetTemplate = CreateTemplate(203001, [FunctionDialogAction]);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(
				targetTemplate,
				controllerDispatchFacts: new QuestDialogNpcControllerDispatchFacts(
					IsInTalkRange: false,
					NpcAiHandledDialogSelect: true)),
			templates);

		var controllerPlan = Assert.IsType<NpcDialogControllerDispatchPlan>(plan.ControllerDispatchPlan);
		Assert.Equal(NpcDialogControllerDispatchStatus.OutOfTalkRange, controllerPlan.Status);
		Assert.False(controllerPlan.CallsNpcAi);
		Assert.False(controllerPlan.CallsDialogService);
	}

	[Fact]
	public void CreatePlan_DoesNotComposeControllerDispatchWhenBranchIsBlocked()
	{
		var targetTemplate = CreateTemplate(203001, [FunctionDialogAction]);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(
				targetTemplate,
				interactionAllowed: false,
				controllerDispatchFacts: new QuestDialogNpcControllerDispatchFacts()),
			templates);

		Assert.Equal(QuestDialogNpcTargetBranchStatus.InteractionNotAllowed, plan.BranchPlan.Status);
		Assert.Null(plan.BranchPlan.Dispatch);
		Assert.Null(plan.ControllerDispatchPlan);
	}

	[Fact]
	public void CreatePlan_KeepsControllerDispatchOptional()
	{
		var targetTemplate = CreateTemplate(203001, [FunctionDialogAction]);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(targetTemplate),
			templates);

		Assert.Equal(QuestDialogNpcTargetBranchStatus.DispatchController, plan.BranchPlan.Status);
		Assert.NotNull(plan.BranchPlan.Dispatch);
		Assert.Null(plan.ControllerDispatchPlan);
	}

	[Fact]
	public void CreatePlan_DoesNotApplyNpcSupportGuardToNonNpcCreatures()
	{
		var templates = new NpcTemplateTable([CreateTemplate(203001, [FunctionDialogAction])]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			new QuestDialogNpcTargetBranchRuntimeSnapshot(
				PlayerObjectId,
				TargetObjectId,
				FunctionDialogAction,
				LastPage: 7,
				QuestId: 1001,
				ExtendedRewardIndex: 3,
				TargetExists: true,
				TargetIsCreature: true,
				TargetIsNpc: false,
				InteractionAllowed: false),
			templates);

		Assert.True(plan.Input.IsFunctionDialog);
		Assert.False(plan.Input.NpcSupportsAction);
		Assert.Equal(QuestDialogNpcTargetBranchStatus.DispatchController, plan.BranchPlan.Status);
		Assert.NotNull(plan.BranchPlan.Dispatch);
	}

	private static QuestDialogNpcTargetBranchRuntimeSnapshot CreateSnapshot(
		NpcTemplateSummary targetTemplate,
		bool interactionAllowed = true,
		NpcDialogInteractionAllowedInput? interactionInput = null,
		QuestDialogNpcControllerDispatchFacts? controllerDispatchFacts = null,
		int questId = 1001,
		int dialogActionId = FunctionDialogAction,
		NpcDialogTradeListFactAdapterInput? tradeListFactInput = null,
		NpcDialogLimitedItemFactAdapterInput? limitedItemFactInput = null)
	{
		return new QuestDialogNpcTargetBranchRuntimeSnapshot(
			PlayerObjectId,
			TargetObjectId,
			dialogActionId,
			LastPage: 7,
			QuestId: questId,
			ExtendedRewardIndex: 3,
			TargetExists: true,
			TargetIsCreature: true,
			TargetIsNpc: true,
			TargetNpcTemplate: targetTemplate,
			InteractionAllowed: interactionAllowed,
			InteractionInput: interactionInput,
			ControllerDispatchFacts: controllerDispatchFacts,
			TradeListFactInput: tradeListFactInput,
			LimitedItemFactInput: limitedItemFactInput);
	}

	private static NpcTemplateSummary CreateTemplate(int templateId, IReadOnlyList<int>? functionDialogIds = null)
	{
		return new NpcTemplateSummary(
			templateId,
			$"npc_{templateId}",
			NameId: 0,
			Level: 1,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "NONE",
			Tribe: "NONE",
			Type: "NPC",
			FunctionDialogIds: functionDialogIds);
	}

	private static TradeListTable CreateTradeLists(
		IReadOnlyList<TradeListTemplateSummary>? tradeLists = null,
		IReadOnlyList<TradeListTemplateSummary>? tradeInLists = null,
		IReadOnlyList<TradeListTemplateSummary>? purchaseLists = null)
	{
		return new TradeListTable(
			tradeLists ?? Array.Empty<TradeListTemplateSummary>(),
			tradeInLists ?? Array.Empty<TradeListTemplateSummary>(),
			purchaseLists ?? Array.Empty<TradeListTemplateSummary>());
	}

	private static GoodsListTable CreateGoodsLists(params GoodsListSummary[] goodsLists)
	{
		return new GoodsListTable(goodsLists, Array.Empty<GoodsListSummary>(), Array.Empty<GoodsListSummary>());
	}
}
