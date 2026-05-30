namespace Aion.GameServer.Services;

public enum VeteranRewardEligibilityPlanStatus
{
	Eligible,
	SkippedNotLevel65,
	SkippedCharTooYoung,
	SkippedAccountTooYoung,
	SkippedAlreadyReceivedAll,
	SkippedDaoError,
}

public sealed record VeteranRewardEligibilityPlan(
	VeteranRewardEligibilityPlanStatus Status,
	int PlayerLevel,
	int CharAgeMonths,
	int AccountAgeMonths,
	int ReceivedMonths,
	int MaxMonthsToReceive,
	int PendingMonths,
	bool IsEligible,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class VeteranRewardEligibilityPlanService
{
	// Java parity: services/reward/VeteranRewardService.RANDOM_ITEMS_PER_MONTH.
	public const int RandomItemsPerMonth = 4;

	// Java parity: maximum fixed monthly rewards (60 months = 5 years).
	public const int MaxFixedRewardMonths = 60;

	public static VeteranRewardEligibilityPlan CreatePlan(
		int playerLevel,
		int charAgeMonths,
		int accountAgeMonths,
		int receivedMonths)
	{
		// Java parity: services/reward/VeteranRewardService.tryReward guard order.
		if (playerLevel != 65)
		{
			return new VeteranRewardEligibilityPlan(
				VeteranRewardEligibilityPlanStatus.SkippedNotLevel65,
				playerLevel, charAgeMonths, accountAgeMonths, receivedMonths, 0, 0,
				IsEligible: false,
				$"VeteranRewardService.tryReward -> player.getLevel() ({playerLevel}) != 65 -> return"
			);
		}

		if (charAgeMonths < 1)
		{
			return new VeteranRewardEligibilityPlan(
				VeteranRewardEligibilityPlanStatus.SkippedCharTooYoung,
				playerLevel, charAgeMonths, accountAgeMonths, receivedMonths, 0, 0,
				IsEligible: false,
				"VeteranRewardService.tryReward -> MONTHS.between(charCreation, now) < 1 -> return"
			);
		}

		var maxMonths = accountAgeMonths;
		if (maxMonths < 1)
		{
			return new VeteranRewardEligibilityPlan(
				VeteranRewardEligibilityPlanStatus.SkippedAccountTooYoung,
				playerLevel, charAgeMonths, accountAgeMonths, receivedMonths, maxMonths, 0,
				IsEligible: false,
				"VeteranRewardService.tryReward -> maxMonthsToReceive < 1 -> return"
			);
		}

		if (receivedMonths < 0)
		{
			return new VeteranRewardEligibilityPlan(
				VeteranRewardEligibilityPlanStatus.SkippedDaoError,
				playerLevel, charAgeMonths, accountAgeMonths, receivedMonths, maxMonths, 0,
				IsEligible: false,
				"VeteranRewardService.tryReward -> receivedMonths < 0 (DAO error) -> return"
			);
		}

		if (receivedMonths >= maxMonths)
		{
			return new VeteranRewardEligibilityPlan(
				VeteranRewardEligibilityPlanStatus.SkippedAlreadyReceivedAll,
				playerLevel, charAgeMonths, accountAgeMonths, receivedMonths, maxMonths, 0,
				IsEligible: false,
				$"VeteranRewardService.tryReward -> receivedMonths({receivedMonths}) >= maxMonths({maxMonths}) -> return"
			);
		}

		var pendingMonths = maxMonths - receivedMonths;
		return new VeteranRewardEligibilityPlan(
			VeteranRewardEligibilityPlanStatus.Eligible,
			playerLevel, charAgeMonths, accountAgeMonths, receivedMonths, maxMonths, pendingMonths,
			IsEligible: true,
			$"VeteranRewardService.tryReward -> eligible for {pendingMonths} pending months ({receivedMonths}-{maxMonths})"
		);
	}
}
