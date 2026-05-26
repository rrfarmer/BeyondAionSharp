using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public sealed record ArmsfusionPricePlan(
	long BasePricePerLevelSquared,
	int MainWeaponLevel,
	long BasePrice,
	long FusionPrice,
	string JavaSource,
	bool IsLive);

public static class ArmsfusionPricePlanService
{
	public static ArmsfusionPricePlan CreatePlan(
		ItemTemplateSummary mainWeaponTemplate,
		string playerRace,
		GameServerPriceOptions? priceOptions = null,
		PriceInfluenceRates? influenceRates = null)
	{
		// Java parity: services/ArmsfusionService.fusionWeapons calculates:
		// getBasePricePerLevelSquared(mainWeapon.quality) * level * level,
		// then services/trade/PricesService.getPriceForService(..., player.getRace()).
		var basePricePerLevelSquared = GetBasePricePerLevelSquared(mainWeaponTemplate.Quality);
		var level = mainWeaponTemplate.Level;
		var basePrice = basePricePerLevelSquared * level * level;
		var fusionPrice = PricesService.GetPriceForService(
			basePrice,
			playerRace,
			priceOptions ?? new GameServerPriceOptions(),
			influenceRates ?? new PriceInfluenceRates());
		return new ArmsfusionPricePlan(
			basePricePerLevelSquared,
			level,
			basePrice,
			fusionPrice,
			"ArmsfusionService.fusionWeapons -> getBasePricePerLevelSquared -> PricesService.getPriceForService",
			IsLive: false);
	}

	public static long GetBasePricePerLevelSquared(string itemQuality)
	{
		// Java parity: services/ArmsfusionService.getBasePricePerLevelSquared.
		return itemQuality.ToUpperInvariant() switch
		{
			"JUNK" or "COMMON" => 200,
			"RARE" => 250,
			"LEGEND" => 300,
			"UNIQUE" => 400,
			"EPIC" => 500,
			_ => 600,
		};
	}
}
