using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class QuestRewardService
{
	private readonly GameServerRateOptions _rateOptions;
	private readonly WorldNpcResourceStatsService _resourceStats;

	public QuestRewardService(WorldNpcResourceStatsService resourceStats, GameServerOptions? options = null)
	{
		_resourceStats = resourceStats;
		_rateOptions = options?.Rates ?? new GameServerRateOptions();
	}

	public async ValueTask<QuestDpRewardResult> ApplyDpRewardAsync(
		Player? player,
		int rewardDp,
		int? maxDp = null)
	{
		// Java parity: services/QuestService.giveReward -> if (rewards.getDp() != 0) player.getCommonData().addDp(rewards.getDp()).
		if (player == null)
			return QuestDpRewardResult.MissingPlayer(rewardDp);
		if (rewardDp == 0)
			return QuestDpRewardResult.NoDpReward(player.ObjectId, player.Dp);

		var previousDp = player.Dp;
		var change = await _resourceStats.AddPlayerDpAsync(player, rewardDp, maxDp);
		return QuestDpRewardResult.FromDpChange(change, rewardDp, previousDp);
	}

	public QuestApRewardResult ApplyApReward(
		Player? player,
		int rewardAp,
		bool isNonCountQuest = false,
		IReadOnlyList<float>? apQuestRates = null,
		AbyssPointsAddOptions? abyssPointsOptions = null)
	{
		// Java parity: services/QuestService.giveReward -> rewards.getAp(),
		// Rates.AP_QUEST for non-NON_COUNT quests, then AbyssPointsService.addAp(player, ap).
		if (player == null)
			return QuestApRewardResult.MissingPlayer(rewardAp, isNonCountQuest);
		if (rewardAp == 0)
			return QuestApRewardResult.NoApReward(player.ObjectId, player.AbyssRank.Ap, isNonCountQuest);

		var appliedRewardAp = isNonCountQuest
			? rewardAp
			: ApplyQuestApRate(player.AccountMembership, rewardAp, apQuestRates ?? _rateOptions.ApQuestRates);
		var previousAp = player.AbyssRank.Ap;
		var plan = AbyssPointsService.AddAp(player, appliedRewardAp, abyssPointsOptions);
		return QuestApRewardResult.FromAbyssPointsPlan(
			plan,
			player.ObjectId,
			rewardAp,
			appliedRewardAp,
			isNonCountQuest,
			previousAp);
	}

	public QuestGpRewardResult ApplyGpReward(
		Player? player,
		int rewardGp,
		IReadOnlyList<float>? gpRates = null)
	{
		// Java parity: services/QuestService.giveReward -> rewards.getGp(),
		// Rates.GP, then GloryPointsService.addGp(playerObjectId, gp).
		if (player == null)
			return QuestGpRewardResult.MissingPlayer(rewardGp);
		if (rewardGp == 0)
			return QuestGpRewardResult.NoGpReward(player.ObjectId, player.AbyssRank.Gp);

		var appliedRewardGp = ApplyQuestGpRate(player.AccountMembership, rewardGp, gpRates ?? _rateOptions.GpRates);
		var previousGp = player.AbyssRank.Gp;
		var plan = GloryPointsService.AddGp(player, player.ObjectId, appliedRewardGp);
		return QuestGpRewardResult.FromGloryPointsPlan(
			plan,
			player.ObjectId,
			rewardGp,
			appliedRewardGp,
			previousGp);
	}

	public QuestKinahRewardPlan CreateKinahRewardPlan(
		Player? player,
		IReadOnlyList<InventoryItem> inventoryItems,
		long rewardKinah,
		Func<int>? nextObjectId = null,
		IReadOnlyList<float>? questKinahRates = null,
		long kinahMaxStackCount = long.MaxValue)
	{
		// Java parity: services/QuestService.giveReward -> if (rewards.getKinah() != 0)
		// player.getInventory().increaseKinah(Rates.QUEST_KINAH.calcResult(...), INC_KINAH_QUEST).
		if (player == null)
			return QuestKinahRewardPlan.MissingPlayer(rewardKinah);
		if (rewardKinah == 0)
			return QuestKinahRewardPlan.NoReward(player.ObjectId);

		var appliedKinah = ApplyQuestKinahRate(
			player.AccountMembership,
			rewardKinah,
			questKinahRates ?? _rateOptions.QuestKinahRates);
		var kinahItem = inventoryItems.FirstOrDefault(item =>
			item.ItemId == InventoryItemFactory.KinahItemId && item.Location == QuestKinahRewardPlan.CubeStorageId);

		if (kinahItem == null)
		{
			var objectId = nextObjectId?.Invoke() ?? 0;
			if (objectId == 0)
				return QuestKinahRewardPlan.MissingKinahObjectId(player.ObjectId, rewardKinah, appliedKinah);

			var increase = ApplyJavaIncreaseItemCount(0, appliedKinah, kinahMaxStackCount);
			var created = CreateKinahItem(objectId, player.ObjectId, increase.CurrentCount);
			return appliedKinah > 0
				? QuestKinahRewardPlan.CreatedKinahItem(player.ObjectId, rewardKinah, appliedKinah, created, increase.LeftCount)
				: QuestKinahRewardPlan.CreatedEmptyKinahItem(player.ObjectId, rewardKinah, appliedKinah, created);
		}

		if (appliedKinah <= 0)
		{
			return QuestKinahRewardPlan.NonPositiveAppliedAmount(
				player.ObjectId,
				rewardKinah,
				appliedKinah,
				kinahItem.Count,
				kinahItem);
		}

		var existingIncrease = ApplyJavaIncreaseItemCount(kinahItem.Count, appliedKinah, kinahMaxStackCount);
		return QuestKinahRewardPlan.UpdatedExistingKinahItem(
			player.ObjectId,
			rewardKinah,
			appliedKinah,
			kinahItem.Count,
			CopyInventoryItem(kinahItem, existingIncrease.CurrentCount),
			existingIncrease.LeftCount);
	}

	public static long ApplyQuestKinahRate(byte membershipLevel, long rewardKinah, IReadOnlyList<float> questKinahRates)
	{
		// Java parity: model/gameobjects/player/Rates.QUEST_KINAH.calcResult uses long * float,
		// float precision, and Java primitive narrowing from float to long.
		var product = (float)rewardKinah * SelectMembershipRate(membershipLevel, questKinahRates);
		return JavaFloatToLong(product);
	}

	public static int ApplyQuestApRate(byte membershipLevel, int rewardAp, IReadOnlyList<float> apQuestRates)
	{
		// Java parity: model/gameobjects/player/Rates.AP_QUEST.calcResult.
		var result = (long)(rewardAp * SelectMembershipRate(membershipLevel, apQuestRates));
		return JavaLongToIntOrOriginal(result, rewardAp);
	}

	public static int ApplyQuestGpRate(byte membershipLevel, int rewardGp, IReadOnlyList<float> gpRates)
	{
		// Java parity: model/gameobjects/player/Rates.GP.calcResult.
		var result = (long)(rewardGp * SelectMembershipRate(membershipLevel, gpRates));
		return JavaLongToIntOrOriginal(result, rewardGp);
	}

	private static long JavaFloatToLong(float value)
	{
		if (float.IsNaN(value))
			return 0;
		if (value >= 9.223372036854776E18f)
			return long.MaxValue;
		if (value <= -9.223372036854776E18f)
			return long.MinValue;
		return (long)value;
	}

	private static JavaIncreaseItemCountResult ApplyJavaIncreaseItemCount(long currentCount, long count, long cap)
	{
		if (count <= 0)
			return new JavaIncreaseItemCountResult(currentCount, 0);

		// Java parity: model/gameobjects/Item.increaseItemCount uses primitive long addition
		// before comparing against the item template max-stack cap.
		var addedToCurrent = unchecked(currentCount + count);
		var addCount = addedToCurrent > cap
			? unchecked(cap - currentCount)
			: count;
		var newCount = addCount != 0
			? unchecked(currentCount + addCount)
			: currentCount;
		return new JavaIncreaseItemCountResult(newCount, unchecked(count - addCount));
	}

	private static float SelectMembershipRate(byte membershipLevel, IReadOnlyList<float> rates)
	{
		// Java parity: model/gameobjects/player/Rates.get returns 1 when the configured rate array is empty.
		if (rates.Count == 0)
			return 1f;

		return rates[Math.Min(rates.Count - 1, membershipLevel)];
	}

	private static InventoryItem CreateKinahItem(int objectId, int ownerId, long count)
	{
		// Java parity: model/items/storage/Storage.increaseKinah creates ItemId.KINAH with count 0
		// before applying a positive count increase. This planner keeps that creation visible.
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = InventoryItemFactory.KinahItemId,
			Count = count,
			OwnerId = ownerId,
			Location = QuestKinahRewardPlan.CubeStorageId,
			Slot = QuestKinahRewardPlan.FirstAvailableSlot,
		};
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, long count)
	{
		return new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = count,
			Color = item.Color,
			ColorExpires = item.ColorExpires,
			Creator = item.Creator,
			ExpireTime = item.ExpireTime,
			ActivationCount = item.ActivationCount,
			OwnerId = item.OwnerId,
			IsEquipped = item.IsEquipped,
			IsSoulBound = item.IsSoulBound,
			Slot = item.Slot,
			Location = item.Location,
			Enchant = item.Enchant,
			EnchantBonus = item.EnchantBonus,
			ItemSkin = item.ItemSkin,
			FusionedItem = item.FusionedItem,
			OptionalSocket = item.OptionalSocket,
			OptionalFusionSocket = item.OptionalFusionSocket,
			Charge = item.Charge,
			TuneCount = item.TuneCount,
			RandomBonus = item.RandomBonus,
			FusionRandomBonus = item.FusionRandomBonus,
			Tempering = item.Tempering,
			PackCount = item.PackCount,
			IsAmplified = item.IsAmplified,
			BuffSkill = item.BuffSkill,
			RandomPlumeBonus = item.RandomPlumeBonus,
			ManaStones = item.ManaStones,
			FusionStones = item.FusionStones,
			Godstone = item.Godstone,
			IdianStone = item.IdianStone,
		};
	}

	private static int JavaLongToIntOrOriginal(long value, int original)
	{
		// Java parity: Rates.calcResult(int) returns the original value if Math.toIntExact overflows.
		if (value is < int.MinValue or > int.MaxValue)
			return original;
		return (int)value;
	}

	private readonly record struct JavaIncreaseItemCountResult(long CurrentCount, long LeftCount);
}

