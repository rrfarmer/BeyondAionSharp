using Aion.GameServer.Configs.Main;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Model.Templates.Rewards;
using Aion.GameServer.Network.Aion.Instanceinfo;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/pvparenas/ArenaOfGloryInstance (xTz, Estrayl) : PvPArenaInstance. @InstanceID(300550000). 1:1.</summary>
[InstanceID(300550000)]
public class ArenaOfGloryInstance : PvPArenaInstance
{
    public ArenaOfGloryInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        pointsPerKill = 1000;
        pointsPerDeath = -200;
        base.OnInstanceCreate();
    }

    protected override int GetBoostMoraleEffectDuration(int rank)
    {
        return rank switch
        {
            0 or 1 => 14000,
            _ => 15000,
        };
    }

    protected override float GetRunnerUpScoreMod(int victimRank)
    {
        return 4f;
    }

    protected override void SendPacket(Player player, InstanceScoreType? scoreType)
    {
        instance.ForEachPlayer(
            p => PacketSendUtility.SendPacket(p, new SM_INSTANCE_SCORE(instance.GetMapId(), new ArenaScoreWriter(instanceScore, p.GetObjectId(), false))));
    }

    protected override BaseRewards GetBaseRewardsPerPlayer(int difficultyId)
    {
        // Extracted values from instant_dungeon_idarenapvp.xml (Retail Leak 4.6, also verified by different videos)
        // Retail4.6, there are 4 difficultyIds - maybe they were removed
        // 1 new BaseValuesPerPlayer(700, 0, 310, 27);
        // 2 new BaseValuesPerPlayer(900, 0, 425, 50);
        // 3 new BaseValuesPerPlayer(1100, 0, 655, 77)
        // 4 new BaseValuesPerPlayer(1300, 31, 655, 77);
        return difficultyId switch
        {
            2 => new BaseRewards(1300, 31, 655, 77),
            _ => new BaseRewards(1100, 0, 655, 77),
        };
    }

    protected override float GetRewardRate(int rank, int difficultyId)
    {
        return rank switch
        {
            0 => 0.55f,
            1 => 0.25f,
            2 => 0.15f,
            _ => 0.05f,
        };
    }

    protected override void SetRewardItems(PvPArenaPlayerReward reward, int rank, int difficultyId)
    {
        switch (rank)
        {
            case 0:
                switch (difficultyId)
                {
                    case 2:
                        reward.SetRewardItem1(new RewardItem(186000242, 3)); // Ceramium Medal
                        reward.SetRewardItem2(new RewardItem(182213259, 1)); // Arena of Glory Ticket
                        break;
                    default:
                        reward.SetRewardItem1(new RewardItem(186000147, 5)); // Mithril Medal
                        reward.SetRewardItem2(new RewardItem(182213259, 1)); // Arena of Glory Ticket
                        break;
                }
                break;
            case 1:
                switch (difficultyId)
                {
                    case 2:
                        reward.SetRewardItem1(new RewardItem(186000242, 1)); // Ceramium Medal
                        break;
                    default:
                        reward.SetRewardItem1(new RewardItem(186000147, 1)); // Mithril Medal
                        reward.SetRewardItem1(new RewardItem(186000096, 3)); // Platinum Medal
                        break;
                }
                break;
            case 2:
                switch (difficultyId)
                {
                    case 2:
                        reward.SetRewardItem1(new RewardItem(186000147, 2)); // Mithril Medal
                        break;
                    default:
                        reward.SetRewardItem1(new RewardItem(186000096, 3)); // Platinum Medal
                        break;
                }
                break;
            default:
                switch (difficultyId)
                {
                    case 2:
                        reward.SetRewardItem1(new RewardItem(162000124, 5)); // Superior Recovery Serum
                        break;
                    default:
                        reward.SetRewardItem1(new RewardItem(162000077, 1)); // Fine Life Serum
                        break;
                }
                break;
        }
    }

    protected override float GetConfigRate(Player player)
    {
        return Rates.Get(player, RatesConfig.PVP_ARENA_GLORY_REWARD_RATES);
    }
}
