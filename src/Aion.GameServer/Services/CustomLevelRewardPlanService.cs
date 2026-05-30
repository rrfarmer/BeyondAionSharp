using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class CustomLevelRewardPlanService
{
	// Java parity: services/BonusPackService and services/FactionPackService reward tables,
	// sender/title/body text, and level/mailbox gating all collapse into this shared planner.
	public const int TargetLevel = 65;
	public const int MaxMailboxLetters = 100;
	public const string Sender = "Beyond Aion";
	public const string LetterType = "EXPRESS";
	public const string BonusPackTitle = "Bonus Pack";
	public const string BonusPackBody =
		"Greetings Daeva!\n\n"
		+ "You have reached level 65 with your first character and therefore we have a special something for you."
		+ " In gratitude for your support we have prepared a package with valuable items for you.\n\n"
		+ "Enjoy your stay on Beyond Aion!";
	public const string FactionPackTitle = "Faction Pack";
	public const string FactionPackBody =
		"Greetings Daeva!\n\n"
		+ "In gratitude for your decision to join this faction we prepared an additional item pack.\n\n"
		+ "Enjoy your stay on Beyond Aion!";

	public static readonly DateTime ElyosMinCreationTime = new(2020, 9, 14, 0, 0, 0);
	public static readonly DateTime ElyosMaxCreationTime = new(2020, 9, 26, 23, 59, 59);
	public static readonly DateTime AsmodianMinCreationTime = new(2022, 6, 18, 0, 0, 0);
	public static readonly DateTime AsmodianMaxCreationTime = new(2022, 7, 19, 23, 59, 59);

	private static readonly IReadOnlyList<CustomLevelRewardItem> BonusPackRewards =
	[
		new CustomLevelRewardItem(186000242, 15),
		new CustomLevelRewardItem(186000130, 6500),
		new CustomLevelRewardItem(186000051, 5),
		new CustomLevelRewardItem(166020003, 15),
		new CustomLevelRewardItem(186000236, 250),
		new CustomLevelRewardItem(186000237, 4500),
		new CustomLevelRewardItem(186000409, 150),
		new CustomLevelRewardItem(188052562, 5),
		new CustomLevelRewardItem(190100051, 1),
	];

	private static readonly IReadOnlyList<CustomLevelRewardItem> FactionPackRewards =
	[
		new CustomLevelRewardItem(186000236, 500),
		new CustomLevelRewardItem(162002030, 250),
		new CustomLevelRewardItem(162000023, 100),
		new CustomLevelRewardItem(166000195, 50),
		new CustomLevelRewardItem(169630007, 1),
		new CustomLevelRewardItem(188053526, 5),
	];

	public static IReadOnlyList<CustomLevelRewardItem> BonusRewards => BonusPackRewards;

	public static IReadOnlyList<CustomLevelRewardItem> FactionRewards => FactionPackRewards;

	public static CustomLevelRewardPlan CreateBonusPackPlan(Player? player, int receivedPlayerId, bool storeReceivingPlayerSucceeded)
	{
		// Java parity: BonusPackService.addPlayerCustomReward entry path before DAO receipt checks
		// fan out to one express system mail per reward descriptor.
		return CreatePackPlan(
			CustomLevelRewardPackKind.Bonus,
			player,
			accountCreationLocalTime: null,
			receivedPlayerId,
			storeReceivingPlayerSucceeded,
			itemTemplates: null
		);
	}

	public static CustomLevelRewardPlan CreateFactionPackPlan(
		Player? player,
		DateTime accountCreationLocalTime,
		int receivedPlayerId,
		bool storeReceivingPlayerSucceeded,
		ItemTemplateTable? itemTemplates = null
	)
	{
		// Java parity: FactionPackService.sendRewards uses the same descriptor pipeline, but adds
		// faction-creation-window and opposite-race item filtering.
		return CreatePackPlan(
			CustomLevelRewardPackKind.Faction,
			player,
			accountCreationLocalTime,
			receivedPlayerId,
			storeReceivingPlayerSucceeded,
			itemTemplates
		);
	}

	private static CustomLevelRewardPlan CreatePackPlan(
		CustomLevelRewardPackKind kind,
		Player? player,
		DateTime? accountCreationLocalTime,
		int receivedPlayerId,
		bool storeReceivingPlayerSucceeded,
		ItemTemplateTable? itemTemplates
	)
	{
		// Java parity: BonusPackService.addPlayerCustomReward / FactionPackService.sendRewards guard
		// order checks player presence, configured rewards, level, mailbox space, faction windows,
		// prior receipt, and receipt-store success before assembling system-mail descriptors.
		if (player == null)
			return CustomLevelRewardPlan.MissingPlayer(kind, accountCreationLocalTime, receivedPlayerId, storeReceivingPlayerSucceeded);

		var rewards = kind == CustomLevelRewardPackKind.Bonus ? BonusPackRewards : FactionPackRewards;
		if (rewards.Count == 0)
			return CustomLevelRewardPlan.Skipped(
				kind,
				player,
				accountCreationLocalTime,
				receivedPlayerId,
				storeReceivingPlayerSucceeded,
				CustomLevelRewardPlanStatus.NoRewardsConfigured
			);
		if (player.Level != TargetLevel)
			return CustomLevelRewardPlan.Skipped(
				kind,
				player,
				accountCreationLocalTime,
				receivedPlayerId,
				storeReceivingPlayerSucceeded,
				CustomLevelRewardPlanStatus.SkippedWrongLevel
			);
		if (player.Mailbox.Count + rewards.Count > MaxMailboxLetters)
			return CustomLevelRewardPlan.Skipped(
				kind,
				player,
				accountCreationLocalTime,
				receivedPlayerId,
				storeReceivingPlayerSucceeded,
				CustomLevelRewardPlanStatus.SkippedMailboxLimit
			);
		if (kind == CustomLevelRewardPackKind.Faction && accountCreationLocalTime is { } creationTime)
		{
			var window = GetFactionCreationWindow(player.Race);
			if (creationTime < window.MinCreationTime)
				return CustomLevelRewardPlan.Skipped(
					kind,
					player,
					accountCreationLocalTime,
					receivedPlayerId,
					storeReceivingPlayerSucceeded,
					CustomLevelRewardPlanStatus.SkippedCreationBeforeWindow
				);
			if (creationTime > window.MaxCreationTime)
				return CustomLevelRewardPlan.Skipped(
					kind,
					player,
					accountCreationLocalTime,
					receivedPlayerId,
					storeReceivingPlayerSucceeded,
					CustomLevelRewardPlanStatus.SkippedCreationAfterWindow
				);
		}
		if (receivedPlayerId > 0)
			return CustomLevelRewardPlan.Skipped(
				kind,
				player,
				accountCreationLocalTime,
				receivedPlayerId,
				storeReceivingPlayerSucceeded,
				CustomLevelRewardPlanStatus.SkippedAlreadyReceived
			);
		if (!storeReceivingPlayerSucceeded)
			return CustomLevelRewardPlan.Skipped(
				kind,
				player,
				accountCreationLocalTime,
				receivedPlayerId,
				storeReceivingPlayerSucceeded,
				CustomLevelRewardPlanStatus.SkippedStoreFailed
			);

		var descriptors =
			kind == CustomLevelRewardPackKind.Bonus ? CreateBonusDescriptors(rewards) : CreateFactionDescriptors(player, rewards, itemTemplates);

		var status = descriptors.Any(descriptor => descriptor.Status == CustomLevelRewardDescriptorStatus.PlannedSystemMail)
			? CustomLevelRewardPlanStatus.Planned
			: CustomLevelRewardPlanStatus.NoDeliverableRewards;

		return new CustomLevelRewardPlan(
			kind,
			status,
			player.ObjectId,
			player.AccountId,
			player.Name,
			player.Race,
			player.Level,
			player.Mailbox.Count,
			accountCreationLocalTime,
			receivedPlayerId,
			storeReceivingPlayerSucceeded,
			descriptors
		);
	}

	private static IReadOnlyList<CustomLevelRewardDescriptor> CreateBonusDescriptors(IReadOnlyList<CustomLevelRewardItem> rewards)
	{
		return rewards
			.Select(reward => new CustomLevelRewardDescriptor(
				reward,
				CustomLevelRewardDescriptorStatus.PlannedSystemMail,
				"BonusPackService.addPlayerCustomReward -> SystemMailService.sendMail",
				TemplateRace: null,
				Notes: "Java stores the receiving player before sending one express system mail per reward."
			))
			.ToArray();
	}

	private static IReadOnlyList<CustomLevelRewardDescriptor> CreateFactionDescriptors(
		Player player,
		IReadOnlyList<CustomLevelRewardItem> rewards,
		ItemTemplateTable? itemTemplates
	)
	{
		var oppositeRace = GetOppositeRace(player.Race);
		return rewards
			.Select(reward =>
			{
				var templateRace = itemTemplates?.GetItemTemplate(reward.ItemId)?.Race;
				var skippedOppositeRace =
					templateRace != null && oppositeRace != null && string.Equals(NormalizeRace(templateRace), oppositeRace, StringComparison.Ordinal);
				var notes = skippedOppositeRace
					? "Java skips faction-pack reward items whose template race is the player's opposite race."
					: "Java sends one express system mail per deliverable faction-pack reward.";
				return new CustomLevelRewardDescriptor(
					reward,
					skippedOppositeRace ? CustomLevelRewardDescriptorStatus.SkippedOppositeRaceItem : CustomLevelRewardDescriptorStatus.PlannedSystemMail,
					skippedOppositeRace
						? "FactionPackService.sendRewards -> ItemTemplate.getRace == player.getOppositeRace"
						: "FactionPackService.sendRewards -> SystemMailService.sendMail",
					templateRace,
					Notes: notes
				);
			})
			.ToArray();
	}

	private static (DateTime MinCreationTime, DateTime MaxCreationTime) GetFactionCreationWindow(string race)
	{
		// Java parity: FactionPackService.sendRewards uses race-specific creation windows.
		return string.Equals(NormalizeRace(race), "ASMODIANS", StringComparison.Ordinal)
			? (AsmodianMinCreationTime, AsmodianMaxCreationTime)
			: (ElyosMinCreationTime, ElyosMaxCreationTime);
	}

	private static string? GetOppositeRace(string race)
	{
		// Java parity: Race.getOppositeRace drives faction-pack template-race filtering.
		return NormalizeRace(race) switch
		{
			"ELYOS" => "ASMODIANS",
			"ASMODIANS" => "ELYOS",
			_ => null,
		};
	}

	private static string NormalizeRace(string? race)
	{
		return race?.Trim().ToUpperInvariant() ?? string.Empty;
	}
}