public sealed record QuestDpRewardResult(
	QuestDpRewardStatus Status,
	int ObjectId,
	int RewardDp,
	int PreviousDp,
	int CurrentDp,
	WorldNpcResourceChangeResult? Change = null)
{
	public static QuestDpRewardResult MissingPlayer(int rewardDp)
	{
		return new QuestDpRewardResult(
			QuestDpRewardStatus.MissingPlayer,
			0,
			rewardDp,
			0,
			0);
	}

	public static QuestDpRewardResult NoDpReward(int objectId, int currentDp)
	{
		return new QuestDpRewardResult(
			QuestDpRewardStatus.NoDpReward,
			objectId,
			0,
			currentDp,
			currentDp);
	}

	public static QuestDpRewardResult FromDpChange(
		WorldNpcResourceChangeResult change,
		int rewardDp,
		int previousDp)
	{
		var status = change.Status is WorldNpcResourceChangeStatus.StartingClass
			or WorldNpcResourceChangeStatus.MissingTarget
			or WorldNpcResourceChangeStatus.MissingMaxResource
			? QuestDpRewardStatus.DpBoundarySkipped
			: QuestDpRewardStatus.Applied;
		return new QuestDpRewardResult(
			status,
			change.ObjectId,
			rewardDp,
			previousDp,
			change.CurrentValue,
			change);
	}
}

