namespace Aion.GameServer.Services;

public enum BonusPackRewardPlanStatus
{
	CanReward,
	SkippedNoRewards,
	SkippedNotLevel65,
	SkippedMailboxWouldOverflow,
	SkippedAlreadyReceived,
}

public sealed record BonusPackRewardPlan(
	BonusPackRewardPlanStatus Status,
	int PlayerLevel,
	int CurrentMailboxLetters,
	int RewardCount,
	bool CanReward,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class BonusPackRewardPlanService
{
	// Java parity: services/BonusPackService.addPlayerCustomReward constants.
	public const int RequiredLevel = 65;
	public const int MailboxLetterLimit = 100;

	// Java parity: mail text sent by BonusPackService.
	public const string MailSender = "Beyond Aion";
	public const string MailTitle = "Bonus Pack";
	public const string MailBody =
		"Greetings Daeva!\n\n"
		+ "You have reached level 65 with your first character and therefore we have a special something for you. "
		+ "In gratitude for your support we have prepared a package with valuable items for you.\n\n"
		+ "Enjoy your stay on Beyond Aion!";

	public static BonusPackRewardPlan CreatePlan(
		bool hasRewards,
		int rewardCount,
		int playerLevel,
		int currentMailboxLetters,
		bool alreadyReceived)
	{
		// Java parity: services/BonusPackService.addPlayerCustomReward guard order.
		if (!hasRewards || rewardCount == 0)
		{
			return new BonusPackRewardPlan(
				BonusPackRewardPlanStatus.SkippedNoRewards,
				playerLevel, currentMailboxLetters, rewardCount,
				CanReward: false,
				"BonusPackService.addPlayerCustomReward -> rewards == null || isEmpty -> return"
			);
		}

		if (playerLevel != RequiredLevel)
		{
			return new BonusPackRewardPlan(
				BonusPackRewardPlanStatus.SkippedNotLevel65,
				playerLevel, currentMailboxLetters, rewardCount,
				CanReward: false,
				$"BonusPackService.addPlayerCustomReward -> player.getLevel() ({playerLevel}) != 65 -> return"
			);
		}

		if (currentMailboxLetters + rewardCount > MailboxLetterLimit)
		{
			return new BonusPackRewardPlan(
				BonusPackRewardPlanStatus.SkippedMailboxWouldOverflow,
				playerLevel, currentMailboxLetters, rewardCount,
				CanReward: false,
				$"BonusPackService.addPlayerCustomReward -> mailboxLetters({currentMailboxLetters}) + rewards({rewardCount}) > {MailboxLetterLimit} -> return"
			);
		}

		if (alreadyReceived)
		{
			return new BonusPackRewardPlan(
				BonusPackRewardPlanStatus.SkippedAlreadyReceived,
				playerLevel, currentMailboxLetters, rewardCount,
				CanReward: false,
				"BonusPackService.addPlayerCustomReward -> BonusPackDAO.loadReceivingPlayer(accountId) > 0 -> return"
			);
		}

		return new BonusPackRewardPlan(
			BonusPackRewardPlanStatus.CanReward,
			playerLevel, currentMailboxLetters, rewardCount,
			CanReward: true,
			$"BonusPackService.addPlayerCustomReward -> all guards passed -> store receipt + send {rewardCount} express mails"
		);
	}
}
