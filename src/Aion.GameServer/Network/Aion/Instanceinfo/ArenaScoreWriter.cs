using Aion.Commons.Nio;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Model.Templates.Rewards;
using Aion.GameServer.Model;

namespace Aion.GameServer.Network.Aion.Instanceinfo;

/// <summary>
/// Java parity: network/aion/instanceinfo/ArenaScoreWriter (Neonm, Estrayl) : InstanceScoreWriter&lt;PvPArenaScore&gt;. Writes the
/// per-player arena table (padded to 12 @ 92 bytes) + the owner's reward block. ArenaRewardItem is a C# record (itemId() -> ItemId
/// property etc.). writeS(name,54) fixed-size. PvPArenaScore/PvPArenaPlayerReward/RewardItem red-tolerated.
/// </summary>
public class ArenaScoreWriter : InstanceScoreWriter<PvPArenaScore>
{
    protected readonly int ownerObjectId;
    private readonly bool rewardTable;

    public ArenaScoreWriter(PvPArenaScore score, int ownerObjectId, bool rewardTable)
        : base(score)
    {
        this.ownerObjectId = ownerObjectId;
        this.rewardTable = rewardTable;
    }

    protected override void WriteMe(ByteBuffer buf)
    {
        WritePlayerScores(buf);
        WriteOwnerRewards(buf);
        WriteD(buf, 0); // Another instance buff id, possibly 'stageend_buff'
        WriteD(buf, 0); // unk
        WriteD(buf, instanceScore.GetRound()); // round
        WriteD(buf, instanceScore.GetUpperScoreCap()); // cap points
        WriteD(buf, 3); // possible rounds
        WriteD(buf, rewardTable ? 1 : 0); // possible reward table
    }

    protected void WritePlayerScores(ByteBuffer buf)
    {
        int playerCount = 0;
        foreach (PvPArenaPlayerReward apr in instanceScore.GetPlayerRewards())
        {
            WriteD(buf, apr.GetOwnerId());
            WriteD(buf, apr.GetPvPKills());
            WriteD(buf, instanceScore.IsRewarded() ? apr.GetScorePoints() : apr.GetPoints());
            WriteD(buf, 0); // unk
            WriteC(buf, 0); // unk
            WriteC(buf, apr.GetPlayerClass().GetClassId());
            WriteC(buf, 1); // unk
            WriteC(buf, instanceScore.GetRank(apr)); // top position
            WriteD(buf, apr.GetRemainingTime()); // instance buff time, also controls shield effect
            WriteD(buf, instanceScore.IsRewarded() ? apr.GetTimeBonus() : 0);
            WriteD(buf, apr.HasBoostMorale() ? instanceScore.GetBuffId() : 0); // Instance Buff ID
            WriteD(buf, 0); // unk
            WriteH(buf, instanceScore.IsRewarded() ? (short)(apr.GetParticipation() * 100) : 0); // participation
            WriteS(buf, apr.GetPlayerName(), 54);
            playerCount++;
        }
        if (playerCount < 12)
            WriteB(buf, new byte[92 * (12 - playerCount)]); // spaces
    }

    private void WriteOwnerRewards(ByteBuffer buf)
    {
        PvPArenaPlayerReward arenaReward = instanceScore.GetPlayerReward(ownerObjectId);
        if (instanceScore.IsRewarded() && instanceScore.CanReward() && arenaReward != null)
        {
            WriteReward(buf, arenaReward.GetAp());
            WriteReward(buf, arenaReward.GetGp());
            WriteReward(buf, arenaReward.GetCrucibleInsignia());
            WriteReward(buf, arenaReward.GetCourageInsignia());
            WriteSimpleReward(buf, arenaReward.GetRewardItem1());
            WriteSimpleReward(buf, arenaReward.GetRewardItem2());
        }
        else
        {
            WriteB(buf, new byte[72]);
        }
    }

    private void WriteReward(ByteBuffer buf, ArenaRewardItem rewardItem)
    {
        if (rewardItem.ItemId != 0)
            WriteD(buf, rewardItem.ItemId);
        WriteD(buf, rewardItem.BaseCount);
        WriteD(buf, rewardItem.ScoreCount);
        WriteD(buf, rewardItem.RankingCount);
    }

    private void WriteSimpleReward(ByteBuffer buf, RewardItem rewardItem)
    {
        if (rewardItem != null)
        {
            WriteD(buf, rewardItem.GetId());
            WriteD(buf, (int)rewardItem.GetCount());
        }
        else
        {
            WriteD(buf, 0);
            WriteD(buf, 0);
        }
    }
}