public enum QuestDpRewardStatus
{
	Applied,
	MissingPlayer,
	NoDpReward,
	DpBoundarySkipped,
}

public sealed record QuestApRewardResult(
	QuestApRewardStatus Status,
	int ObjectId,
	int RewardAp,
	int AppliedRewardAp,
	bool IsNonCountQuest,
	int PreviousAp,
	int CurrentAp,
	AbyssPointsAddPlan? AbyssPointsPlan = null)
{
	public static QuestApRewardResult MissingPlayer(int rewardAp, bool isNonCountQuest)
	{
		return new QuestApRewardResult(
			QuestApRewardStatus.MissingPlayer,
			0,
			rewardAp,
			0,
			isNonCountQuest,
			0,
			0);
	}

	public static QuestApRewardResult NoApReward(int objectId, int currentAp, bool isNonCountQuest)
	{
		return new QuestApRewardResult(
			QuestApRewardStatus.NoApReward,
			objectId,
			0,
			0,
			isNonCountQuest,
			currentAp,
			currentAp);
	}

	public static QuestApRewardResult FromAbyssPointsPlan(
		AbyssPointsAddPlan plan,
		int objectId,
		int rewardAp,
		int appliedRewardAp,
		bool isNonCountQuest,
		int previousAp)
	{
		return new QuestApRewardResult(
			plan.Applied ? QuestApRewardStatus.Applied : QuestApRewardStatus.ApBoundarySkipped,
			objectId,
			rewardAp,
			appliedRewardAp,
			isNonCountQuest,
			previousAp,
			plan.UpdatedRank?.Ap ?? previousAp,
			plan);
	}
}

