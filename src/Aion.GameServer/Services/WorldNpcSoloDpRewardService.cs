using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class WorldNpcSoloDpRewardService
{
	private static readonly float[] DefaultApPveRates = [1f, 2f];
	private readonly GameServerRateOptions _rateOptions;
	private readonly WorldNpcResourceStatsService _resourceStats;

	public WorldNpcSoloDpRewardService(WorldNpcResourceStatsService resourceStats, GameServerOptions? options = null)
	{
		_resourceStats = resourceStats;
		_rateOptions = options?.Rates ?? new GameServerRateOptions();
	}

	public async ValueTask<WorldNpcSoloDpRewardResult> ApplySoloDpRewardAsync(
		Player? player,
		IWorldNpcObject? npc,
		float damagePercent,
		int? maxDp = null,
		float dpPveRate = 1f)
	{
		// Java parity: controllers/NpcController.doReward solo-player branch -> calculateDPReward, scale by damage percent, addDp.
		if (player == null)
			return WorldNpcSoloDpRewardResult.MissingPlayer(npc?.ObjectId ?? 0, damagePercent);
		if (npc == null)
			return WorldNpcSoloDpRewardResult.MissingNpc(player.ObjectId, player.Dp, damagePercent);
		if (IsPlayerDead(player))
			return WorldNpcSoloDpRewardResult.PlayerDead(player.ObjectId, npc.ObjectId, player.Dp, damagePercent);

		var baseRewardDp = CalculateDpReward(player.Level, npc.Template, dpPveRate);
		var rewardDp = ScaleRewardByDamagePercent(baseRewardDp, damagePercent);
		var previousDp = player.Dp;
		var change = await _resourceStats.AddPlayerDpAsync(player, rewardDp, maxDp);
		return WorldNpcSoloDpRewardResult.FromDpChange(
			change,
			npc.ObjectId,
			baseRewardDp,
			damagePercent,
			rewardDp,
			previousDp);
	}

	public WorldNpcSoloApRewardResult ApplySoloApReward(
		Player? player,
		IWorldNpcObject? npc,
		float damagePercent,
		bool shouldRewardAp,
		int calculatedAp,
		float apMultiplier = 1f,
		AbyssPointsAddOptions? abyssPointsOptions = null,
		bool sourceIsSiegeNpc = false,
		bool sourceSiegeNpcPeace = false)
	{
		// Java parity: controllers/NpcController.doReward solo-player AP branch -> AIQuestion.REWARD_AP,
		// StatFunctions.calculatePvEApGained, then AbyssPointsService.addAp(player, npc, rewardAp).
		if (player == null)
			return WorldNpcSoloApRewardResult.MissingPlayer(npc?.ObjectId ?? 0, damagePercent, calculatedAp, apMultiplier);
		if (npc == null)
			return WorldNpcSoloApRewardResult.MissingNpc(player.ObjectId, player.AbyssRank.Ap, damagePercent, calculatedAp, apMultiplier);
		if (IsPlayerDead(player))
		{
			return WorldNpcSoloApRewardResult.PlayerDead(
				player.ObjectId,
				npc.ObjectId,
				player.AbyssRank.Ap,
				damagePercent,
				calculatedAp,
				apMultiplier);
		}

		if (!shouldRewardAp)
		{
			return WorldNpcSoloApRewardResult.ApRewardDenied(
				player.ObjectId,
				npc.ObjectId,
				player.AbyssRank.Ap,
				damagePercent,
				calculatedAp,
				apMultiplier);
		}

		var rewardAp = CalculateSoloApReward(calculatedAp, damagePercent, apMultiplier);
		if (rewardAp <= 0)
		{
			return WorldNpcSoloApRewardResult.NoApReward(
				player.ObjectId,
				npc.ObjectId,
				player.AbyssRank.Ap,
				damagePercent,
				calculatedAp,
				apMultiplier);
		}

		var previousAp = player.AbyssRank.Ap;
		var plan = AbyssPointsService.AddApFromObject(
			player,
			npc.ObjectId,
			sourceIsPlayer: false,
			sourceIsSiegeNpc,
			sourceSiegeNpcPeace,
			rewardAp,
			abyssPointsOptions);
		return WorldNpcSoloApRewardResult.FromAbyssPointsPlan(
			plan,
			player.ObjectId,
			npc.ObjectId,
			damagePercent,
			calculatedAp,
			apMultiplier,
			rewardAp,
			previousAp);
	}

	public WorldNpcSoloApRewardResult ApplySoloApRewardFromNpcStats(
		Player? player,
		IWorldNpcObject? npc,
		float damagePercent,
		bool shouldRewardAp,
		float apMultiplier = 1f,
		IReadOnlyList<float>? apPveRates = null,
		int apBoostStat = 100,
		AbyssPointsAddOptions? abyssPointsOptions = null,
		bool sourceIsSiegeNpc = false,
		bool sourceSiegeNpcPeace = false)
	{
		// Java parity: NpcController.doReward asks StatFunctions.calculatePvEApGained before scaling rewardAp.
		// RatesConfig.AP_PVE_RATES reaches StatFunctions through Rates.AP_PVE.calcResult.
		var configuredApPveRates = apPveRates ?? _rateOptions.ApPveRates;
		var calculatedAp = player != null && npc != null
			? CalculatePveApGained(player, npc.Template, configuredApPveRates, apBoostStat)
			: 0;
		return ApplySoloApReward(
			player,
			npc,
			damagePercent,
			shouldRewardAp,
			calculatedAp,
			apMultiplier,
			abyssPointsOptions,
			sourceIsSiegeNpc,
			sourceSiegeNpcPeace);
	}

	public static int CalculateSoloApReward(int calculatedAp, float damagePercent, float apMultiplier = 1f)
	{
		// Java parity: NpcController.doReward keeps rewardAp as float and casts to int only after >= 1.
		var rewardAp = 1f;
		rewardAp *= damagePercent;
		rewardAp *= apMultiplier;
		rewardAp *= calculatedAp;
		return rewardAp >= 1f ? JavaFloatToInt(rewardAp) : 0;
	}

	public static int CalculatePveApGained(
		Player player,
		NpcTemplateSummary npcTemplate,
		IReadOnlyList<float>? apPveRates = null,
		int apBoostStat = 100)
	{
		// Java parity: utils/stats/StatFunctions.calculatePvEApGained + Rates.AP_PVE.
		if (player.Level - npcTemplate.Level > 10)
			return 1;

		float apNpcRate = GetApNpcRating(npcTemplate.Rating);
		if (string.Equals(npcTemplate.Name, "flame hoverstone", StringComparison.Ordinal))
			apNpcRate = 0.5f;

		var baseAp = JavaFloatToInt((float)Math.Floor(15f * apNpcRate));
		var membershipRate = SelectMembershipRate(player.AccountMembership, apPveRates ?? DefaultApPveRates);
		var statRate = apBoostStat / 100f;
		var result = (long)(baseAp * membershipRate * statRate);
		return JavaLongToIntOrOriginal(result, baseAp);
	}

	public static int CalculateDpReward(int playerLevel, NpcTemplateSummary npcTemplate, float dpPveRate = 1f)
	{
		// Java parity: utils/stats/StatFunctions.calculateDPReward.
		var baseDp = npcTemplate.Level * GetRatingMultiplier(npcTemplate.Rating);
		var xpPercentage = GetXpRewardPercent(npcTemplate.Level - playerLevel);
		var rewardBeforeRates = (int)Math.Floor(baseDp * xpPercentage / 100f);
		return JavaFloatToInt(rewardBeforeRates * dpPveRate);
	}

	public static int ScaleRewardByDamagePercent(int rewardDp, float damagePercent)
	{
		// Java compound assignment `rewardDp *= percentage` narrows the float result back to int.
		return JavaFloatToInt(rewardDp * damagePercent);
	}

	public static int GetXpRewardPercent(int levelDifference)
	{
		// Java parity: utils/stats/XPRewardEnum.xpRewardFrom.
		return levelDifference switch
		{
			< -11 => 0,
			-11 => 0,
			-10 => 1,
			-9 => 10,
			-8 => 20,
			-7 => 30,
			-6 => 40,
			-5 => 50,
			-4 => 60,
			-3 => 90,
			-2 or -1 or 0 => 100,
			1 => 105,
			2 => 110,
			3 => 115,
			_ => 120,
		};
	}

	public static int GetRatingMultiplier(string npcRating)
	{
		// Java parity: utils/stats/StatFunctions.calculateRatingMultiplier.
		return npcRating.ToUpperInvariant() switch
		{
			"JUNK" or "NORMAL" => 2,
			"ELITE" => 3,
			"HERO" => 4,
			"LEGENDARY" => 5,
			_ => 1,
		};
	}

	public static int GetApNpcRating(string npcRating)
	{
		// Java parity: utils/stats/StatFunctions.getApNpcRating.
		return npcRating.ToUpperInvariant() switch
		{
			"JUNK" => 1,
			"NORMAL" => 2,
			"ELITE" => 4,
			"HERO" => 35,
			"LEGENDARY" => 2500,
			_ => 1,
		};
	}

	private static float SelectMembershipRate(byte membershipLevel, IReadOnlyList<float> rates)
	{
		// Java parity: model/gameobjects/player/Rates.get returns 1 when the configured rate array is empty.
		if (rates.Count == 0)
			return 1f;

		return rates[Math.Min(rates.Count - 1, membershipLevel)];
	}

	private static bool IsPlayerDead(Player player)
	{
		return player.LifeStats?.CurrentHp <= 0 || player.CreatureState == PlayerCreatureState.Dead;
	}

	private static int JavaFloatToInt(float value)
	{
		if (float.IsNaN(value))
			return 0;
		if (value <= int.MinValue)
			return int.MinValue;
		if (value >= int.MaxValue)
			return int.MaxValue;
		return (int)value;
	}

	private static int JavaLongToIntOrOriginal(long value, int original)
	{
		// Java parity: Rates.calcResult(int) returns the original value if Math.toIntExact overflows.
		if (value is < int.MinValue or > int.MaxValue)
			return original;
		return (int)value;
	}
}

