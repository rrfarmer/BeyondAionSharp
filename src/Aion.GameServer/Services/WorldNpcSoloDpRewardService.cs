using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class WorldNpcSoloDpRewardService
{
	private readonly WorldNpcResourceStatsService _resourceStats;

	public WorldNpcSoloDpRewardService(WorldNpcResourceStatsService resourceStats)
	{
		_resourceStats = resourceStats;
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