public enum QuestApRewardStatus
{
	Applied,
	MissingPlayer,
	NoApReward,
	ApBoundarySkipped,
}

public sealed record QuestGpRewardResult(
	QuestGpRewardStatus Status,
	int ObjectId,
	int RewardGp,
	int AppliedRewardGp,
	int PreviousGp,
	int CurrentGp,
	GloryPointsAddPlan? GloryPointsPlan = null)
{
	public static QuestGpRewardResult MissingPlayer(int rewardGp)
	{
		return new QuestGpRewardResult(
			QuestGpRewardStatus.MissingPlayer,
			0,
			rewardGp,
			0,
			0,
			0);
	}

	public static QuestGpRewardResult NoGpReward(int objectId, int currentGp)
	{
		return new QuestGpRewardResult(
			QuestGpRewardStatus.NoGpReward,
			objectId,
			0,
			0,
			currentGp,
			currentGp);
	}

	public static QuestGpRewardResult FromGloryPointsPlan(
		GloryPointsAddPlan plan,
		int objectId,
		int rewardGp,
		int appliedRewardGp,
		int previousGp)
	{
		return new QuestGpRewardResult(
			plan.Applied ? QuestGpRewardStatus.Applied : QuestGpRewardStatus.GpBoundarySkipped,
			objectId,
			rewardGp,
			appliedRewardGp,
			previousGp,
			plan.UpdatedRank?.Gp ?? previousGp,
			plan);
	}
}

public enum QuestGpRewardStatus
{
	Applied,
	MissingPlayer,
	NoGpReward,
	GpBoundarySkipped,
}

