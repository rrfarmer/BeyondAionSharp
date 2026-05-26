using Aion.GameServer.Configuration;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BrokerRegistrationCommissionPlanServiceTests
{
	[Theory]
	[InlineData(0, 0.02f, 2_000)]
	[InlineData(9, 0.02f, 2_000)]
	[InlineData(10, 0.04f, 4_000)]
	[InlineData(14, 0.04f, 4_000)]
	public void CreatePlan_UsesJavaRegisteredItemCountRate(int registeredItemsCount, float expectedRate, long expectedRawCommission)
	{
		var plan = BrokerRegistrationCommissionPlanService.CreatePlan(
			price: 10_000,
			count: 10,
			registeredItemsCount,
			playerRace: "ELYOS");

		Assert.False(plan.IsLive);
		Assert.Equal(expectedRate, plan.Rate);
		Assert.Equal(expectedRawCommission, plan.RawCommission);
		Assert.Equal(expectedRawCommission, plan.Commission);
		Assert.False(plan.UsesMinimumCommission);
		Assert.Contains("BrokerService.registerItem", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_AppliesJavaMinimumBeforePriceService()
	{
		var plan = BrokerRegistrationCommissionPlanService.CreatePlan(
			price: 100,
			count: 1,
			registeredItemsCount: 0,
			playerRace: "ELYOS",
			priceOptions: new GameServerPriceOptions { DefaultPrices = 200, DefaultModifier = 200, DefaultTaxes = 200 },
			influenceRates: new PriceInfluenceRates(Elyos: 0.5f, Asmodians: 0.5f));

		Assert.Equal(2, plan.RawCommission);
		Assert.True(plan.UsesMinimumCommission);
		Assert.Equal(10, plan.Commission);
	}

	[Fact]
	public void CreatePlan_UsesJavaPricesServiceForNonMinimumCommission()
	{
		var plan = BrokerRegistrationCommissionPlanService.CreatePlan(
			price: 98_765,
			count: 5,
			registeredItemsCount: 12,
			playerRace: "ASMODIANS",
			priceOptions: new GameServerPriceOptions
			{
				DefaultPrices = 100,
				DefaultModifier = 90,
				DefaultTaxes = 100,
				VendorBuyModifier = 125,
				VendorSellModifier = 20,
			},
			influenceRates: new PriceInfluenceRates(Elyos: 0.5f, Asmodians: 0.3f));

		Assert.Equal(19_753, plan.RawCommission);
		Assert.False(plan.UsesMinimumCommission);
		Assert.Equal(20_532, plan.Commission);
	}
}
