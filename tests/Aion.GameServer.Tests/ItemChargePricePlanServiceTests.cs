using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ItemChargePricePlanServiceTests
{
	// --- GetNextChargeLevel ---

	[Fact]
	public void GetNextChargeLevel_BelowLevel1ThresholdIsLevel1()
	{
		Assert.Equal(1, ItemChargePricePlanService.GetNextChargeLevel(0));
		Assert.Equal(1, ItemChargePricePlanService.GetNextChargeLevel(499_999));
	}

	[Fact]
	public void GetNextChargeLevel_AtLevel1ThresholdIsLevel2()
	{
		Assert.Equal(2, ItemChargePricePlanService.GetNextChargeLevel(500_000));
		Assert.Equal(2, ItemChargePricePlanService.GetNextChargeLevel(999_999));
	}

	[Fact]
	public void GetNextChargeLevel_AtMaxIs0()
	{
		Assert.Equal(0, ItemChargePricePlanService.GetNextChargeLevel(1_000_000));
	}

	// --- getPayAmountForService ---

	[Fact]
	public void CreatePlan_ZeroChargeLevel1FullPrice()
	{
		// price1=1000, price2=2000; charge=0, level=1
		// firstLevel = 1000/2 = 500; ratio = 1 - 0/500000 = 1; money = ceil(500 * 1) = 500
		var plan = ItemChargePricePlanService.CreatePlan(1000, 2000, chargeLevel: 1, currentChargePoints: 0);

		Assert.Equal(500L, plan.PayAmount);
		Assert.Equal(500.0, plan.FirstLevel, 6);
	}

	[Fact]
	public void CreatePlan_HalfChargedLevel1HalfPrice()
	{
		// price1=1000; charge=250000 (half of 500000); ratio=1-0.5=0.5; money=ceil(500*0.5)=250
		var plan = ItemChargePricePlanService.CreatePlan(1000, 2000, chargeLevel: 1, currentChargePoints: 250_000);

		Assert.Equal(250L, plan.PayAmount);
	}

	[Fact]
	public void CreatePlan_FullyChargedLevel1ZeroCost()
	{
		// charge=500000 (full level1); ratio=1-1=0; money=0
		var plan = ItemChargePricePlanService.CreatePlan(1000, 2000, chargeLevel: 1, currentChargePoints: 500_000);

		Assert.Equal(0L, plan.PayAmount);
	}

	[Fact]
	public void CreatePlan_Level2FromZeroIncludesBothLevels()
	{
		// price1=1000, price2=2000; charge=0, level=2; nextChargeLevel=1 (from scratch)
		// firstLevel=500; updateLevel=round(500 + 500) = 1000
		// ratio=1; money = ceil(500*1) + 1000 = 500 + 1000 = 1500
		var plan = ItemChargePricePlanService.CreatePlan(1000, 2000, chargeLevel: 2, currentChargePoints: 0);

		Assert.Equal(1500L, plan.PayAmount);
	}

	[Fact]
	public void CreatePlan_Level2FromLevel1OnlyUpdateCost()
	{
		// charge=500000 (already at level 1); nextChargeLevel=2
		// updateLevel=1000; ratio=1-0=1; money=ceil(1000*1)=1000
		var plan = ItemChargePricePlanService.CreatePlan(1000, 2000, chargeLevel: 2, currentChargePoints: 500_000);

		Assert.Equal(1000L, plan.PayAmount);
	}

	[Fact]
	public void Constants_MatchJavaChargeInfo()
	{
		Assert.Equal(500_000, ItemChargePricePlanService.ChargeLevel1Threshold);
		Assert.Equal(1_000_000, ItemChargePricePlanService.ChargeLevel2Threshold);
	}
}
