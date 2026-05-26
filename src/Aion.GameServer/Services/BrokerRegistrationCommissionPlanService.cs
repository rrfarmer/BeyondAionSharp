using Aion.GameServer.Configuration;

namespace Aion.GameServer.Services;

public sealed record BrokerRegistrationCommissionPlan(
	long RawCommission,
	long Commission,
	float Rate,
	bool UsesMinimumCommission,
	int RegisteredItemsCount,
	string JavaSource,
	bool IsLive);

public static class BrokerRegistrationCommissionPlanService
{
	public static BrokerRegistrationCommissionPlan CreatePlan(
		long price,
		long count,
		int registeredItemsCount,
		string playerRace,
		GameServerPriceOptions? priceOptions = null,
		PriceInfluenceRates? influenceRates = null)
	{
		// Java parity: services/BrokerService.registerItem registrationCommition calculation.
		// Java keeps the minimum commission at 10 without passing it through PricesService.
		var rate = registeredItemsCount > 9 ? 0.04f : 0.02f;
		var rawCommission = (long)(price * count * rate);
		var usesMinimumCommission = rawCommission < 10;
		var commission = usesMinimumCommission
			? 10
			: PricesService.GetPriceForService(
				rawCommission,
				playerRace,
				priceOptions ?? new GameServerPriceOptions(),
				influenceRates ?? new PriceInfluenceRates());
		return new BrokerRegistrationCommissionPlan(
			rawCommission,
			commission,
			rate,
			usesMinimumCommission,
			registeredItemsCount,
			"BrokerService.registerItem -> PricesService.getPriceForService",
			IsLive: false);
	}
}
