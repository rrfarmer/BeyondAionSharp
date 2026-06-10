using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion;
using RankingListPlayer = Aion.GameServer.Dao.AbyssRankDAO.RankingListPlayer;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ABYSS_RANKING_PLAYERS (Rhys2002, zdead, LokiReborn). Paged abyss player ranking list. RankingListPlayer record accessors -> PascalCase; Collections.emptyList()->new List; CHARNAME_MAX_LENGTH from AbstractPlayerInfoPacket. AbyssRankDAO red-tolerated.</summary>
public class SM_ABYSS_RANKING_PLAYERS : AionServerPacket
{
    private readonly List<RankingListPlayer> players;
    private readonly int lastUpdate;
    private readonly int race;
    private readonly int page;
    private readonly bool isEndPacket;

    public SM_ABYSS_RANKING_PLAYERS(int lastUpdate, Race race)
        : this(lastUpdate, new List<RankingListPlayer>(), race, 0, false)
    {
    }

    public SM_ABYSS_RANKING_PLAYERS(int lastUpdate, List<RankingListPlayer> players, Race race, int page, bool isEndPacket)
    {
        this.lastUpdate = lastUpdate;
        this.players = players;
        this.race = race.GetRaceId();
        this.page = page;
        this.isEndPacket = isEndPacket;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(race);
        WriteD(lastUpdate);
        WriteD(page);
        WriteD(isEndPacket ? 0x7F : 0);// 0:Nothing 1:Update Table
        WriteH(players.Count);

        foreach (RankingListPlayer player in players)
        {
            WriteD(player.Position());
            WriteD(player.AbyssRank());
            WriteD(player.OldPosition());
            WriteD(player.Id());
            WriteD(race);
            WriteD(player.PlayerClass().GetClassId());
            WriteC(player.Gender().GetGenderId());
            WriteC(0); // unk
            WriteC(0); // unk
            WriteC(0); // unk
            WriteQ(player.Ap());
            WriteD(player.Gp());
            WriteH(player.Level());
            WriteS(player.Name(), AbstractPlayerInfoPacket.CHARNAME_MAX_LENGTH);// Two strings actually: player name + server name suffix (eg., SL for FastTrack)
            WriteS(player.LegionName(), 42);
        }
    }
}
