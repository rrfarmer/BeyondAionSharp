namespace Aion.GameServer.Custom.Instance;

/// <summary>Java parity: custom/instance/CustomInstanceRank.</summary>
public class CustomInstanceRank
{
    private readonly int playerId;
    private int rank;
    private int maxRank;
    private int dps;
    private long lastEntry;

    public CustomInstanceRank(int playerId, int rank, long lastEntry, int maxRank, int dps)
    {
        this.playerId = playerId;
        this.rank = rank;
        this.lastEntry = lastEntry;
        this.maxRank = maxRank;
        this.dps = dps;
    }

    public int GetPlayerId()
    {
        return playerId;
    }

    public int GetRank()
    {
        return rank;
    }

    public void SetRank(int rank)
    {
        this.rank = rank;
    }

    public long GetLastEntry()
    {
        return lastEntry;
    }

    public void SetLastEntry(long lastEntry)
    {
        this.lastEntry = lastEntry;
    }

    public int GetMaxRank()
    {
        return maxRank;
    }

    public void SetMaxRank(int maxRank)
    {
        this.maxRank = maxRank;
    }

    public int GetDps()
    {
        return dps;
    }

    public void SetDps(int dps)
    {
        this.dps = dps;
    }
}
