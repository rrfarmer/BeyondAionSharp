using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SellLimitLookupServiceTests
{
	[Theory]
	[InlineData(1,  5_300_047L)]
	[InlineData(15, 5_300_047L)]
	[InlineData(30, 5_300_047L)]
	[InlineData(31, 7_100_047L)]
	[InlineData(40, 7_100_047L)]
	[InlineData(41, 12_050_047L)]
	[InlineData(55, 12_050_047L)]
	[InlineData(56, 14_600_047L)]
	[InlineData(60, 14_600_047L)]
	[InlineData(61, 17_150_047L)]
	[InlineData(65, 17_150_047L)]
	public void CreatePlan_MatchesJavaEnumRange(int level, long expectedLimit)
	{
		var plan = SellLimitLookupService.CreatePlan(level);

		Assert.True(plan.Found);
		Assert.Equal(expectedLimit, plan.BaseLimit);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_LevelBelowRangeNotFound()
	{
		// Java would throw; C# returns not-found
		var plan = SellLimitLookupService.CreatePlan(0);
		Assert.False(plan.Found);
		Assert.Null(plan.BaseLimit);
	}

	[Fact]
	public void CreatePlan_LevelAboveMaxNotFound()
	{
		var plan = SellLimitLookupService.CreatePlan(66);
		Assert.False(plan.Found);
	}

	[Fact]
	public void Entries_HasFiveRanges()
	{
		Assert.Equal(5, SellLimitLookupService.Entries.Count);
	}
}
