using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class TeleportTransportationPricePlanServiceTests
{
	[Fact]
	public void CreatePlan_UsesJavaPricesServiceAndStagesFlyKinahDecrease()
	{
		var player = CreatePlayer(kinah: 20_000);

		var plan = TeleportTransportationPricePlanService.CreatePlan(
			player,
			locationBasePrice: 12_345,
			priceOptions: new GameServerPriceOptions
			{
				DefaultPrices = 100,
				DefaultModifier = 90,
				DefaultTaxes = 100,
				VendorBuyModifier = 125,
				VendorSellModifier = 20,
			},
			influenceRates: new PriceInfluenceRates(Elyos: 0.5f, Asmodians: 0.3f));

		Assert.False(plan.IsLive);
		Assert.Equal(TeleportTransportationPricePlanStatus.Ready, plan.Status);
		Assert.False(plan.UsedHiPass);
		Assert.Equal(12_345, plan.BasePrice);
		Assert.Equal(12_832, plan.TransportationPrice);
		Assert.Equal(7_168, plan.KinahItemUpdate?.Count);
		Assert.Contains("TeleportService.checkKinahForTransportation", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_UsesHiPassOneKinahOverrideBeforePricesService()
	{
		var player = CreatePlayer(kinah: 5);

		var plan = TeleportTransportationPricePlanService.CreatePlan(
			player,
			locationBasePrice: 500_000,
			hasHiPassEffect: true,
			priceOptions: new GameServerPriceOptions { DefaultPrices = 200, DefaultModifier = 200, DefaultTaxes = 200 },
			influenceRates: new PriceInfluenceRates(Elyos: 0.5f, Asmodians: 0.5f));

		Assert.Equal(TeleportTransportationPricePlanStatus.Ready, plan.Status);
		Assert.True(plan.UsedHiPass);
		Assert.Equal(1, plan.TransportationPrice);
		Assert.Equal(4, plan.KinahItemUpdate?.Count);
	}

	[Fact]
	public void CreatePlan_ReportsNotEnoughKinahWithRequiredPrice()
	{
		var player = CreatePlayer(kinah: 100);

		var plan = TeleportTransportationPricePlanService.CreatePlan(
			player,
			locationBasePrice: 12_345,
			priceOptions: new GameServerPriceOptions(),
			influenceRates: new PriceInfluenceRates());

		Assert.Equal(TeleportTransportationPricePlanStatus.NotEnoughKinah, plan.Status);
		Assert.Equal(12_345, plan.TransportationPrice);
		Assert.Null(plan.KinahItemUpdate);
	}

	private static Player CreatePlayer(long kinah)
	{
		return new Player
		{
			ObjectId = 7001,
			Race = "ASMODIANS",
			InventoryItems =
			[
				new InventoryItem { ObjectId = 9001, ItemId = 182400001, Count = kinah, Location = 0 },
			],
		};
	}
}
