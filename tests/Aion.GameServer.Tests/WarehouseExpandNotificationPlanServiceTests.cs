using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WarehouseExpandNotificationPlanServiceTests
{
	[Theory]
	[InlineData(WarehouseExpandType.Npc)]
	[InlineData(WarehouseExpandType.BonusTicket)]
	public void CreatePlan_BothExpandTypesProduceSlotsExtendedNotification(WarehouseExpandType expandType)
	{
		var plan = WarehouseExpandNotificationPlanService.CreatePlan(expandType);

		Assert.Equal(WarehouseExpandNotificationPlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldSendToPlayer);
		Assert.NotNull(plan.ExpandedMessage);
		Assert.Equal(8, plan.SlotsAdded);
		Assert.Equal(expandType, plan.ExpandType);
	}

	[Fact]
	public void SlotsPerExpansionConstantIs8()
	{
		// Java parity: WarehouseService.expand hardcodes STR_EXTEND_CHAR_WAREHOUSE_SIZE_EXTENDED(8)
		Assert.Equal(8, WarehouseExpandNotificationPlanService.SlotsPerExpansion);
	}

	[Fact]
	public void CreatePlan_RecordsDeferredLiveSideEffects()
	{
		var plan = WarehouseExpandNotificationPlanService.CreatePlan(WarehouseExpandType.Npc);

		Assert.Contains("deferred", plan.JavaSource.ToLowerInvariant());
		Assert.Contains("setWarehouseLimit", plan.JavaSource);
	}
}
