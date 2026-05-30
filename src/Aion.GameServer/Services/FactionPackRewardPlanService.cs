namespace Aion.GameServer.Services;

public enum FactionPackEligibilityPlanStatus
{
	Eligible,
	SkippedNoRewardsOrNotLevel65,
	SkippedMailboxWouldOverflow,
	SkippedAccountCreatedBeforeWindow,
	SkippedAccountCreatedAfterWindow,
	SkippedAlreadyReceived,
}

public sealed record FactionPackEligibilityPlan(
	FactionPackEligibilityPlanStatus Status,
	string Race,
	DateTimeOffset AccountCreationTime,
	bool IsEligible,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class FactionPackRewardPlanService
{
	// Java parity: services/FactionPackService creation time windows.
	public static readonly DateTimeOffset ElyosMinCreationTime =
		new DateTimeOffset(2020, 9, 14, 0, 0, 0, TimeSpan.Zero);
	public static readonly DateTimeOffset ElyosMaxCreationTime =
		new DateTimeOffset(2020, 9, 26, 23, 59, 59, TimeSpan.Zero);
	public static readonly DateTimeOffset AsmodianMinCreationTime =
		new DateTimeOffset(2022, 6, 18, 0, 0, 0, TimeSpan.Zero);
	public static readonly DateTimeOffset AsmodianMaxCreationTime =
		new DateTimeOffset(2022, 7, 19, 23, 59, 59, TimeSpan.Zero);

	// Java parity: rewards list size (6 items)
	public const int RewardCount = 6;
	public const int MailboxLetterLimit = 100;
	public const int RequiredLevel = 65;

	// Java parity: FactionPackService mail text.
	public const string MailSender = "Beyond Aion";
	public const string MailTitle = "Faction Pack";
	public const string MailBody =
		"Greetings Daeva!\n\n"
		+ "In gratitude for your decision to join this faction we prepared an additional item pack.\n\n"
		+ "Enjoy your stay on Beyond Aion!";

	public static FactionPackEligibilityPlan CreatePlan(
		bool rewardsAvailable,
		int playerLevel,
		int currentMailboxLetters,
		string race,
		DateTimeOffset accountCreationTime,
		bool alreadyReceived)
	{
		// Java parity: services/FactionPackService.addPlayerCustomReward + sendRewards guard order.
		if (!rewardsAvailable || playerLevel != RequiredLevel
			|| currentMailboxLetters + RewardCount > MailboxLetterLimit)
		{
			return new FactionPackEligibilityPlan(
				FactionPackEligibilityPlanStatus.SkippedNoRewardsOrNotLevel65,
				race, accountCreationTime,
				IsEligible: false,
				"FactionPackService.addPlayerCustomReward -> rewards.isEmpty || level != 65 || mailbox overflow -> return"
			);
		}

		var (minTime, maxTime) = string.Equals(race, "ASMODIANS", StringComparison.OrdinalIgnoreCase)
			? (AsmodianMinCreationTime, AsmodianMaxCreationTime)
			: (ElyosMinCreationTime, ElyosMaxCreationTime);

		if (accountCreationTime < minTime)
		{
			return new FactionPackEligibilityPlan(
				FactionPackEligibilityPlanStatus.SkippedAccountCreatedBeforeWindow,
				race, accountCreationTime,
				IsEligible: false,
				$"FactionPackService.sendRewards -> creationTime {accountCreationTime:O} < minWindow {minTime:O} -> return"
			);
		}

		if (accountCreationTime > maxTime)
		{
			return new FactionPackEligibilityPlan(
				FactionPackEligibilityPlanStatus.SkippedAccountCreatedAfterWindow,
				race, accountCreationTime,
				IsEligible: false,
				$"FactionPackService.sendRewards -> creationTime {accountCreationTime:O} > maxWindow {maxTime:O} -> return"
			);
		}

		if (alreadyReceived)
		{
			return new FactionPackEligibilityPlan(
				FactionPackEligibilityPlanStatus.SkippedAlreadyReceived,
				race, accountCreationTime,
				IsEligible: false,
				"FactionPackService.sendRewards -> FactionPackDAO.loadReceivingPlayer > 0 -> return"
			);
		}

		return new FactionPackEligibilityPlan(
			FactionPackEligibilityPlanStatus.Eligible,
			race, accountCreationTime,
			IsEligible: true,
			$"FactionPackService.sendRewards -> all guards passed for {race} account created {accountCreationTime:O}"
		);
	}
}
