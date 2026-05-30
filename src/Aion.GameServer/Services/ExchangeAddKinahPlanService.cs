using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum ExchangeAddKinahPlanStatus
{
	CanAdd,
	BlockedNoAvailableKinah,
	BlockedZeroOrNegativeAmount,
}

public sealed record ExchangeAddKinahPlan(
	ExchangeAddKinahPlanStatus Status,
	long RequestedAmount,
	long CountToAdd,
	SmExchangeAddKinah? SelfPacket,
	SmExchangeAddKinah? OtherPacket,
	bool ShouldSendToSelf,
	bool ShouldSendToOther,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class ExchangeAddKinahPlanService
{
	public static ExchangeAddKinahPlan CreatePlan(
		long requestedAmount,
		long inventoryKinah,
		long alreadyInExchangeKinah)
	{
		// Java parity: services/ExchangeService.addKinah.
		// if (itemCount < 1) return (silent)
		// availableCount = inventoryKinah - exchangeKinahCount
		// countToAdd = min(availableCount, itemCount)
		// if (countToAdd > 0) -> send packets
		if (requestedAmount < 1)
		{
			return new ExchangeAddKinahPlan(
				ExchangeAddKinahPlanStatus.BlockedZeroOrNegativeAmount,
				requestedAmount,
				CountToAdd: 0,
				SelfPacket: null,
				OtherPacket: null,
				ShouldSendToSelf: false,
				ShouldSendToOther: false,
				"ExchangeService.addKinah -> itemCount < 1 -> return (silent)"
			);
		}

		var availableCount = inventoryKinah - alreadyInExchangeKinah;
		var countToAdd = availableCount > requestedAmount ? requestedAmount : availableCount;

		if (countToAdd <= 0)
		{
			return new ExchangeAddKinahPlan(
				ExchangeAddKinahPlanStatus.BlockedNoAvailableKinah,
				requestedAmount,
				CountToAdd: 0,
				SelfPacket: null,
				OtherPacket: null,
				ShouldSendToSelf: false,
				ShouldSendToOther: false,
				$"ExchangeService.addKinah -> availableCount({availableCount}) <= 0 -> no packets"
			);
		}

		return new ExchangeAddKinahPlan(
			ExchangeAddKinahPlanStatus.CanAdd,
			requestedAmount,
			countToAdd,
			new SmExchangeAddKinah(countToAdd, SmExchangeAddKinah.ActionSelf),
			new SmExchangeAddKinah(countToAdd, SmExchangeAddKinah.ActionOther),
			ShouldSendToSelf: true,
			ShouldSendToOther: true,
			$"ExchangeService.addKinah -> countToAdd={countToAdd} -> SM_EXCHANGE_ADD_KINAH(action=0) to self + SM_EXCHANGE_ADD_KINAH(action=1) to partner"
		);
	}
}