public sealed record QuestKinahRewardPlan(
	QuestKinahRewardStatus Status,
	int ObjectId,
	long RewardKinah,
	long AppliedKinah,
	long PreviousKinah,
	long CurrentKinah,
	bool CreatesMissingKinahItem,
	InventoryItem? KinahItemUpdate,
	int PacketUpdateType,
	long OverflowRemainder,
	string JavaSource)
{
	public const int CubeStorageId = 0;
	public const long FirstAvailableSlot = 65535;

	public static QuestKinahRewardPlan MissingPlayer(long rewardKinah)
	{
		return new QuestKinahRewardPlan(
			QuestKinahRewardStatus.MissingPlayer,
			0,
			rewardKinah,
			0,
			0,
			0,
			false,
			null,
			SmInventoryUpdateItem.IncreaseKinahQuest,
			0,
			"QuestService.giveReward -> Storage.increaseKinah");
	}

	public static QuestKinahRewardPlan NoReward(int objectId)
	{
		return new QuestKinahRewardPlan(
			QuestKinahRewardStatus.NoReward,
			objectId,
			0,
			0,
			0,
			0,
			false,
			null,
			SmInventoryUpdateItem.IncreaseKinahQuest,
			0,
			"QuestService.giveReward skips raw zero kinah");
	}

	public static QuestKinahRewardPlan MissingKinahObjectId(int objectId, long rewardKinah, long appliedKinah)
	{
		return new QuestKinahRewardPlan(
			QuestKinahRewardStatus.MissingKinahObjectId,
			objectId,
			rewardKinah,
			appliedKinah,
			0,
			0,
			true,
			null,
			SmInventoryUpdateItem.IncreaseKinahQuest,
			0,
			"Storage.increaseKinah requires ItemFactory.newItem(ItemId.KINAH, 0) when missing");
	}

	public static QuestKinahRewardPlan CreatedKinahItem(
		int objectId,
		long rewardKinah,
		long appliedKinah,
		InventoryItem kinahItem,
		long overflowRemainder)
	{
		return new QuestKinahRewardPlan(
			QuestKinahRewardStatus.CreatedKinahItem,
			objectId,
			rewardKinah,
			appliedKinah,
			0,
			kinahItem.Count,
			true,
			kinahItem,
			SmInventoryUpdateItem.IncreaseKinahQuest,
			overflowRemainder,
			"Storage.increaseKinah creates missing kinah then increases positive amount");
	}

	public static QuestKinahRewardPlan CreatedEmptyKinahItem(
		int objectId,
		long rewardKinah,
		long appliedKinah,
		InventoryItem kinahItem)
	{
		return new QuestKinahRewardPlan(
			QuestKinahRewardStatus.NonPositiveAppliedAmountCreatedKinahItem,
			objectId,
			rewardKinah,
			appliedKinah,
			0,
			0,
			true,
			kinahItem,
			SmInventoryUpdateItem.IncreaseKinahQuest,
			0,
			"Storage.increaseKinah creates missing kinah before amount > 0 guard");
	}

	public static QuestKinahRewardPlan NonPositiveAppliedAmount(
		int objectId,
		long rewardKinah,
		long appliedKinah,
		long currentKinah,
		InventoryItem kinahItem)
	{
		return new QuestKinahRewardPlan(
			QuestKinahRewardStatus.NonPositiveAppliedAmountExistingKinahItem,
			objectId,
			rewardKinah,
			appliedKinah,
			currentKinah,
			currentKinah,
			false,
			null,
			SmInventoryUpdateItem.IncreaseKinahQuest,
			0,
			"Storage.increaseKinah applies only amount > 0 after ensuring kinah item exists");
	}

	public static QuestKinahRewardPlan UpdatedExistingKinahItem(
		int objectId,
		long rewardKinah,
		long appliedKinah,
		long previousKinah,
		InventoryItem kinahItem,
		long overflowRemainder)
	{
		return new QuestKinahRewardPlan(
			QuestKinahRewardStatus.UpdatedExistingKinahItem,
			objectId,
			rewardKinah,
			appliedKinah,
			previousKinah,
			kinahItem.Count,
			false,
			kinahItem,
			SmInventoryUpdateItem.IncreaseKinahQuest,
			overflowRemainder,
			"Storage.increaseKinah -> Item.increaseItemCount with kinah item template cap");
	}
}

public enum QuestKinahRewardStatus
{
	CreatedKinahItem,
	UpdatedExistingKinahItem,
	NonPositiveAppliedAmountCreatedKinahItem,
	NonPositiveAppliedAmountExistingKinahItem,
	MissingPlayer,
	MissingKinahObjectId,
	NoReward,
}