public sealed record WorldNpcSoloDpRewardResult(
	WorldNpcSoloDpRewardStatus Status,
	int ObjectId,
	int NpcObjectId,
	int BaseRewardDp,
	float DamagePercent,
	int RewardDp,
	int PreviousDp,
	int CurrentDp,
	WorldNpcResourceChangeResult? Change = null)
{
	public static WorldNpcSoloDpRewardResult MissingPlayer(int npcObjectId, float damagePercent)
	{
		return new WorldNpcSoloDpRewardResult(
			WorldNpcSoloDpRewardStatus.MissingPlayer,
			0,
			npcObjectId,
			0,
			damagePercent,
			0,
			0,
			0);
	}

	public static WorldNpcSoloDpRewardResult MissingNpc(int objectId, int currentDp, float damagePercent)
	{
		return new WorldNpcSoloDpRewardResult(
			WorldNpcSoloDpRewardStatus.MissingNpc,
			objectId,
			0,
			0,
			damagePercent,
			0,
			currentDp,
			currentDp);
	}

	public static WorldNpcSoloDpRewardResult PlayerDead(
		int objectId,
		int npcObjectId,
		int currentDp,
		float damagePercent)
	{
		return new WorldNpcSoloDpRewardResult(
			WorldNpcSoloDpRewardStatus.PlayerDead,
			objectId,
			npcObjectId,
			0,
			damagePercent,
			0,
			currentDp,
			currentDp);
	}

	public static WorldNpcSoloDpRewardResult FromDpChange(
		WorldNpcResourceChangeResult change,
		int npcObjectId,
		int baseRewardDp,
		float damagePercent,
		int rewardDp,
		int previousDp)
	{
		var status = change.Status is WorldNpcResourceChangeStatus.StartingClass
			or WorldNpcResourceChangeStatus.MissingTarget
			or WorldNpcResourceChangeStatus.MissingMaxResource
			? WorldNpcSoloDpRewardStatus.DpBoundarySkipped
			: WorldNpcSoloDpRewardStatus.Applied;
		return new WorldNpcSoloDpRewardResult(
			status,
			change.ObjectId,
			npcObjectId,
			baseRewardDp,
			damagePercent,
			rewardDp,
			previousDp,
			change.CurrentValue,
			change);
	}
}

