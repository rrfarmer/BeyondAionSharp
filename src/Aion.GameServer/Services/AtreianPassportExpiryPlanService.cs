namespace Aion.GameServer.Services;

public sealed record AtreianPassportExpiryPlan(
	DateTimeOffset? ExpireDate,
	bool IsDisabled,
	DateTimeOffset CheckDateTime,
	string JavaSource
)
{
	public bool IsLive => false;
}

public sealed record AtreianPassportExpireDatePlan(
	DateTimeOffset? LastRewardPeriodEnd,
	DateTimeOffset? ExpireDate,
	bool HasExpireDate,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class AtreianPassportExpiryPlanService
{
	// Java parity: AtreianPassportService.calculatePassportExpireDate adds 14 days after end-of-day.
	public const int ExpiryDaysAfterLastReward = 14;

	public static AtreianPassportExpiryPlan CreateIsDisabledPlan(DateTimeOffset? expireDate, DateTimeOffset checkDateTime)
	{
		// Java parity: AtreianPassportService.isAtreianPassportDisabled(LocalDateTime checkDateTime).
		// return expireDate != null && checkDateTime.isAfter(expireDate);
		var isDisabled = expireDate.HasValue && checkDateTime > expireDate.Value;

		return new AtreianPassportExpiryPlan(
			expireDate, isDisabled, checkDateTime,
			isDisabled
				? $"AtreianPassportService.isAtreianPassportDisabled -> expireDate={expireDate:O} checkTime={checkDateTime:O} -> true (disabled)"
				: $"AtreianPassportService.isAtreianPassportDisabled -> expireDate={expireDate?.ToString("O") ?? "null"} -> false (active)"
		);
	}

	public static AtreianPassportExpireDatePlan CreateExpireDatePlan(DateTimeOffset? lastRewardPeriodEnd)
	{
		// Java parity: AtreianPassportService.calculatePassportExpireDate.
		// disableDateTime.toLocalDate().atTime(LocalTime.MAX).plusDays(14)
		if (!lastRewardPeriodEnd.HasValue)
		{
			return new AtreianPassportExpireDatePlan(
				null, null, HasExpireDate: false,
				"AtreianPassportService.calculatePassportExpireDate -> findLastRewardTime() == null -> return null"
			);
		}

		// Java: start of day + LocalTime.MAX (23:59:59.9...) + 14 days
		var endOfDayOfLastReward = lastRewardPeriodEnd.Value.Date.AddDays(1).AddTicks(-1);
		var expireDate = new DateTimeOffset(endOfDayOfLastReward, lastRewardPeriodEnd.Value.Offset)
			.AddDays(ExpiryDaysAfterLastReward);

		return new AtreianPassportExpireDatePlan(
			lastRewardPeriodEnd,
			expireDate,
			HasExpireDate: true,
			$"AtreianPassportService.calculatePassportExpireDate -> lastReward={lastRewardPeriodEnd.Value:O} -> expire={expireDate:O} (end-of-day + {ExpiryDaysAfterLastReward} days)"
		);
	}
}
