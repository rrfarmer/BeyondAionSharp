using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class TradeApFormulaServiceTests
{
	[Fact]
	public void CalculateAbyssBuyRequiredAp_MatchesJavaDoubleNarrowingAndFinalIntegerDivision()
	{
		var single = TradeApFormulaService.CalculateAbyssBuyRequiredAp(
			requiredApPerItem: 1_234,
			count: 3,
			sellPriceRate: 85,
			vendorBuyModifier: 125);
		var summed = TradeApFormulaService.CalculateAbyssBuyRequiredAp(
			[
				new TradeApCostComponent(1_234, 3),
				new TradeApCostComponent(500, 2),
			],
			sellPriceRate: 85,
			vendorBuyModifier: 125);

		Assert.Equal(3_933, single);
		Assert.Equal(4_995, summed);
	}

	[Fact]
	public void CalculateAbyssBuyRequiredAp_UsesAbyssKinahModifierInputLikeJavaCaller()
	{
		var normalRate = TradeApFormulaService.CalculateAbyssBuyRequiredAp(
			requiredApPerItem: 10_000,
			count: 1,
			sellPriceRate: 100,
			vendorBuyModifier: 100);
		var abyssKinahRate = TradeApFormulaService.CalculateAbyssBuyRequiredAp(
			requiredApPerItem: 10_000,
			count: 1,
			sellPriceRate: 80,
			vendorBuyModifier: 100);

		Assert.Equal(10_000, normalRate);
		Assert.Equal(8_000, abyssKinahRate);
	}

	[Fact]
	public void CalculateApResaleReward_MatchesJavaMathRoundAndCountCast()
	{
		var roundedUp = TradeApFormulaService.CalculateApResaleReward(
			requiredApPerItem: 1_255,
			buyPriceRate: 12.5f,
			count: 3);
		var roundedDown = TradeApFormulaService.CalculateApResaleReward(
			requiredApPerItem: 1_244,
			buyPriceRate: 12.5f,
			count: 3);

		Assert.Equal(471, roundedUp);
		Assert.Equal(468, roundedDown);
	}

	[Fact]
	public void CalculateTradeInApDelta_SpendsOnlyPositiveDifference()
	{
		var positiveDelta = TradeApFormulaService.CalculateTradeInApDelta(
			targetRequiredAp: 20_000,
			tradeInItemRequiredAp: [5_000, 2_500],
			count: 2,
			sellPriceRate: 100,
			vendorBuyModifier: 100);
		var coveredDelta = TradeApFormulaService.CalculateTradeInApDelta(
			targetRequiredAp: 10_000,
			tradeInItemRequiredAp: [8_000, 3_000],
			count: 1,
			sellPriceRate: 100,
			vendorBuyModifier: 100);

		Assert.Equal(25_000, positiveDelta);
		Assert.Equal(0, coveredDelta);
	}
}
