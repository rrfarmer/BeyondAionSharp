using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class TuningActionGuardPlanServiceTests
{
	private const int TuningScrollItemId = 166030001;
	private const int WeaponItemId = 100000001;
	private const int ArmorItemId = 110000001;
	private const int UntunableItemId = 182400001;

	[Fact]
	public void CreatePlan_AllowsJavaHappyPath()
	{
		var plan = TuningActionGuardPlanService.CreatePlan(
			CreateItem(2001),
			CreateTuningScrollTemplate(level: 60),
			CreateItem(1001, tuneCount: 0),
			CreateTargetTemplate(WeaponItemId, level: 55, itemGroup: "SWORD", canTune: true, maxTuneCount: 2),
			TuningActionTargetType.Weapon,
			shouldNotReduceTuneCount: false,
			tuningScrollName: "Fine Tuning Scroll",
			targetItemName: "Tac Officer's Sword");

		Assert.Equal(TuningActionGuardPlanStatus.Allowed, plan.Status);
		Assert.True(plan.CanAct);
		Assert.Null(plan.DenialMessage);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_FollowsJavaGuardOrder()
	{
		var plan = TuningActionGuardPlanService.CreatePlan(
			CreateItem(2001),
			CreateTuningScrollTemplate(level: 40),
			CreateItem(1001, isEquipped: true, tuneCount: -1),
			CreateTargetTemplate(UntunableItemId, level: 80, itemGroup: "NONE", canTune: false, maxTuneCount: 0),
			TuningActionTargetType.Weapon,
			shouldNotReduceTuneCount: false,
			tuningScrollName: "Fine Tuning Scroll",
			targetItemName: "Unknown Target");

		Assert.Equal(TuningActionGuardPlanStatus.BlockedEquippedTarget, plan.Status);
		Assert.False(plan.CanAct);
		Assert.Null(plan.DenialMessage);
		Assert.Contains("isEquipped", plan.JavaSource);
	}

	[Theory]
	[InlineData(TuningActionGuardPlanStatus.BlockedUnidentifiedTarget, 1401637)]
	[InlineData(TuningActionGuardPlanStatus.BlockedUntunableTarget, 1401636)]
	public void CreatePlan_UsesJavaSystemMessagesForTargetStateFailures(TuningActionGuardPlanStatus expectedStatus, int expectedMessageId)
	{
		var targetItem = expectedStatus == TuningActionGuardPlanStatus.BlockedUnidentifiedTarget
			? CreateItem(1001, tuneCount: -1)
			: CreateItem(1001, tuneCount: 0);
		var targetTemplate = expectedStatus == TuningActionGuardPlanStatus.BlockedUnidentifiedTarget
			? CreateTargetTemplate(WeaponItemId, level: 55, itemGroup: "SWORD", canTune: true, maxTuneCount: 2)
			: CreateTargetTemplate(UntunableItemId, level: 55, itemGroup: "SWORD", canTune: false, maxTuneCount: 0);

		var plan = TuningActionGuardPlanService.CreatePlan(
			CreateItem(2001),
			CreateTuningScrollTemplate(level: 60),
			targetItem,
			targetTemplate,
			TuningActionTargetType.Weapon,
			shouldNotReduceTuneCount: false,
			tuningScrollName: "Fine Tuning Scroll",
			targetItemName: "Tac Officer's Sword");

		Assert.Equal(expectedStatus, plan.Status);
		Assert.False(plan.CanAct);
		Assert.Equal(expectedMessageId, plan.DenialMessage?.MessageId);
	}

	[Fact]
	public void CreatePlan_UsesJavaWrongSelectMessageForTargetMismatch()
	{
		var plan = TuningActionGuardPlanService.CreatePlan(
			CreateItem(2001),
			CreateTuningScrollTemplate(level: 60),
			CreateItem(1001, tuneCount: 0),
			CreateTargetTemplate(ArmorItemId, level: 55, itemGroup: "LT_TORSO", canTune: true, maxTuneCount: 2),
			TuningActionTargetType.Weapon,
			shouldNotReduceTuneCount: false,
			tuningScrollName: "Fine Tuning Scroll",
			targetItemName: "Leather Tunic");

		Assert.Equal(TuningActionGuardPlanStatus.BlockedWrongTargetType, plan.Status);
		Assert.False(plan.CanAct);
		Assert.Equal(1401633, plan.DenialMessage?.MessageId);
	}

	[Fact]
	public void CreatePlan_UsesJavaWrongLevelMessageForHigherLevelTarget()
	{
		var plan = TuningActionGuardPlanService.CreatePlan(
			CreateItem(2001),
			CreateTuningScrollTemplate(level: 50),
			CreateItem(1001, tuneCount: 0),
			CreateTargetTemplate(WeaponItemId, level: 55, itemGroup: "SWORD", canTune: true, maxTuneCount: 2),
			TuningActionTargetType.Weapon,
			shouldNotReduceTuneCount: false,
			tuningScrollName: "Fine Tuning Scroll",
			targetItemName: "Tac Officer's Sword");

		Assert.Equal(TuningActionGuardPlanStatus.BlockedHigherTargetLevel, plan.Status);
		Assert.False(plan.CanAct);
		Assert.Equal(1401635, plan.DenialMessage?.MessageId);
	}

	[Fact]
	public void CreatePlan_FinalTuneCountGuardIsSilentLikeJava()
	{
		var plan = TuningActionGuardPlanService.CreatePlan(
			CreateItem(2001),
			CreateTuningScrollTemplate(level: 60),
			CreateItem(1001, tuneCount: 2),
			CreateTargetTemplate(WeaponItemId, level: 55, itemGroup: "SWORD", canTune: true, maxTuneCount: 2),
			TuningActionTargetType.Weapon,
			shouldNotReduceTuneCount: false,
			tuningScrollName: "Fine Tuning Scroll",
			targetItemName: "Tac Officer's Sword");

		Assert.Equal(TuningActionGuardPlanStatus.BlockedMaxTuneCount, plan.Status);
		Assert.False(plan.CanAct);
		Assert.Null(plan.DenialMessage);
	}

	[Fact]
	public void CreatePlan_ShouldNotReduceTuneCountBypassesJavaFinalGuard()
	{
		var plan = TuningActionGuardPlanService.CreatePlan(
			CreateItem(2001),
			CreateTuningScrollTemplate(level: 60),
			CreateItem(1001, tuneCount: 2),
			CreateTargetTemplate(WeaponItemId, level: 55, itemGroup: "SWORD", canTune: true, maxTuneCount: 2),
			TuningActionTargetType.Weapon,
			shouldNotReduceTuneCount: true,
			tuningScrollName: "Fine Tuning Scroll",
			targetItemName: "Tac Officer's Sword");

		Assert.Equal(TuningActionGuardPlanStatus.Allowed, plan.Status);
		Assert.True(plan.CanAct);
		Assert.Contains("bypasses final tune-count guard", plan.JavaSource);
	}

	private static InventoryItem CreateItem(int objectId, bool isEquipped = false, int tuneCount = 0)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = WeaponItemId,
			Location = 0,
			Count = 1,
			IsEquipped = isEquipped,
			TuneCount = tuneCount,
		};
	}

	private static ItemTemplateSummary CreateTuningScrollTemplate(int level)
	{
		return new ItemTemplateSummary(TuningScrollItemId, "Fine Tuning Scroll", 0, 0, level, "NONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0);
	}

	private static ItemTemplateSummary CreateTargetTemplate(int itemId, int level, string itemGroup, bool canTune, int maxTuneCount)
	{
		return new ItemTemplateSummary(itemId, $"Target {itemId}", 0, 1, level, itemGroup, "NORMAL", "UNIQUE", "PC_ALL", 1, 0, 1, CanTune: canTune, MaxTuneCount: maxTuneCount);
	}
}
