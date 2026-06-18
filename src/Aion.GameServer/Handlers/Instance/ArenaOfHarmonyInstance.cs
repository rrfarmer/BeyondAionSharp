using Aion.GameServer.Configs.Main;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Model.Templates.Rewards;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/pvparenas/ArenaOfHarmonyInstance (xTz, Estrayl) : HarmonyTrainingGroundsInstance. @InstanceID(300450000). 1:1.</summary>
[InstanceID(300450000)]
public class ArenaOfHarmonyInstance : HarmonyTrainingGroundsInstance
{
    public ArenaOfHarmonyInstance(WorldMapInstance instance) : base(instance)
    {
    }

    protected override BaseRewards GetBaseRewardsPerPlayer(int difficultyId)
    {
        return difficultyId switch
        {
            2 => new BaseRewards(2200, 0, 0, 22),
            3 => new BaseRewards(3200, 0, 0, 34),
            4 => new BaseRewards(2500, 31, 0, 34),
            _ => new BaseRewards(1200, 0, 0, 12),
        };
    }

    protected override BaseRewards GetBaseRewards(int difficultyId)
    {
        return new BaseRewards(200, 0, 0, 0);
    }

    protected override float GetRewardRate(int rank, int difficultyId)
    {
        return rank == 0 ? 0.9f : 0.1f;
    }

    protected override void SetRewardItems(PvPArenaPlayerReward reward, int rank, int difficultyId)
    {
        switch (rank)
        {
            case 0:
                switch (difficultyId)
                {
                    case 1:
                        reward.SetRewardItem1(new RewardItem(188052179, 1)); // Lesser Arena Supply Package
                        break;
                    case 2:
                        reward.SetRewardItem1(new RewardItem(188052180, 1)); // Arena Supply Package
                        break;
                    case 3:
                        reward.SetRewardItem1(new RewardItem(188052185, 1)); // Composite Manastone Pouch
                        break;
                    case 4:
                        reward.SetRewardItem1(new RewardItem(188052605, 1)); // Victor's Reward Box
                        reward.SetRewardItem2(new RewardItem(188052482, 1)); // Superior Arena Supply Package
                        break;
                }
                break;
            case 1:
                switch (difficultyId)
                {
                    case 3:
                        reward.SetRewardItem1(new RewardItem(188052181, 1)); // Greater Arena Supply Package
                        break;
                    case 4:
                        reward.SetRewardItem1(new RewardItem(188052606, 1)); // Consolation Prize Box
                        break;
                }
                break;
        }
    }

    protected override float GetConfigRate(Player player)
    {
        if (player == null) // Happens during calculation for group rewards
            return 1f;
        return Rates.Get(player, RatesConfig.PVP_ARENA_HARMONY_REWARD_RATES);
    }
}
