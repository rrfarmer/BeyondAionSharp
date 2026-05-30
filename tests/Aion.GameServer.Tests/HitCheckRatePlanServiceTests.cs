using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class HitCheckRatePlanServiceTests
{
	// --- Difference limits ---

	[Theory]
	[InlineData(HitCheckType.Dodge, 300)]
	[InlineData(HitCheckType.Parry, 400)]
	[InlineData(HitCheckType.Block, 500)]
	public void GetDifferenceLimit_ReturnsJavaValues(HitCheckType type, int expected)
	{
		Assert.Equal(expected, HitCheckRatePlanService.GetDifferenceLimit(type));
	}

	// --- Dodge ---

	[Fact]
	public void CreatePlan_DodgeBasicRate()
	{
		// evasion=400, accuracy=200 → base=200, not NPC → effective=min(300,200)=200
		var plan = HitCheckRatePlanService.CreatePlan(HitCheckType.Dodge, 400f, 200f, isNpcTarget: false);

		Assert.Equal(200f, plan.BaseRate, 6);
		Assert.Equal(200f, plan.EffectiveRate, 6);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_DodgeCappedAt300()
	{
		// base=500 → effective=min(300,500)=300
		var plan = HitCheckRatePlanService.CreatePlan(HitCheckType.Dodge, 700f, 200f, isNpcTarget: false);

		Assert.Equal(300f, plan.EffectiveRate, 6);
	}

	[Fact]
	public void CreatePlan_NpcDodgeAppliesLevelDiffMod()
	{
		// Java: if attacked instanceof Npc → dodgeRate *= (1 + npcLevelDiffMod)
		// base=100, npcLevelDiffMod=0.3 → adjusted=100*1.3=130; effective=min(300,130)=130
		var plan = HitCheckRatePlanService.CreatePlan(
			HitCheckType.Dodge, 300f, 200f, isNpcTarget: true, npcLevelDiffMod: 0.3f);

		Assert.Equal(130f, plan.AdjustedRate, 4);
		Assert.Equal(130f, plan.EffectiveRate, 4);
	}

	[Fact]
	public void CreatePlan_PlayerDodgeDoesNotApplyLevelDiffMod()
	{
		var plan = HitCheckRatePlanService.CreatePlan(
			HitCheckType.Dodge, 300f, 200f, isNpcTarget: false, npcLevelDiffMod: 0.5f);

		// No NPC level diff applied for non-NPC target
		Assert.Equal(100f, plan.AdjustedRate, 6);
	}

	// --- Parry ---

	[Fact]
	public void CreatePlan_ParryCappedAt400()
	{
		var plan = HitCheckRatePlanService.CreatePlan(HitCheckType.Parry, 700f, 200f, isNpcTarget: false);

		Assert.Equal(400f, plan.EffectiveRate, 6);
	}

	// --- Block ---

	[Fact]
	public void CreatePlan_BlockCappedAt500()
	{
		var plan = HitCheckRatePlanService.CreatePlan(HitCheckType.Block, 800f, 200f, isNpcTarget: false);

		Assert.Equal(500f, plan.EffectiveRate, 6);
	}
}
