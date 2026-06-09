namespace Aion.GameServer.Model.Templates.Rewards;

/// <summary>Java parity: model/templates/rewards/ArenaRewardItem (Estrayl) — Java record.</summary>
public record ArenaRewardItem(int ItemId, int BaseCount, int RankingCount, int ScoreCount)
{
    public int GetTotalCount()
    {
        return BaseCount + RankingCount + ScoreCount;
    }
}
