namespace Aion.GameServer.Services;

public enum DropBidNormalizationPlanStatus
{
	BidValid,
	BidNormalizedToZero,
}

public sealed record DropBidNormalizationPlan(
	DropBidNormalizationPlanStatus Status,
	long RequestedBid,
	long NormalizedBid,
	bool BidNormalized,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class DropBidNormalizationPlanService
{
	public const long MaxBid = 999_999_999L;

	public static DropBidNormalizationPlan CreatePlan(long requestedBid, long playerKinah)
	{
		// Java parity: services/drop/DropDistributionService.handleBid bid normalization.
		// if ((bid > 0 && player.getInventory().getKinah() < bid) || bid < 0 || bid > 999999999) bid = 0;
		var shouldNormalize =
			(requestedBid > 0 && playerKinah < requestedBid)
			|| requestedBid < 0
			|| requestedBid > MaxBid;

		if (shouldNormalize)
		{
			return new DropBidNormalizationPlan(
				DropBidNormalizationPlanStatus.BidNormalizedToZero,
				requestedBid,
				NormalizedBid: 0,
				BidNormalized: true,
				$"DropDistributionService.handleBid -> bid({requestedBid}) invalid (kinah={playerKinah}) -> bid = 0 (pass)"
			);
		}

		return new DropBidNormalizationPlan(
			DropBidNormalizationPlanStatus.BidValid,
			requestedBid,
			NormalizedBid: requestedBid,
			BidNormalized: false,
			$"DropDistributionService.handleBid -> bid={requestedBid} valid (kinah={playerKinah})"
		);
	}
}
