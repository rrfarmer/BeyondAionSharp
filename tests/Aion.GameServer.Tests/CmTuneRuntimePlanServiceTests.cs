using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CmTuneRuntimePlanServiceTests
{
	[Fact]
	public void CreatePlan_ReturnsNoTargetWhenLookupMisses()
	{
		var plan = CmTuneRuntimePlanService.CreatePlan(
			targetItem: null,
			targetTemplate: null,
			tuningScrollObjectId: 2001,
			tuningScrollItem: CreateItem(objectId: 2001, itemId: 166200000),
			tuningScrollTemplate: CreateScrollTemplate(ItemActionUseTargetType.Weapon, shouldNotReduceTuneCount: false),
			tuningScrollName: "scroll",
			targetItemName: "target");

		Assert.Equal(CmTuneRuntimePlanStatus.NoTargetItem, plan.Status);
		Assert.Null(plan.ResolvedAction);
		Assert.Null(plan.GuardPlan);
	}

	[Fact]
	public void CreatePlan_PrefersIdentifyBranchBeforeScrollHandling()
	{
		var plan = CmTuneRuntimePlanService.CreatePlan(
			targetItem: CreateItem(objectId: 1001, itemId: 110100001, tuneCount: -1),
			targetTemplate: CreateTargetTemplate(),
			tuningScrollObjectId: 2001,
			tuningScrollItem: null,
			tuningScrollTemplate: null,
			tuningScrollName: "scroll",
			targetItemName: "target");

		Assert.Equal(CmTuneRuntimePlanStatus.IdentifyTargetItem, plan.Status);
		Assert.Contains("!item.isIdentified()", plan.JavaSource, StringComparison.Ordinal);
		Assert.Null(plan.ResolvedAction);
		Assert.Null(plan.GuardPlan);
	}

	[Fact]
	public void CreatePlan_AuditsIdentifiedTargetWithoutScroll()
	{
		var plan = CmTuneRuntimePlanService.CreatePlan(
			targetItem: CreateItem(objectId: 1001, itemId: 110100001),
			targetTemplate: CreateTargetTemplate(),
			tuningScrollObjectId: 0,
			tuningScrollItem: null,
			tuningScrollTemplate: null,
			tuningScrollName: "scroll",
			targetItemName: "target");

		Assert.Equal(CmTuneRuntimePlanStatus.AuditAlreadyIdentifiedWithoutScroll, plan.Status);
		Assert.Equal("attempted to tune an already identified item without tuning scroll.", plan.AuditMessage);
		Assert.Null(plan.ResolvedAction);
		Assert.Null(plan.GuardPlan);
	}

	[Fact]
	public void CreatePlan_ReturnsMissingScrollWhenObjectLookupFails()
	{
		var plan = CmTuneRuntimePlanService.CreatePlan(
			targetItem: CreateItem(objectId: 1001, itemId: 110100001),
			targetTemplate: CreateTargetTemplate(),
			tuningScrollObjectId: 2001,
			tuningScrollItem: null,
			tuningScrollTemplate: null,
			tuningScrollName: "scroll",
			targetItemName: "target");

		Assert.Equal(CmTuneRuntimePlanStatus.MissingTuningScroll, plan.Status);
		Assert.Null(plan.ResolvedAction);
		Assert.Null(plan.GuardPlan);
	}

	[Fact]
	public void CreatePlan_ReturnsMissingActionWhenScrollTemplateHasNoTuningMetadata()
	{
		var plan = CmTuneRuntimePlanService.CreatePlan(
			targetItem: CreateItem(objectId: 1001, itemId: 110100001),
			targetTemplate: CreateTargetTemplate(),
			tuningScrollObjectId: 2001,
			tuningScrollItem: CreateItem(objectId: 2001, itemId: 166200000),
			tuningScrollTemplate: new ItemTemplateSummary(166200000, "Plain Scroll", 0, 55, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
			tuningScrollName: "scroll",
			targetItemName: "target");

		Assert.Equal(CmTuneRuntimePlanStatus.MissingTuningAction, plan.Status);
		Assert.Null(plan.ResolvedAction);
		Assert.Null(plan.GuardPlan);
	}

	[Fact]
	public void CreatePlan_UsesGuardPlanWhenResolvedActionCannotAct()
	{
		var plan = CmTuneRuntimePlanService.CreatePlan(
			targetItem: CreateItem(objectId: 1001, itemId: 111100001),
			targetTemplate: CreateTargetTemplate(itemGroup: "CL_TORSO"),
			tuningScrollObjectId: 2001,
			tuningScrollItem: CreateItem(objectId: 2001, itemId: 166200000),
			tuningScrollTemplate: CreateScrollTemplate(ItemActionUseTargetType.Weapon, shouldNotReduceTuneCount: false),
			tuningScrollName: "scroll",
			targetItemName: "target");

		Assert.Equal(CmTuneRuntimePlanStatus.GuardBlocked, plan.Status);
		Assert.NotNull(plan.GuardPlan);
		Assert.Equal(TuningActionGuardPlanStatus.BlockedWrongTargetType, plan.GuardPlan!.Status);
		Assert.Null(plan.ResolvedAction);
	}

	[Fact]
	public void CreatePlan_ResolvesExecutableActionWithLoadedMetadata()
	{
		var targetItem = CreateItem(objectId: 1001, itemId: 110100001, tuneCount: 2);
		var targetTemplate = CreateTargetTemplate(maxTuneCount: 6, maxEnchantBonus: 5, optionSlotBonus: 3);
		var scrollItem = CreateItem(objectId: 2001, itemId: 166200001);
		var scrollTemplate = CreateScrollTemplate(ItemActionUseTargetType.Weapon, shouldNotReduceTuneCount: true);

		var plan = CmTuneRuntimePlanService.CreatePlan(
			targetItem,
			targetTemplate,
			tuningScrollObjectId: scrollItem.ObjectId,
			tuningScrollItem: scrollItem,
			tuningScrollTemplate: scrollTemplate,
			tuningScrollName: "scroll",
			targetItemName: "target");

		Assert.Equal(CmTuneRuntimePlanStatus.ExecuteTuning, plan.Status);
		Assert.NotNull(plan.ResolvedAction);
		Assert.NotNull(plan.GuardPlan);
		Assert.True(plan.GuardPlan!.CanAct);
		Assert.Equal(TuningActionTargetType.Weapon, plan.ResolvedAction!.TargetType);
		Assert.True(plan.ResolvedAction.ShouldNotReduceTuneCount);
		Assert.Same(targetItem, plan.ResolvedAction.TargetItem);
		Assert.Same(targetTemplate, plan.ResolvedAction.TargetTemplate);
		Assert.Same(scrollItem, plan.ResolvedAction.TuningScrollItem);
		Assert.Same(scrollTemplate, plan.ResolvedAction.TuningScrollTemplate);
	}

	private static InventoryItem CreateItem(int objectId, int itemId, int tuneCount = 0) =>
		new()
		{
			ObjectId = objectId,
			ItemId = itemId,
			OwnerId = 9001,
			Location = 0,
			Slot = 1,
			Count = 1,
			TuneCount = tuneCount,
		};

	private static ItemTemplateSummary CreateTargetTemplate(
		string itemGroup = "SWORD",
		int maxTuneCount = 6,
		int maxEnchantBonus = 0,
		int optionSlotBonus = 0) =>
		new(
			110100001,
			"Target",
			0,
			50,
			1,
			itemGroup,
			"NORMAL",
			"COMMON",
			"PC_ALL",
			1,
			0,
			0,
			CanTune: true,
			MaxTuneCount: maxTuneCount,
			MaxEnchantBonus: maxEnchantBonus,
			OptionSlotBonus: optionSlotBonus);

	private static ItemTemplateSummary CreateScrollTemplate(ItemActionUseTargetType targetType, bool shouldNotReduceTuneCount) =>
		new(
			166200000,
			"Scroll",
			0,
			55,
			1,
			"NONE",
			"NORMAL",
			"COMMON",
			"PC_ALL",
			1,
			0,
			0,
			TuningAction: new ItemTuningActionInfo(targetType, shouldNotReduceTuneCount));
}
