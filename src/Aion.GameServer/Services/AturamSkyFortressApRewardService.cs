using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class AturamSkyFortressApRewardService
{
	public const int RewardNpcId = 217382;
	public const int RewardAp = 540;

	public AturamSkyFortressApRewardResult ApplyGeneratorApReward(
		Player? mostDamagePlayer,
		int npcId,
		AbyssPointsAddOptions? abyssPointsOptions = null)
	{
		// Java parity: data/handlers/instance/AturamSkyFortressInstance.onDie case 217382
		// selects npc.getAggroList().getMostPlayerDamage(), then AbyssPointsService.addAp(player, 540).
		if (npcId != RewardNpcId)
			return AturamSkyFortressApRewardResult.NotRewardNpc(npcId);
		if (mostDamagePlayer == null)
			return AturamSkyFortressApRewardResult.NoMostDamagePlayer(npcId);

		var previousAp = mostDamagePlayer.AbyssRank.Ap;
		var plan = AbyssPointsService.AddAp(mostDamagePlayer, RewardAp, abyssPointsOptions);
		return AturamSkyFortressApRewardResult.FromAbyssPointsPlan(
			plan,
			mostDamagePlayer.ObjectId,
			npcId,
			RewardAp,
			previousAp);
	}
}

public sealed record AturamSkyFortressApRewardResult(
	AturamSkyFortressApRewardStatus Status,
	int ObjectId,
	int NpcId,
	int RewardAp,
	int PreviousAp,
	int CurrentAp,
	AbyssPointsAddPlan? AbyssPointsPlan = null)
{
	public static AturamSkyFortressApRewardResult NotRewardNpc(int npcId)
	{
		return new AturamSkyFortressApRewardResult(
			AturamSkyFortressApRewardStatus.NotRewardNpc,
			0,
			npcId,
			0,
			0,
			0);
	}

	public static AturamSkyFortressApRewardResult NoMostDamagePlayer(int npcId)
	{
		return new AturamSkyFortressApRewardResult(
			AturamSkyFortressApRewardStatus.NoMostDamagePlayer,
			0,
			npcId,
			AturamSkyFortressApRewardService.RewardAp,
			0,
			0);
	}

	public static AturamSkyFortressApRewardResult FromAbyssPointsPlan(
		AbyssPointsAddPlan plan,
		int objectId,
		int npcId,
		int rewardAp,
		int previousAp)
	{
		return new AturamSkyFortressApRewardResult(
			plan.Applied ? AturamSkyFortressApRewardStatus.Applied : AturamSkyFortressApRewardStatus.ApBoundarySkipped,
			objectId,
			npcId,
			rewardAp,
			previousAp,
			plan.UpdatedRank?.Ap ?? previousAp,
			plan);
	}
}

public enum AturamSkyFortressApRewardStatus
{
	Applied,
	NotRewardNpc,
	NoMostDamagePlayer,
	ApBoundarySkipped,
}
