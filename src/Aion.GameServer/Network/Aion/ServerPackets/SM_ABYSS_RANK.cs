using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Abyss;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ABYSS_RANK (Nemiroff). Sends a player's abyss rank stats (AP/GP/rank/ranking position + all/daily/weekly/last kill/AP/GP). Converges AbyssPointsService/GloryPointsService/PlayerEnterWorldService. Integer rankingListPosition->int?; AbyssRank/AbyssRankingCache red-tolerated.</summary>
public class SM_ABYSS_RANK : AionServerPacket
{
    private readonly AbyssRank rank;
    private readonly int rankingListPosition;

    public SM_ABYSS_RANK(Player player)
        : this(player, null)
    {
    }

    public SM_ABYSS_RANK(Player player, int? rankingListPosition)
    {
        this.rank = player.GetAbyssRank();
        this.rankingListPosition = rankingListPosition == null ? AbyssRankingCache.GetInstance().GetRankingListPosition(player) : rankingListPosition.Value;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteQ(rank.GetAp());
        WriteD(rank.GetCurrentGP());
        WriteD(rank.GetRank().GetId());
        WriteD(rankingListPosition);

        WriteD(0); // exp % removed with 4.5?
        WriteD(rank.GetAllKill());
        WriteD(rank.GetMaxRank());

        WriteD(rank.GetDailyKill());
        WriteQ(rank.GetDailyAP());
        WriteD(rank.GetDailyGP());

        WriteD(rank.GetWeeklyKill());
        WriteQ(rank.GetWeeklyAP());
        WriteD(rank.GetWeeklyGP());

        WriteD(rank.GetLastKill());
        WriteQ(rank.GetLastAP());
        WriteD(rank.GetLastGP());

        WriteC(0x00); // unk
    }
}