public sealed record CustomLevelRewardItem(int ItemId, long Count);

public sealed record CustomLevelRewardPlan(
	CustomLevelRewardPackKind Kind,
	CustomLevelRewardPlanStatus Status,
	int ObjectId,
	int AccountId,
	string PlayerName,
	string PlayerRace,
	int PlayerLevel,
	int MailboxLetters,
	DateTime? AccountCreationLocalTime,
	int ReceivedPlayerId,
	bool StoreReceivingPlayerSucceeded,
	IReadOnlyList<CustomLevelRewardDescriptor> Descriptors
)
{
	public bool Applied => Status == CustomLevelRewardPlanStatus.Planned;

	public static CustomLevelRewardPlan MissingPlayer(
		CustomLevelRewardPackKind kind,
		DateTime? accountCreationLocalTime,
		int receivedPlayerId,
		bool storeReceivingPlayerSucceeded
	)
	{
		return new CustomLevelRewardPlan(
			kind,
			CustomLevelRewardPlanStatus.MissingPlayer,
			ObjectId: 0,
			AccountId: 0,
			PlayerName: string.Empty,
			PlayerRace: string.Empty,
			PlayerLevel: 0,
			MailboxLetters: 0,
			accountCreationLocalTime,
			receivedPlayerId,
			storeReceivingPlayerSucceeded,
			Array.Empty<CustomLevelRewardDescriptor>()
		);
	}

	public static CustomLevelRewardPlan Skipped(
		CustomLevelRewardPackKind kind,
		Player player,
		DateTime? accountCreationLocalTime,
		int receivedPlayerId,
		bool storeReceivingPlayerSucceeded,
		CustomLevelRewardPlanStatus status
	)
	{
		return new CustomLevelRewardPlan(
			kind,
			status,
			player.ObjectId,
			player.AccountId,
			player.Name,
			player.Race,
			player.Level,
			player.Mailbox.Count,
			accountCreationLocalTime,
			receivedPlayerId,
			storeReceivingPlayerSucceeded,
			Array.Empty<CustomLevelRewardDescriptor>()
		);
	}
}