public enum WorldNpcSoloDpRewardStatus
{
	Applied,
	MissingPlayer,
	MissingNpc,
	PlayerDead,
	DpBoundarySkipped,
}

public sealed record WorldNpcSoloApRewardResult(
	WorldNpcSoloApRewardStatus Status,
	int ObjectId,
	int NpcObjectId,
	float DamagePercent,
	int CalculatedAp,
	float ApMultiplier,
	int RewardAp,
	int PreviousAp,
	int CurrentAp,
	AbyssPointsAddPlan? AbyssPointsPlan = null)
{
	public static WorldNpcSoloApRewardResult MissingPlayer(
		int npcObjectId,
		float damagePercent,
		int calculatedAp,
		float apMultiplier)
	{
		return new WorldNpcSoloApRewardResult(
			WorldNpcSoloApRewardStatus.MissingPlayer,
			0,
			npcObjectId,
			damagePercent,
			calculatedAp,
			apMultiplier,
			0,
			0,
			0);
	}

	public static WorldNpcSoloApRewardResult MissingNpc(
		int objectId,
		int currentAp,
		float damagePercent,
		int calculatedAp,
		float apMultiplier)
	{
		return new WorldNpcSoloApRewardResult(
			WorldNpcSoloApRewardStatus.MissingNpc,
			objectId,
			0,
			damagePercent,
			calculatedAp,
			apMultiplier,
			0,
			currentAp,
			currentAp);
	}

	public static WorldNpcSoloApRewardResult PlayerDead(
		int objectId,
		int npcObjectId,
		int currentAp,
		float damagePercent,
		int calculatedAp,
		float apMultiplier)
	{
		return new WorldNpcSoloApRewardResult(
			WorldNpcSoloApRewardStatus.PlayerDead,
			objectId,
			npcObjectId,
			damagePercent,
			calculatedAp,
			apMultiplier,
			0,
			currentAp,
			currentAp);
	}

	public static WorldNpcSoloApRewardResult ApRewardDenied(
		int objectId,
		int npcObjectId,
		int currentAp,
		float damagePercent,
		int calculatedAp,
		float apMultiplier)
	{
		return new WorldNpcSoloApRewardResult(
			WorldNpcSoloApRewardStatus.ApRewardDenied,
			objectId,
			npcObjectId,
			damagePercent,
			calculatedAp,
			apMultiplier,
			0,
			currentAp,
			currentAp);
	}

	public static WorldNpcSoloApRewardResult NoApReward(
		int objectId,
		int npcObjectId,
		int currentAp,
		float damagePercent,
		int calculatedAp,
		float apMultiplier)
	{
		return new WorldNpcSoloApRewardResult(
			WorldNpcSoloApRewardStatus.NoApReward,
			objectId,
			npcObjectId,
			damagePercent,
			calculatedAp,
			apMultiplier,
			0,
			currentAp,
			currentAp);
	}

	public static WorldNpcSoloApRewardResult FromAbyssPointsPlan(
		AbyssPointsAddPlan plan,
		int objectId,
		int npcObjectId,
		float damagePercent,
		int calculatedAp,
		float apMultiplier,
		int rewardAp,
		int previousAp)
	{
		return new WorldNpcSoloApRewardResult(
			plan.Applied ? WorldNpcSoloApRewardStatus.Applied : WorldNpcSoloApRewardStatus.ApBoundarySkipped,
			objectId,
			npcObjectId,
			damagePercent,
			calculatedAp,
			apMultiplier,
			rewardAp,
			previousAp,
			plan.UpdatedRank?.Ap ?? previousAp,
			plan);
	}
}

public enum WorldNpcSoloApRewardStatus
{
	Applied,
	MissingPlayer,
	MissingNpc,
	PlayerDead,
	ApRewardDenied,
	NoApReward,
	ApBoundarySkipped,
}
