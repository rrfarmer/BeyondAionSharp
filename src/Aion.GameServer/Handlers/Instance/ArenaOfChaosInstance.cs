using Aion.GameServer.Configs.Main;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Model.Templates.Rewards;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/pvparenas/ArenaOfChaosInstance (xTz, Estrayl) : ChaosTrainingGroundsInstance. @InstanceID(300350000). 1:1.</summary>
[InstanceID(300350000)]
public class ArenaOfChaosInstance : ChaosTrainingGroundsInstance
{
    public ArenaOfChaosInstance(WorldMapInstance instance) : base(instance)
    {
    }

    protected override BaseRewards GetBaseRewardsPerPlayer(int difficultyId)
    {
        // Extracted values from instant_dungeon_idarenapvp.xml (Retail Leak 4.6, also verified by different videos)
        return difficultyId switch
        {
            2 => new BaseRewards(900, 0, 425, 50),
            3 => new BaseRewards(1100, 0, 655, 77),
            4 => new BaseRewards(1300, 31, 655, 77),
            _ => new BaseRewards(700, 0, 310, 27),
        };
    }

    protected override BaseRewards GetBaseRewards(int difficultyId)
    {
        return difficultyId switch
        {
            1 => new BaseRewards(200, 0, 90, 0),
            2 => new BaseRewards(200, 0, 125, 0),
            _ => new BaseRewards(200, 0, 195, 0),
        };
    }

    protected override float GetRewardRate(int rank, int difficultyId)
    {
        return rank switch
        {
            0 => 0.21f,
            1 => 0.16f,
            2 => 0.135f,
            3 => 0.115f,
            4 => 0.095f,
            5 => 0.075f,
            _ => 0.050f,
        };
    }

    protected override void SetRewardItems(PvPArenaPlayerReward reward, int rank, int difficultyId)
    {
        switch (rank)
        {
            case 0:
            case 1:
                if (difficultyId > 2)
                    reward.SetRewardItem2(new RewardItem(186000185, 1)); // Arena of Glory Ticket, Item2 is indented by retail
                break;
            case 2:
                reward.SetRewardItem1(new RewardItem(186000165, 1)); // Opportunity Token
                break;
            case 3:
                reward.SetRewardItem1(new RewardItem(186000165, 2)); // Opportunity Token
                break;
            case 4:
                reward.SetRewardItem1(new RewardItem(186000165, 3)); // Opportunity Token
                break;
            default:
                reward.SetRewardItem1(new RewardItem(186000165, 5)); // Opportunity Token
                break;
        }
    }

    protected override float GetConfigRate(Player player)
    {
        return Rates.Get(player, RatesConfig.PVP_ARENA_CHAOS_REWARD_RATES);
    }
}
