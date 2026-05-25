using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class WorldNpcTeamApRewardService
{
	private readonly GameServerRateOptions _rateOptions;

	public WorldNpcTeamApRewardService(GameServerOptions? options = null)
	{
		_rateOptions = options?.Rates ?? new GameServerRateOptions();
	}

	public WorldNpcTeamApRewardResult ApplyMemberApRewardFromNpcStats(
		Player? member,
		IWorldNpcObject? npc,
		float damagePercent,
		float instanceApMultiplier,
		int eligiblePlayerCount,
		bool shouldRewardAp,
		bool suppressApForMentorGroup = false,
		IReadOnlyList<float>? apPveRates = null,
		int apBoostStat = 100,
		AbyssPointsAddOptions? abyssPointsOptions = null,
		bool sourceIsSiegeNpc = false,
		bool sourceSiegeNpcPeace = false)
	{
		// Java parity: model/team/common/service/PlayerTeamDistributionService.doReward AP loop.
		// Caller supplies Java's already-filtered PlayerTeamRewardStats.players size and AIQuestion.REWARD_AP result.
		if (member == null)
			return WorldNpcTeamApRewardResult.MissingMember(npc?.ObjectId ?? 0, damagePercent, instanceApMultiplier, eligiblePlayerCount);
		if (npc == null)
		{
			return WorldNpcTeamApRewardResult.MissingNpc(
				member.ObjectId,
				member.AbyssRank.Ap,
				damagePercent,
				instanceApMultiplier,
				eligiblePlayerCount);
		}

		if (eligiblePlayerCount <= 0)
		{
			return WorldNpcTeamApRewardResult.NoEligiblePlayers(
				member.ObjectId,
				npc.ObjectId,
				member.AbyssRank.Ap,
				damagePercent,
				instanceApMultiplier,
				eligiblePlayerCount);
		}

		if (IsPlayerDead(member))
		{
			return WorldNpcTeamApRewardResult.PlayerDead(
				member.ObjectId,
				npc.ObjectId,
				member.AbyssRank.Ap,
				damagePercent,
				instanceApMultiplier,
				eligiblePlayerCount);
		}

		if (!shouldRewardAp)
		{
			return WorldNpcTeamApRewardResult.ApRewardDenied(
				member.ObjectId,
				npc.ObjectId,
				member.AbyssRank.Ap,
				damagePercent,
				instanceApMultiplier,
				eligiblePlayerCount);
		}

		if (suppressApForMentorGroup)
		{
			return WorldNpcTeamApRewardResult.MentorGroupApSuppressed(
				member.ObjectId,
				npc.ObjectId,
				member.AbyssRank.Ap,
				damagePercent,
				instanceApMultiplier,
				eligiblePlayerCount);
		}

		var configuredApPveRates = apPveRates ?? _rateOptions.ApPveRates;
		var calculatedAp = WorldNpcSoloDpRewardService.CalculatePveApGained(
			member,
			npc.Template,
			configuredApPveRates,
			apBoostStat);
		var rewardAp = CalculateTeamMemberApReward(
			calculatedAp,
			damagePercent,
			instanceApMultiplier,
			eligiblePlayerCount);
		if (rewardAp < 1)
		{
			return WorldNpcTeamApRewardResult.NoApReward(
				member.ObjectId,
				npc.ObjectId,
				member.AbyssRank.Ap,
				damagePercent,
				instanceApMultiplier,
				eligiblePlayerCount,
				calculatedAp);
		}

		var previousAp = member.AbyssRank.Ap;
		var plan = AbyssPointsService.AddApFromObject(
			member,
			npc.ObjectId,
			sourceIsPlayer: false,
			sourceIsSiegeNpc,
			sourceSiegeNpcPeace,
			rewardAp,
			abyssPointsOptions);
		return WorldNpcTeamApRewardResult.FromAbyssPointsPlan(
			plan,
			member.ObjectId,
			npc.ObjectId,
			damagePercent,
			instanceApMultiplier,
			eligiblePlayerCount,
			calculatedAp,
			rewardAp,
			previousAp);
	}

	public static int CalculateTeamMemberApReward(
		int calculatedPveAp,
		float damagePercent,
		float instanceApMultiplier,
		int eligiblePlayerCount)
	{
		// Java parity: rewardAp starts at 1, is scaled by damage percent and instance AP multiplier,
		// then multiplied by StatFunctions.calculatePvEApGained. Java casts to int before group-share division.
		if (eligiblePlayerCount <= 0)
			return 0;

		var rewardAp = 1f;
		rewardAp *= damagePercent;
		rewardAp *= instanceApMultiplier;
		rewardAp *= calculatedPveAp;
		var narrowedRewardAp = JavaFloatToInt(rewardAp);
		return narrowedRewardAp / eligiblePlayerCount;
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

public sealed record WorldNpcTeamApRewardResult(
	WorldNpcTeamApRewardStatus Status,
	int ObjectId,
	int NpcObjectId,
	float DamagePercent,
	float InstanceApMultiplier,
	int EligiblePlayerCount,
	int CalculatedAp,
	int RewardAp,
	int PreviousAp,
	int CurrentAp,
	AbyssPointsAddPlan? AbyssPointsPlan = null)
{
	public static WorldNpcTeamApRewardResult MissingMember(
		int npcObjectId,
		float damagePercent,
		float instanceApMultiplier,
		int eligiblePlayerCount)
	{
		return new WorldNpcTeamApRewardResult(
			WorldNpcTeamApRewardStatus.MissingMember,
			0,
			npcObjectId,
			damagePercent,
			instanceApMultiplier,
			eligiblePlayerCount,
			0,
			0,
			0,
			0);
	}

	public static WorldNpcTeamApRewardResult MissingNpc(
		int objectId,
		int currentAp,
		float damagePercent,
		float instanceApMultiplier,
		int eligiblePlayerCount)
	{
		return new WorldNpcTeamApRewardResult(
			WorldNpcTeamApRewardStatus.MissingNpc,
			objectId,
			0,
			damagePercent,
			instanceApMultiplier,
			eligiblePlayerCount,
			0,
			0,
			currentAp,
			currentAp);
	}

	public static WorldNpcTeamApRewardResult NoEligiblePlayers(
		int objectId,
		int npcObjectId,
		int currentAp,
		float damagePercent,
		float instanceApMultiplier,
		int eligiblePlayerCount)
	{
		return new WorldNpcTeamApRewardResult(
			WorldNpcTeamApRewardStatus.NoEligiblePlayers,
			objectId,
			npcObjectId,
			damagePercent,
			instanceApMultiplier,
			eligiblePlayerCount,
			0,
			0,
			currentAp,
			currentAp);
	}

	public static WorldNpcTeamApRewardResult PlayerDead(
		int objectId,
		int npcObjectId,
		int currentAp,
		float damagePercent,
		float instanceApMultiplier,
		int eligiblePlayerCount)
	{
		return new WorldNpcTeamApRewardResult(
			WorldNpcTeamApRewardStatus.PlayerDead,
			objectId,
			npcObjectId,
			damagePercent,
			instanceApMultiplier,
			eligiblePlayerCount,
			0,
			0,
			currentAp,
			currentAp);
	}

	public static WorldNpcTeamApRewardResult ApRewardDenied(
		int objectId,
		int npcObjectId,
		int currentAp,
		float damagePercent,
		float instanceApMultiplier,
		int eligiblePlayerCount)
	{
		return new WorldNpcTeamApRewardResult(
			WorldNpcTeamApRewardStatus.ApRewardDenied,
			objectId,
			npcObjectId,
			damagePercent,
			instanceApMultiplier,
			eligiblePlayerCount,
			0,
			0,
			currentAp,
			currentAp);
	}

	public static WorldNpcTeamApRewardResult MentorGroupApSuppressed(
		int objectId,
		int npcObjectId,
		int currentAp,
		float damagePercent,
		float instanceApMultiplier,
		int eligiblePlayerCount)
	{
		return new WorldNpcTeamApRewardResult(
			WorldNpcTeamApRewardStatus.MentorGroupApSuppressed,
			objectId,
			npcObjectId,
			damagePercent,
			instanceApMultiplier,
			eligiblePlayerCount,
			0,
			0,
			currentAp,
			currentAp);
	}

	public static WorldNpcTeamApRewardResult NoApReward(
		int objectId,
		int npcObjectId,
		int currentAp,
		float damagePercent,
		float instanceApMultiplier,
		int eligiblePlayerCount,
		int calculatedAp)
	{
		return new WorldNpcTeamApRewardResult(
			WorldNpcTeamApRewardStatus.NoApReward,
			objectId,
			npcObjectId,
			damagePercent,
			instanceApMultiplier,
			eligiblePlayerCount,
			calculatedAp,
			0,
			currentAp,
			currentAp);
	}

	public static WorldNpcTeamApRewardResult FromAbyssPointsPlan(
		AbyssPointsAddPlan plan,
		int objectId,
		int npcObjectId,
		float damagePercent,
		float instanceApMultiplier,
		int eligiblePlayerCount,
		int calculatedAp,
		int rewardAp,
		int previousAp)
	{
		return new WorldNpcTeamApRewardResult(
			plan.Applied ? WorldNpcTeamApRewardStatus.Applied : WorldNpcTeamApRewardStatus.ApBoundarySkipped,
			objectId,
			npcObjectId,
			damagePercent,
			instanceApMultiplier,
			eligiblePlayerCount,
			calculatedAp,
			rewardAp,
			previousAp,
			plan.UpdatedRank?.Ap ?? previousAp,
			plan);
	}
}

public enum WorldNpcTeamApRewardStatus
{
	Applied,
	MissingMember,
	MissingNpc,
	NoEligiblePlayers,
	PlayerDead,
	ApRewardDenied,
	MentorGroupApSuppressed,
	NoApReward,
	ApBoundarySkipped,
}
