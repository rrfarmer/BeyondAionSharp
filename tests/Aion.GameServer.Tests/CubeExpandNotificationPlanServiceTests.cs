using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CubeExpandNotificationPlanServiceTests
{
	[Theory]
	[InlineData(CubeExpandType.Npc)]
	[InlineData(CubeExpandType.Item)]
	[InlineData(CubeExpandType.Quest)]
	public void CreatePlan_AllExpandTypesProduceSlotsExtendedNotification(CubeExpandType expandType)
	{
		var plan = CubeExpandNotificationPlanService.CreatePlan(expandType);

		Assert.Equal(CubeExpandNotificationPlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldSendToPlayer);
		Assert.NotNull(plan.ExpandedMessage);
		Assert.Equal(1300431, plan.ExpandedMessage.MessageId); // InventorySizeExtended
		Assert.Equal(9, plan.SlotsAdded);
		Assert.Equal(expandType, plan.ExpandType);
	}

	[Fact]
	public void CreatePlan_RecordsDeferredLiveSideEffects()
	{
		var plan = CubeExpandNotificationPlanService.CreatePlan(CubeExpandType.Npc);

		Assert.Contains("deferred", plan.JavaSource.ToLowerInvariant());
		Assert.Contains("setCubeLimit", plan.JavaSource);
	}

	[Fact]
	public void SlotsPerExpansionConstantIs9()
	{
		// Java parity: CubeExpandService.expand hardcodes STR_EXTEND_INVENTORY_SIZE_EXTENDED(9)
		Assert.Equal(9, CubeExpandNotificationPlanService.SlotsPerExpansion);
	}
}
