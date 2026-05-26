using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class NpcDialogTradeRuntimeFactAdapterServiceTests
{
	[Fact]
	public void CreatePlan_UsesJavaNoLegionAndDefaultVendorModifierAsExplicitStagedFacts()
	{
		var plan = NpcDialogTradeRuntimeFactAdapterService.CreatePlan(
			new NpcDialogTradeRuntimeFactAdapterInput(
				PlayerObjectId: 42,
				PlayerLegionId: 77));

		Assert.Equal(42, plan.PlayerObjectId);
		Assert.Equal(77, plan.PlayerLegionId);
		Assert.Equal(0, plan.PlayerLegionLevel);
		Assert.Equal(100, plan.VendorBuyModifier);
		Assert.Contains("Player.getLegion()", plan.JavaSource);
		Assert.Contains("PricesService.getVendorBuyModifier", plan.JavaSource);
		Assert.Contains("Staged default", plan.LegionLevelSource);
		Assert.Contains("Staged default", plan.VendorBuyModifierSource);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_CanCarryInjectedRuntimeFactsWithoutMarkingLiveLookupComplete()
	{
		var plan = NpcDialogTradeRuntimeFactAdapterService.CreatePlan(
			new NpcDialogTradeRuntimeFactAdapterInput(
				PlayerObjectId: 42,
				PlayerLegionId: 77,
				PlayerLegionLevel: 5,
				VendorBuyModifier: 125));

		var tradeInput = plan.ToTradeListFactInput(npcId: 203060);
		var limitedInput = plan.ToLimitedItemFactInput(npcId: 203060);

		Assert.Equal(5, plan.PlayerLegionLevel);
		Assert.Equal(125, plan.VendorBuyModifier);
		Assert.Equal("Injected runtime value", plan.LegionLevelSource);
		Assert.Equal("Injected runtime value", plan.VendorBuyModifierSource);
		Assert.Equal(203060, tradeInput.NpcId);
		Assert.Equal(5, tradeInput.PlayerLegionLevel);
		Assert.Equal(125, tradeInput.VendorBuyModifier);
		Assert.Equal(203060, limitedInput.NpcId);
		Assert.Equal(42, limitedInput.PlayerObjectId);
		Assert.False(plan.IsLive);
	}
}
