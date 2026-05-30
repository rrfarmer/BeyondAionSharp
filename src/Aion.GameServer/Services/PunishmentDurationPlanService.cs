namespace Aion.GameServer.Services;

public sealed record PunishmentBanDurationPlan(
	int DayCount,
	long DurationSeconds,
	bool IsPermanent,
	string JavaSource
)
{
	public bool IsLive => false;
}

public sealed record PunishmentPrisonRemainingMinutesPlan(
	int DurationSeconds,
	int RemainingMinutes,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class PunishmentDurationPlanService
{
	public const long PermanentBanDurationSeconds = int.MaxValue;

	public static PunishmentBanDurationPlan CreateBanDurationPlan(int dayCount)
	{
		// Java parity: services/PunishmentService.calculateDuration.
		// dayCount == 0 means permanent (Integer.MAX_VALUE seconds); otherwise TimeUnit.DAYS.toSeconds(dayCount).
		var durationSeconds = dayCount == 0
			? PermanentBanDurationSeconds
			: (long)dayCount * 86400L;

		return new PunishmentBanDurationPlan(
			dayCount,
			durationSeconds,
			IsPermanent: dayCount == 0,
			dayCount == 0
				? "PunishmentService.calculateDuration -> dayCount == 0 -> Integer.MAX_VALUE (permanent)"
				: $"PunishmentService.calculateDuration -> TimeUnit.DAYS.toSeconds({dayCount}) = {durationSeconds}"
		);
	}

	public static PunishmentPrisonRemainingMinutesPlan CreatePrisonRemainingMinutesPlan(int prisonDurationSeconds)
	{
		// Java parity: services/PunishmentService.updatePrisonStatus remaining-minutes formula.
		// int remainingMinutes = prisonDurationSeconds / 60; if (remainingMinutes <= 0) remainingMinutes = 1;
		var remainingMinutes = prisonDurationSeconds / 60;
		if (remainingMinutes <= 0)
			remainingMinutes = 1;

		return new PunishmentPrisonRemainingMinutesPlan(
			prisonDurationSeconds,
			remainingMinutes,
			$"PunishmentService.updatePrisonStatus -> remainingMinutes = max(1, {prisonDurationSeconds} / 60) = {remainingMinutes}"
		);
	}
}
