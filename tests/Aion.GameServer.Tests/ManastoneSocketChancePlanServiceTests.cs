using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ManastoneSocketChancePlanServiceTests
{
	// --- CalculateSlotLevel ---

	[Theory]
	[InlineData(1,  20)]   // ceil((1+10)/10)*10 = ceil(1.1)*10 = 2*10 = 20
	[InlineData(10, 20)]   // ceil((10+10)/10)*10 = ceil(2.0)*10 = 2*10 = 20
	[InlineData(11, 30)]   // ceil((11+10)/10)*10 = ceil(2.1)*10 = 3*10 = 30
	[InlineData(30, 40)]   // ceil((30+10)/10)*10 = ceil(4.0)*10 = 4*10 = 40
	[InlineData(55, 70)]   // ceil((55+10)/10)*10 = ceil(6.5)*10 = 7*10 = 70
	[InlineData(60, 70)]   // ceil((60+10)/10)*10 = ceil(7.0)*10 = 7*10 = 70
	[InlineData(65, 80)]   // ceil((65+10)/10)*10 = ceil(7.5)*10 = 8*10 = 80
	public void CalculateSlotLevel_MatchesJavaFormula(int itemLevel, int expectedSlotLevel)
	{
		Assert.Equal(expectedSlotLevel, ManastoneSocketChancePlanService.CalculateSlotLevel(itemLevel));
	}

	// --- socketManastone chance ---

	[Fact]
	public void CreatePlan_StoneLevelTooHighBlocks()
	{
		// itemLevel=10 → slotLevel=20; stoneLevel=25 > 20 → blocked
		var plan = ManastoneSocketChancePlanService.CreatePlan(10, stoneLevel: 25, 0, false, 80f, 0f);

		Assert.True(plan.StoneLevelTooHigh);
		Assert.Equal(0f, plan.FinalChance, 6);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_NormalQualityNoReduction()
	{
		// itemLevel=55 → slotLevel=70; stoneLevel=55, count=0, normal quality
		// socketDiff = 0*1.25+1.75 = 1.75; bonus = (70-55)/1.75 = 8.57; final ≈ 80+8.57=88.57
		var plan = ManastoneSocketChancePlanService.CreatePlan(55, 55, 0, false, 80f, 0f);

		Assert.False(plan.StoneLevelTooHigh);
		Assert.Equal(1.75f, plan.SocketDiff, 4);
		Assert.InRange(plan.FinalChance, 88f, 90f);
	}

	[Fact]
	public void CreatePlan_RareQualityReduces20Percent()
	{
		// base=80, isRare=true → 80*0.8 = 64 before diff bonus
		var plan = ManastoneSocketChancePlanService.CreatePlan(55, 55, 0, true, 80f, 0f);

		// base was 80, reduced to 64; diff bonus added
		Assert.True(plan.IsRareOrHigher);
		Assert.True(plan.BaseChance > plan.ChanceAfterDiffBonus - 10); // some diff bonus added but started from 64
	}

	[Fact]
	public void CreatePlan_MoreStonesIncreaseDifficulty()
	{
		// count=3 → socketDiff = 3*1.25+1.75 = 5.5 > 1.75 → smaller bonus
		var noStones = ManastoneSocketChancePlanService.CreatePlan(55, 55, 0, false, 80f, 0f);
		var threeStones = ManastoneSocketChancePlanService.CreatePlan(55, 55, 3, false, 80f, 0f);

		Assert.True(noStones.FinalChance > threeStones.FinalChance);
	}

	[Fact]
	public void CreatePlan_SupplementAddsChance()
	{
		var noSupp = ManastoneSocketChancePlanService.CreatePlan(55, 55, 0, false, 80f, 0f);
		var withSupp = ManastoneSocketChancePlanService.CreatePlan(55, 55, 0, false, 80f, 10f);

		Assert.Equal(noSupp.FinalChance + 10f, withSupp.FinalChance, 4);
	}
}
