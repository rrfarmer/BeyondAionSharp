using System.Collections.Generic;
using Aion.Commons.Nio;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Model.Templates.Rewards;

namespace Aion.GameServer.Network.Aion.Instanceinfo;

/// <summary>
/// Java parity: network/aion/instanceinfo/HarmonyScoreWriter (xTz) : InstanceScoreWriter&lt;HarmonyArenaScore&gt;. Multi-mode writer
/// keyed on InstanceScoreType (SCREAMING; GetId() extension). AGPlayer/ArenaRewardItem are C# records (name()/baseCount() -> Name/
/// BaseCount properties). writeS(name,52) fixed-size. HarmonyArenaScore/HarmonyGroupReward/PvPArenaPlayerReward red-tolerated.
/// </summary>
public class HarmonyScoreWriter : InstanceScoreWriter<HarmonyArenaScore>
{
    private readonly InstanceScoreType type;
    private int playerObjId;

    public HarmonyScoreWriter(HarmonyArenaScore reward, InstanceScoreType type, Player owner)
        : base(reward)
    {
        this.type = type;
        this.playerObjId = owner.GetObjectId();
    }

    public HarmonyScoreWriter(HarmonyArenaScore reward, InstanceScoreType type)
        : base(reward)
    {
        this.type = type;
    }

    protected override void WriteMe(ByteBuffer buf)
    {
        HarmonyGroupReward harmonyGroupReward = instanceScore.GetGroupReward(playerObjId);
        WriteC(buf, type.GetId());
        switch (type)
        {
            case InstanceScoreType.UPDATE_INSTANCE_PROGRESS:
                WriteD(buf, 0);
                WriteD(buf, instanceScore.GetRound());
                break;
            case InstanceScoreType.INIT_PLAYER:
                if (harmonyGroupReward == null)
                {
                    WriteB(buf, new byte[64]);
                    return;
                }

                WriteD(buf, harmonyGroupReward.GetOwnerId());
                WriteS(buf, harmonyGroupReward.GetAGPlayer(playerObjId).Name, 52); // playerName
                WriteD(buf, harmonyGroupReward.GetGrpObjectId()); // groupObj
                WriteD(buf, playerObjId); // memberObj
                break;
            case InstanceScoreType.UPDATE_PLAYER_BUFF_STATUS:
                PvPArenaPlayerReward reward = instanceScore.GetPlayerReward(playerObjId);
                if (reward == null)
                {
                    WriteB(buf, new byte[16]);
                    return;
                }

                WriteD(buf, reward.GetRemainingTime()); // buffTime
                WriteD(buf, reward.HasBoostMorale() ? instanceScore.GetBuffId() : 0); // Buff ID
                WriteD(buf, 0);
                WriteD(buf, playerObjId); // memberObj
                break;
            case InstanceScoreType.SHOW_REWARD:
                PvPArenaPlayerReward playerReward = instanceScore.GetPlayerReward(playerObjId);
                if (playerReward == null)
                {
                    WriteB(buf, new byte[72]);
                    return;
                }

                WritePoints(buf, playerReward.GetAp());
                WritePoints(buf, playerReward.GetGp());
                WriteReward(buf, playerReward.GetCourageInsignia());
                WriteReward(buf, playerReward.GetCrucibleInsignia());
                WriteSimpleReward(buf, playerReward.GetRewardItem1());
                WriteSimpleReward(buf, playerReward.GetRewardItem2());
                break;
            case InstanceScoreType.UPDATE_INSTANCE_BUFFS_AND_SCORE:
                WriteD(buf, instanceScore.GetLowerScoreCap());
                WriteD(buf, instanceScore.GetUpperScoreCap()); // capPoints
                WriteD(buf, 3); // possible rounds
                WriteD(buf, 1);
                WriteD(buf, 0); // Global Buff Id
                WriteD(buf, 0);
                WriteD(buf, 0);
                WriteD(buf, instanceScore.GetRound()); // round
                List<HarmonyGroupReward> groups = instanceScore.GetHarmonyGroupInside();
                WriteC(buf, groups.Count); // size
                int groupIndex = 0;
                foreach (HarmonyGroupReward group in groups)
                {
                    WriteC(buf, instanceScore.GetRank(group));
                    WriteD(buf, group.GetPvPKills());
                    WriteD(buf, group.GetPoints()); // groupScore
                    WriteD(buf, group.GetGrpObjectId()); // groupObj
                    List<Player> members = instanceScore.GetPlayersInside(group);
                    WriteC(buf, members.Count);
                    int i = 0;
                    foreach (Player p in members)
                    {
                        PvPArenaPlayerReward apr = instanceScore.GetPlayerReward(p.GetObjectId());
                        WriteD(buf, 0);
                        WriteD(buf, apr.GetRemainingTime());
                        WriteD(buf, 0); // Probably Buff id?
                        WriteC(buf, groupIndex); // groupIndex, necessary for group names
                        WriteC(buf, i); // memberNr
                        WriteH(buf, 0);
                        WriteS(buf, p.GetName(), 52); // playerName
                        WriteD(buf, p.GetObjectId()); // memberObj
                        i++;
                    }
                    groupIndex++;
                }
                break;
            case InstanceScoreType.UPDATE_RANK:
                if (harmonyGroupReward == null)
                {
                    WriteB(buf, new byte[13]);
                    return;
                }

                WriteC(buf, instanceScore.GetRank(harmonyGroupReward));
                WriteD(buf, harmonyGroupReward.GetPvPKills()); // kills
                WriteD(buf, harmonyGroupReward.GetPoints()); // groupScore
                WriteD(buf, harmonyGroupReward.GetGrpObjectId()); // groupObj
                break;
        }
    }

    private void WritePoints(ByteBuffer buf, ArenaRewardItem rewardItem)
    {
        WriteD(buf, rewardItem.BaseCount);
        WriteD(buf, rewardItem.ScoreCount);
        WriteD(buf, rewardItem.RankingCount);
    }

    private void WriteReward(ByteBuffer buf, ArenaRewardItem rewardItem)
    {
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
