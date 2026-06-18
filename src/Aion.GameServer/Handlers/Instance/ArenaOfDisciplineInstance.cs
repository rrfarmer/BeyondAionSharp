using Aion.GameServer.Configs.Main;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Model.Templates.Rewards;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/pvparenas/ArenaOfDisciplineInstance (xTz, Estrayl) : DisciplineTrainingGroundsInstance. @InstanceID(300360000). 1:1.</summary>
[InstanceID(300360000)]
public class ArenaOfDisciplineInstance : DisciplineTrainingGroundsInstance
{
    public ArenaOfDisciplineInstance(WorldMapInstance instance) : base(instance)
    {
    }

    protected override BaseRewards GetBaseRewardsPerPlayer(int difficultyId)
    {
        // Extracted values from instant_dungeon_idarenapvp.xml (Retail Leak 4.6, also verified by different videos)
        return difficultyId switch
        {
            2 => new BaseRewards(900, 0, 425, 38),
            3 => new BaseRewards(1100, 0, 655, 59),
            4 => new BaseRewards(1200, 39, 655, 59),
            _ => new BaseRewards(700, 0, 310, 20),
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
            0 => difficultyId == 4 ? 0.75f : 0.72f,
            _ => difficultyId == 4 ? 0.25f : 0.28f,
        };
    }

    protected override void SetRewardItems(PvPArenaPlayerReward reward, int rank, int difficultyId)
    {
        if (rank != 0 && difficultyId > 1)
            reward.SetRewardItem1(new RewardItem(186000165, 4)); // Opportunity Token
    }

    protected override float GetConfigRate(Player player)
    {
        return Rates.Get(player, RatesConfig.PVP_ARENA_DISCIPLINE_REWARD_RATES);
    }
}
