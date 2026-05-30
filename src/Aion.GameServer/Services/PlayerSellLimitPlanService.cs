using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PlayerSellLimitPlanStatus
{
	AllowedUnlimited,
	AllowedWithCount,
	BlockedLimitReached,
	BlockedInvalidInputs,
}

public sealed record PlayerSellLimitPlan(
	PlayerSellLimitPlanStatus Status,
	long UseCount,
	long RemainingLimitAfter,
	SmSystemMessage? LimitReachedMessage,
	bool ShouldSendLimitMessage,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class PlayerSellLimitPlanService
{
	public static PlayerSellLimitPlan CreatePlan(
		bool limitsEnabled,
		bool dynamicCapEnabled,
		long itemPrice,
		long itemCount,
		long currentLimit)
	{
		// Java parity: services/player/PlayerLimitService.updateSellLimit.
		// Live sellLimit map mutation is deferred; only the deterministic count/limit formula is ported.
		if (!limitsEnabled || itemPrice == 0)
		{
			return new PlayerSellLimitPlan(
				PlayerSellLimitPlanStatus.AllowedUnlimited,
				UseCount: itemCount,
				RemainingLimitAfter: currentLimit,
				LimitReachedMessage: null,
				ShouldSendLimitMessage: false,
				"PlayerLimitService.updateSellLimit -> !LIMITS_ENABLED || itemPrice == 0 -> return itemCount"
			);
		}

		if (itemPrice < 0 || itemCount <= 0)
		{
			return new PlayerSellLimitPlan(
				PlayerSellLimitPlanStatus.BlockedInvalidInputs,
				UseCount: 0,
				RemainingLimitAfter: currentLimit,
				LimitReachedMessage: null,
				ShouldSendLimitMessage: false,
				"PlayerLimitService.updateSellLimit -> itemPrice < 0 || itemCount <= 0 -> return 0"
			);
		}

		var possibleCount = Math.Max(0L, currentLimit / itemPrice);
		if (dynamicCapEnabled && possibleCount < itemCount)
			possibleCount += 1;

		if (possibleCount == 0 || currentLimit == 0)
		{
			return new PlayerSellLimitPlan(
				PlayerSellLimitPlanStatus.BlockedLimitReached,
				UseCount: 0,
				RemainingLimitAfter: currentLimit,
				SmSystemMessage.DayCannotSellNpc(currentLimit),
				ShouldSendLimitMessage: true,
				$"PlayerLimitService.updateSellLimit -> possibleCount==0 || limit==0 -> STR_MSG_DAY_CANNOT_SELL_NPC({currentLimit}) -> return 0"
			);
		}

		var useCount = Math.Min(possibleCount, itemCount);
		var deducted = Math.Min(currentLimit, itemPrice * useCount);
		var remainingLimit = currentLimit - deducted;

		return new PlayerSellLimitPlan(
			PlayerSellLimitPlanStatus.AllowedWithCount,
			useCount,
			remainingLimit,
			LimitReachedMessage: null,
			ShouldSendLimitMessage: false,
			$"PlayerLimitService.updateSellLimit -> useCount={useCount} limit {currentLimit} -> {remainingLimit} [live map deduction deferred]"
		);
	}
}
