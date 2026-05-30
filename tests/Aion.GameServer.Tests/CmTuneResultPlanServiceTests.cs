using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CmTuneResultPlanServiceTests
{
	[Fact]
	public void CreatePlan_ReturnsNoTargetWhenLookupMisses()
	{
		var plan = CmTuneResultPlanService.CreatePlan(
			targetItem: null,
			targetTemplate: null,
			hasAccepted: true,
			targetItemName: "target");

		Assert.Equal(CmTuneResultPlanStatus.NoTargetItem, plan.Status);
		Assert.Null(plan.ApplicationPlan);
		Assert.Null(plan.ResultingTargetItem);
		Assert.Null(plan.ResponseMessage);
		Assert.Null(plan.InventoryUpdatePacket);
	}

	[Fact]
	public void CreatePlan_AcceptedBranchAuditsMissingPendingButStillSendsApplyYesAndInventoryUpdate()
	{
		var targetItem = CreateItem();
		var targetTemplate = CreateTemplate();

		var plan = CmTuneResultPlanService.CreatePlan(
			targetItem,
			targetTemplate,
			hasAccepted: true,
			targetItemName: "target");

		Assert.Equal(CmTuneResultPlanStatus.AcceptedWithoutPendingResultAudited, plan.Status);
		Assert.NotNull(plan.ApplicationPlan);
		Assert.Equal(TuneResultApplicationPlanStatus.MissingPendingResultAudited, plan.ApplicationPlan!.Status);
		Assert.Same(targetItem, plan.ResultingTargetItem);
		Assert.Equal(1401910, plan.ResponseMessage?.MessageId);
		Assert.NotNull(plan.InventoryUpdatePacket);
		Assert.Equal("attempted to apply a tune result without tuning the item beforehand.", plan.AuditMessage);
	}

	[Fact]
	public void CreatePlan_AcceptedBranchAppliesPendingTuneResultAndBuildsInventoryUpdate()
	{
		var pendingResult = new PendingTuneResult(OptionalSockets: 5, EnchantBonus: 7, StatBonusId: 9, IsAttributeOnly: false);
		var targetItem = CreateItem(optionalSockets: 2, enchantBonus: 3, randomBonus: 4, pendingTuneResult: pendingResult);
		var targetTemplate = CreateTemplate();

		var plan = CmTuneResultPlanService.CreatePlan(
			targetItem,
			targetTemplate,
			hasAccepted: true,
			targetItemName: "target");

		Assert.Equal(CmTuneResultPlanStatus.Accepted, plan.Status);
		Assert.NotNull(plan.ApplicationPlan);
		Assert.Equal(TuneResultApplicationPlanStatus.Applied, plan.ApplicationPlan!.Status);
		Assert.Equal(5, plan.ResultingTargetItem?.OptionalSocket);
		Assert.Equal(7, plan.ResultingTargetItem?.EnchantBonus);
		Assert.Equal(9, plan.ResultingTargetItem?.RandomBonus);
		Assert.Null(plan.ResultingTargetItem?.PendingTuneResult);
		Assert.Equal(1401910, plan.ResponseMessage?.MessageId);
		Assert.IsType<SmInventoryUpdateItem>(plan.InventoryUpdatePacket);
	}

	[Fact]
	public void CreatePlan_AttributeOnlyCancelForcesApplyAndAudits()
	{
		var pendingResult = new PendingTuneResult(OptionalSockets: 5, EnchantBonus: 7, StatBonusId: 9, IsAttributeOnly: true);
		var targetItem = CreateItem(pendingTuneResult: pendingResult);
		var targetTemplate = CreateTemplate();

		var plan = CmTuneResultPlanService.CreatePlan(
			targetItem,
			targetTemplate,
			hasAccepted: false,
			targetItemName: "target");

		Assert.Equal(CmTuneResultPlanStatus.AttributeOnlyCancelForcedApply, plan.Status);
		Assert.NotNull(plan.ApplicationPlan);
		Assert.Equal(TuneResultApplicationPlanStatus.Applied, plan.ApplicationPlan!.Status);
		Assert.Equal("tried to cancel a attribute re-identification which is not possible by default", plan.AuditMessage);
		Assert.Equal(1401910, plan.ResponseMessage?.MessageId);
		Assert.Contains("isAttributeOnly()", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_CancelBranchClearsPreviewAndSendsApplyNo()
	{
		var pendingResult = new PendingTuneResult(OptionalSockets: 5, EnchantBonus: 7, StatBonusId: 9, IsAttributeOnly: false);
		var targetItem = CreateItem(optionalSockets: 2, enchantBonus: 3, randomBonus: 4, pendingTuneResult: pendingResult);
		var targetTemplate = CreateTemplate();

		var plan = CmTuneResultPlanService.CreatePlan(
			targetItem,
			targetTemplate,
			hasAccepted: false,
			targetItemName: "target");

		Assert.Equal(CmTuneResultPlanStatus.Cancelled, plan.Status);
		Assert.Null(plan.ApplicationPlan);
		Assert.NotSame(targetItem, plan.ResultingTargetItem);
		Assert.Null(plan.ResultingTargetItem?.PendingTuneResult);
		Assert.Equal(1401911, plan.ResponseMessage?.MessageId);
		Assert.IsType<SmInventoryUpdateItem>(plan.InventoryUpdatePacket);
		Assert.Contains("STR_MSG_ITEM_REIDENTIFY_APPLY_NO", plan.JavaSource, StringComparison.Ordinal);
	}

	private static InventoryItem CreateItem(
		int optionalSockets = 0,
		int enchantBonus = 0,
		int randomBonus = 0,
		PendingTuneResult? pendingTuneResult = null) =>
		new()
		{
			ObjectId = 1001,
			ItemId = 110100001,
			OwnerId = 9001,
			Location = 0,
			Slot = 1,
			Count = 1,
			TuneCount = 3,
			OptionalSocket = optionalSockets,
			EnchantBonus = enchantBonus,
			RandomBonus = randomBonus,
			PendingTuneResult = pendingTuneResult,
		};

	private static ItemTemplateSummary CreateTemplate() =>
		new(
			110100001,
			"Tac Officer's Sword",
			0,
			1,
			55,
			"SWORD",
			"NORMAL",
			"UNIQUE",
			"PC_ALL",
			1,
			0,
			1);
}
