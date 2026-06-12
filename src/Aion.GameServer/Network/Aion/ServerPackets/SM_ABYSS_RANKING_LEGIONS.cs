using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion;
using RankingListLegion = global::Aion.GameServer.Dao.AbyssRankDAO.RankingListLegion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ABYSS_RANKING_LEGIONS (zdead, LokiReborn). Abyss legion ranking list. RankingListLegion record accessors -> PascalCase (aliased from AbyssRankDAO); Collections.emptyList->new List. AbyssRankDAO red-tolerated.</summary>
public class SM_ABYSS_RANKING_LEGIONS : AionServerPacket
{
    private readonly List<RankingListLegion> rankingList;
    private readonly Race race;
    private readonly int updateTime;
    private readonly bool clearListBeforeUpdate;

    public SM_ABYSS_RANKING_LEGIONS(int updateTime, Race race)
        : this(updateTime, new List<RankingListLegion>(), race, false)
    {
    }

    public SM_ABYSS_RANKING_LEGIONS(int updateTime, List<RankingListLegion> rankingList, Race race)
        : this(updateTime, rankingList, race, true)
    {
    }

    private SM_ABYSS_RANKING_LEGIONS(int updateTime, List<RankingListLegion> rankingList, Race race, bool clearListBeforeUpdate)
    {
        this.updateTime = updateTime;
        this.rankingList = rankingList;
        this.race = race;
        this.clearListBeforeUpdate = clearListBeforeUpdate;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(race.GetRaceId());
        WriteD(updateTime);
        WriteD(clearListBeforeUpdate ? 1 : 0);
        WriteD(clearListBeforeUpdate ? 1 : 0);
        WriteH(rankingList.Count);
        foreach (RankingListLegion legion in rankingList)
        {
            WriteD(legion.Position());
            WriteD(legion.OldPosition());
            WriteD(legion.Id());
            WriteD(race.GetRaceId());
            WriteC(legion.Level());
            WriteD(legion.MemberCount());
            WriteQ(legion.ContributionPoints());
            WriteS(legion.Name(), 40);
        }
    }
}
