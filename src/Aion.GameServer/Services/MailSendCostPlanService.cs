using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public sealed record MailSendCostPlan(
	int BaseCost,
	int CostFactor,
	long KinahMailCommission,
	long ItemMailCommission,
	long ServiceBaseCost,
	long ServicePrice,
	long AttachedKinah,
	long FinalMailKinah,
	string JavaSource,
	bool IsLive);

public static class MailSendCostPlanService
{
	public const int NormalLetterTypeId = 0;
	public const int ExpressLetterTypeId = 1;

	public static MailSendCostPlan CreatePlan(
		int letterTypeId,
		long attachedKinah,
		ItemTemplateSummary? attachedItemTemplate,
		long attachedItemCount,
		string senderRace,
		GameServerPriceOptions? priceOptions = null,
		PriceInfluenceRates? influenceRates = null)
	{
		// Java parity: services/mail/MailService.sendMail calculates base cost and commissions,
		// applies PricesService.getPriceForService, then adds attached kinah outside the service price.
		var costFactor = letterTypeId == ExpressLetterTypeId ? 5 : 1;
		var baseCost = letterTypeId == ExpressLetterTypeId ? 500 : 10;
		var kinahMailCommission = attachedKinah > 0
			? (long)(attachedKinah * 0.01f * costFactor)
			: 0;
		var itemMailCommission = attachedItemTemplate == null || attachedItemCount <= 0
			? 0
			: (long)(attachedItemTemplate.Price * GetQualityPriceRate(attachedItemTemplate.Quality) * attachedItemCount * costFactor);
		var serviceBaseCost = baseCost + kinahMailCommission + itemMailCommission;
		var servicePrice = PricesService.GetPriceForService(
			serviceBaseCost,
			senderRace,
			priceOptions ?? new GameServerPriceOptions(),
			influenceRates ?? new PriceInfluenceRates());
		return new MailSendCostPlan(
			baseCost,
			costFactor,
			kinahMailCommission,
			itemMailCommission,
			serviceBaseCost,
			servicePrice,
			attachedKinah,
			servicePrice + attachedKinah,
			"MailService.sendMail -> getQualityPriceRate -> PricesService.getPriceForService",
			IsLive: false);
	}

	public static float GetQualityPriceRate(string quality)
	{
		// Java parity: services/mail/MailService.getQualityPriceRate.
		return quality.ToUpperInvariant() switch
		{
			"MYTHIC" or "EPIC" => 0.05f,
			"UNIQUE" or "LEGEND" => 0.04f,
			"RARE" => 0.03f,
			_ => 0.02f,
		};
	}
}
