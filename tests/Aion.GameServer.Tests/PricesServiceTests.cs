using Aion.GameServer.Configuration;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PricesServiceTests
{
	[Fact]
	public void CreateSnapshot_UsesJavaInfluencePriceAndTaxRules()
	{
		var options = new GameServerPriceOptions
		{
			DefaultPrices = 100,
			DefaultModifier = 90,
			DefaultTaxes = 100,
			VendorBuyModifier = 125,
			VendorSellModifier = 20,
		};
		var influence = new PriceInfluenceRates(Elyos: 0.7f, Asmodians: 0.3f);

		var elyos = PricesService.CreateSnapshot("ELYOS", options, influence);
		var asmodians = PricesService.CreateSnapshot("ASMODIANS", options, influence);

		Assert.Equal(new PriceSnapshot(90, 90, 100, 125, 20), elyos);
		Assert.Equal(new PriceSnapshot(110, 90, 105, 125, 20), asmodians);
	}

	[Fact]
	public void PriceCalculations_FloorEachJavaDoublePercentageStep()
	{
		var options = new GameServerPriceOptions
		{
			DefaultPrices = 100,
			DefaultModifier = 90,
			DefaultTaxes = 100,
			VendorBuyModifier = 125,
			VendorSellModifier = 20,
		};
		var influence = new PriceInfluenceRates(Elyos: 0.5f, Asmodians: 0.3f);

		Assert.Equal(12_832, PricesService.GetPriceForService(12_345, "ASMODIANS", options, influence));
		Assert.Equal(16_039, PricesService.GetBuyPrice(12_345, "ASMODIANS", options, influence));
		Assert.Equal(19_753, PricesService.GetSellReward(98_765, PricesService.GetVendorSellModifier(options)));
	}

	[Fact]
	public void GetGlobalPrices_RejectsInvalidRaceLikeJava()
	{
		var ex = Assert.Throws<ArgumentException>(() =>
			PricesService.GetGlobalPrices("BALAUR", new GameServerPriceOptions(), new PriceInfluenceRates()));

		Assert.Contains("no valid player race", ex.Message);
	}
}
