using System.Collections.Generic;
using Aion.Commons.Nio;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Instance.Playerreward;

namespace Aion.GameServer.Network.Aion.Instanceinfo;

/// <summary>
/// Java parity: network/aion/instanceinfo/DredgionScoreWriter (xTz) : InstanceScoreWriter&lt;PvpInstanceScore&lt;PvpInstancePlayerReward&gt;&gt;.
/// Writes the two race tables (padded to 6), team scores, and dredgion room states. Race SCREAMING (ELYOS/ASMODIANS). writeS(name,54)
/// fixed-size. Player/DredgionRoom/PvpInstanceScore red-tolerated.
/// </summary>
public class DredgionScoreWriter : InstanceScoreWriter<PvpInstanceScore<PvpInstancePlayerReward>>
{
    private readonly List<Player> players;
    private readonly List<DredgionRoom> dredgionRooms;

    public DredgionScoreWriter(PvpInstanceScore<PvpInstancePlayerReward> reward, List<Player> players, List<DredgionRoom> dredgionRooms)
        : base(reward)
    {
        this.players = players;
        this.dredgionRooms = dredgionRooms;
    }

    protected override void WriteMe(ByteBuffer buf)
    {
        FillTableWithGroup(buf, Race.ELYOS);
        FillTableWithGroup(buf, Race.ASMODIANS);
        int elyosScore = instanceScore.GetElyosPoints();
        int asmosScore = instanceScore.GetAsmodiansPoints();
        WriteD(buf, instanceScore.GetInstanceProgressionType().IsEndProgress() ? (asmosScore > elyosScore ? 1 : 0) : 255);
        WriteD(buf, elyosScore);
        WriteD(buf, asmosScore);
        foreach (DredgionRoom d in dredgionRooms)
            WriteC(buf, d.GetState());
    }

    private void FillTableWithGroup(ByteBuffer buf, Race race)
    {
        int count = 0;
        foreach (Player player in players)
        {
            if (race != player.GetRace())
            {
                continue;
            }
            PvpInstancePlayerReward playerReward = instanceScore.GetPlayerReward(player.GetObjectId());
            WriteD(buf, playerReward.GetOwnerId()); // playerObjectId
            WriteD(buf, player.GetAbyssRank().GetRank().GetId()); // playerRank
            WriteD(buf, playerReward.GetPvPKills()); // pvpKills
            WriteD(buf, playerReward.GetMonsterKills()); // monsterKills
            WriteD(buf, playerReward.GetCapturedZones()); // captured
            WriteD(buf, playerReward.GetPoints()); // playerScore

            if (instanceScore.GetInstanceProgressionType().IsEndProgress())
            {
                WriteD(buf, playerReward.GetBonusAp() + playerReward.GetBaseAp()); // Client needs to know the sum here
                WriteD(buf, playerReward.GetBaseAp());
            }
            else
            {
                WriteB(buf, new byte[8]);
            }

            WriteC(buf, player.GetPlayerClass().GetClassId()); // playerClass
            WriteC(buf, 0); // unk
            WriteS(buf, player.GetName(), 54); // playerName
            count++;
        }
        if (count < 6)
        {
            WriteB(buf, new byte[88 * (6 - count)]); // spaces
        }
    }
}
