using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CriticalRatePlanServiceTests
{
	// --- Physical critical rate ---

	[Fact]
	public void CreatePlan_PhysicalBasicCritRate()
	{
		// attackerCrit=300, defenderCR=200, prob=100 → base=100, no adjustment, effective=min(500,100)=100
		var plan = CriticalRatePlanService.CreatePlan(CriticalType.Physical, 300f, 200f, 100);

		Assert.Equal(100f, plan.BaseCriticalRate, 6);
		Assert.Equal(100f, plan.AdjustedCriticalRate, 6);
		Assert.Equal(100f, plan.EffectiveCriticalRate, 6);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_PhysicalCritProbReducesCritRate()
	{
		// base=400, critProb=50, base>0 → adjusted=400*50/100=200; effective=min(500,200)=200
		var plan = CriticalRatePlanService.CreatePlan(CriticalType.Physical, 600f, 200f, 50);

		Assert.Equal(200f, plan.AdjustedCriticalRate, 6);
	}

	[Fact]
	public void CreatePlan_PhysicalNegativeBaseNotAdjustedByProb()
	{
		// base=-100, critProb=50, base <= 0 → no adjustment; effective=min(500,-100)=-100
		var plan = CriticalRatePlanService.CreatePlan(CriticalType.Physical, 100f, 200f, 50);

		Assert.Equal(-100f, plan.BaseCriticalRate, 6);
		Assert.Equal(-100f, plan.AdjustedCriticalRate, 6); // Java: criticalProb != 100 && criticalRate > 0 → not satisfied
	}

	[Fact]
	public void CreatePlan_PhysicalCapAt500()
	{
		// base=800 uncapped → effective=min(500, 800)=500
		var plan = CriticalRatePlanService.CreatePlan(CriticalType.Physical, 1000f, 200f, 100);

		Assert.Equal(500f, plan.EffectiveCriticalRate, 6);
	}

	// --- Magical critical rate ---

	[Fact]
	public void CreatePlan_MagicalNegativeBaseWithProbGetsMinimum1()
	{
		// Java: if (critical <= 0) critical = 1; then *= critProb/100
		// base=-100, prob=50 → min(1, -100)=1 → 1*50/100=0.5; effective=0.5
		var plan = CriticalRatePlanService.CreatePlan(CriticalType.Magical, 100f, 200f, 50);

		Assert.Equal(0.5f, plan.AdjustedCriticalRate, 6);
	}

	[Fact]
	public void CriticalDifferenceLimitConstantIs500()
	{
		Assert.Equal(500, CriticalRatePlanService.CriticalDifferenceLimit);
	}
}
