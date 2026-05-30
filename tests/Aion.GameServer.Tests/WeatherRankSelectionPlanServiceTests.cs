using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WeatherRankSelectionPlanServiceTests
{
	// --- Rank selection ---

	[Fact]
	public void SelectRank_ZeroIsRank0()
	{
		Assert.Equal(0, WeatherRankSelectionPlanService.SelectRank(0));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	public void SelectRank_OneOrTwoIsRank1(int rankChance)
	{
		Assert.Equal(1, WeatherRankSelectionPlanService.SelectRank(rankChance));
	}

	[Theory]
	[InlineData(3)]
	[InlineData(4)]
	[InlineData(5)]
	[InlineData(6)]
	public void SelectRank_ThreeToSixIsRank2(int rankChance)
	{
		Assert.Equal(2, WeatherRankSelectionPlanService.SelectRank(rankChance));
	}

	// --- Snow season ---

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(11)]
	[InlineData(12)]
	public void CanSnow_SnowSeasonMonths(int month)
	{
		Assert.True(WeatherRankSelectionPlanService.CanSnow(month));
	}

	[Theory]
	[InlineData(4)]
	[InlineData(5)]
	[InlineData(6)]
	[InlineData(7)]
	[InlineData(8)]
	[InlineData(9)]
	[InlineData(10)]
	public void CanSnow_NonSnowSeasonMonths(int month)
	{
		Assert.False(WeatherRankSelectionPlanService.CanSnow(month));
	}

	// --- Full plan ---

	[Fact]
	public void CreatePlan_ComposesRankAndSnowFromInputs()
	{
		var plan = WeatherRankSelectionPlanService.CreatePlan(rankChance: 4, month: 12);

		Assert.Equal(4, plan.RankChance);
		Assert.Equal(2, plan.SelectedRank);
		Assert.True(plan.CanSnow); // December = snow
		Assert.Equal(12, plan.Month);
		Assert.False(plan.IsLive);
	}
}
