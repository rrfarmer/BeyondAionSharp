using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class StarterKitLevelChangePlanService
{
	// Java parity: services/reward/StarterKitService reward buckets and mail metadata are modeled
	// here as a non-live planner so level-change call sites can reuse the Java item tables.
	public const string Sender = "Beyond Aion";
	public const string Title = "Starter Kit";
	public const string MailBody =
		"Greetings Daeva!\n\n"
		+ "In gratitude for your decision to join our server, we would like to support you with an additional item pack during the leveling.\n\n"
		+ "Enjoy your stay on Beyond Aion!";

	private static readonly IReadOnlyDictionary<int, IReadOnlyList<StarterKitRewardItem>> RewardsByLevel = new Dictionary<
		int,
		IReadOnlyList<StarterKitRewardItem>
	>
	{
		[1] = [new StarterKitRewardItem(169610056, 1)],
		[20] =
		[
			new StarterKitRewardItem(188054100, 1),
			new StarterKitRewardItem(125001832, 1),
			new StarterKitRewardItem(122000449, 1),
			new StarterKitRewardItem(122000451, 1),
			new StarterKitRewardItem(120015052, 1),
			new StarterKitRewardItem(120015051, 1),
			new StarterKitRewardItem(123000879, 1),
		],
		[25] =
		[
			new StarterKitRewardItem(190100032, 1),
			new StarterKitRewardItem(164002272, 25),
			new StarterKitRewardItem(162000039, 25),
			new StarterKitRewardItem(162002018, 25),
		],
		[35] = [new StarterKitRewardItem(188054101, 1), new StarterKitRewardItem(169620082, 1), new StarterKitRewardItem(169620094, 1)],
		[50] =
		[
			new StarterKitRewardItem(121000815, 1),
			new StarterKitRewardItem(120000901, 1),
			new StarterKitRewardItem(122001038, 1),
			new StarterKitRewardItem(188053624, 10),
			new StarterKitRewardItem(161001001, 5),
		],
		[60] =
		[
			new StarterKitRewardItem(169620072, 1),
			new StarterKitRewardItem(162002030, 100),
			new StarterKitRewardItem(162002018, 50),
			new StarterKitRewardItem(188053526, 5),
			new StarterKitRewardItem(188053783, 5),
		],
	};

	public static IReadOnlyDictionary<int, IReadOnlyList<StarterKitRewardItem>> RewardBuckets => RewardsByLevel;

	public static StarterKitLevelChangePlan CreatePlan(Player? player, bool starterKitEnabled, int fromLevel, int toLevel)
	{
		// Java parity: StarterKitService.onLevelUp scans every level crossed in the update and sends
		// one express system mail per configured reward item for matching buckets.
		if (player == null)
			return StarterKitLevelChangePlan.MissingPlayer(starterKitEnabled, fromLevel, toLevel);
		if (!starterKitEnabled)
			return StarterKitLevelChangePlan.Skipped(player, StarterKitLevelChangePlanStatus.SkippedDisabled, starterKitEnabled, fromLevel, toLevel);
		if (fromLevel > toLevel)
			return StarterKitLevelChangePlan.Skipped(player, StarterKitLevelChangePlanStatus.EmptyLevelRange, starterKitEnabled, fromLevel, toLevel);

		var descriptors = new List<StarterKitLevelChangeDescriptor>();
		for (var level = fromLevel; level <= toLevel; level++)
		{
			if (!RewardsByLevel.TryGetValue(level, out var rewards))
				continue;

			foreach (var reward in rewards)
			{
				descriptors.Add(
					new StarterKitLevelChangeDescriptor(
						level,
						reward,
						StarterKitLevelChangeDescriptorStatus.PlannedSystemMail,
						"StarterKitService.onLevelUp -> SystemMailService.sendMail",
						Notes: "Future live execution must send express system mail with the Java starter-kit sender/title/body."
					)
				);
			}
		}

		return new StarterKitLevelChangePlan(
			descriptors.Count == 0 ? StarterKitLevelChangePlanStatus.NoMatchingRewards : StarterKitLevelChangePlanStatus.Planned,
			player.ObjectId,
			player.Name,
			starterKitEnabled,
			fromLevel,
			toLevel,
			descriptors
		);
	}
}

public sealed record StarterKitRewardItem(int ItemId, long Count);

public sealed record StarterKitLevelChangePlan(
	StarterKitLevelChangePlanStatus Status,
	int ObjectId,
	string PlayerName,
	bool StarterKitEnabled,
	int FromLevel,
	int ToLevel,
	IReadOnlyList<StarterKitLevelChangeDescriptor> Descriptors
)
{
	public bool Applied => Status == StarterKitLevelChangePlanStatus.Planned;

	public static StarterKitLevelChangePlan MissingPlayer(bool starterKitEnabled, int fromLevel, int toLevel)
	{
		return new StarterKitLevelChangePlan(
			StarterKitLevelChangePlanStatus.MissingPlayer,
			ObjectId: 0,
			PlayerName: string.Empty,
			starterKitEnabled,
			fromLevel,
			toLevel,
			Array.Empty<StarterKitLevelChangeDescriptor>()
		);
	}

	public static StarterKitLevelChangePlan Skipped(
		Player player,
		StarterKitLevelChangePlanStatus status,
		bool starterKitEnabled,
		int fromLevel,
		int toLevel
	)
	{
		return new StarterKitLevelChangePlan(
			status,
			player.ObjectId,
			player.Name,
			starterKitEnabled,
			fromLevel,
			toLevel,
			Array.Empty<StarterKitLevelChangeDescriptor>()
		);
	}
}

public sealed record StarterKitLevelChangeDescriptor(
	int Level,
	StarterKitRewardItem Reward,
	StarterKitLevelChangeDescriptorStatus Status,
	string JavaSource,
	bool IsLive = false,
	string? Notes = null
)
{
	public string Sender => StarterKitLevelChangePlanService.Sender;

	public string Title => StarterKitLevelChangePlanService.Title;

	public string Body => StarterKitLevelChangePlanService.MailBody;

	public string LetterType => "EXPRESS";
}

public enum StarterKitLevelChangePlanStatus
{
	Planned,
	SkippedDisabled,
	EmptyLevelRange,
	NoMatchingRewards,
	MissingPlayer,
}

public enum StarterKitLevelChangeDescriptorStatus
{
	PlannedSystemMail,
}