public sealed record CustomLevelRewardDescriptor(
	CustomLevelRewardItem Reward,
	CustomLevelRewardDescriptorStatus Status,
	string JavaSource,
	string? TemplateRace,
	bool IsLive = false,
	string? Notes = null
)
{
	public string Sender => CustomLevelRewardPlanService.Sender;

	public string Title => RewardPackTitle();

	public string Body => RewardPackBody();

	public string LetterType => CustomLevelRewardPlanService.LetterType;

	private string RewardPackTitle()
	{
		return JavaSource.StartsWith("BonusPackService.", StringComparison.Ordinal)
			? CustomLevelRewardPlanService.BonusPackTitle
			: CustomLevelRewardPlanService.FactionPackTitle;
	}

	private string RewardPackBody()
	{
		return JavaSource.StartsWith("BonusPackService.", StringComparison.Ordinal)
			? CustomLevelRewardPlanService.BonusPackBody
			: CustomLevelRewardPlanService.FactionPackBody;
	}
}

public enum CustomLevelRewardPackKind
{
	Bonus,
	Faction,
}

public enum CustomLevelRewardPlanStatus
{
	Planned,
	NoRewardsConfigured,
	SkippedWrongLevel,
	SkippedMailboxLimit,
	SkippedCreationBeforeWindow,
	SkippedCreationAfterWindow,
	SkippedAlreadyReceived,
	SkippedStoreFailed,
	NoDeliverableRewards,
	MissingPlayer,
}

public enum CustomLevelRewardDescriptorStatus
{
	PlannedSystemMail,
	SkippedOppositeRaceItem,
}
