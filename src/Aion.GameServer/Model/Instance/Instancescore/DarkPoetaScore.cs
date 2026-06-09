using Aion.GameServer.Model.Instance.Playerreward;

namespace Aion.GameServer.Model.Instance.Instancescore;

/// <summary>Java parity: model/instance/instancescore/DarkPoetaScore.</summary>
public class DarkPoetaScore : InstanceScore<InstancePlayerReward>
{
    private int points;
    private int npcKills;
    private int rank = 7;
    private int collections;

    public void AddPoints(int points)
    {
        this.points += points;
    }

    public int GetPoints()
    {
        return points;
    }

    public void AddNpcKill()
    {
        npcKills++;
    }

    public int GetNpcKills()
    {
        return npcKills;
    }

    public void SetRank(int rank)
    {
        this.rank = rank;
    }

    public int GetRank()
    {
        return rank;
    }

    public void AddGather()
    {
        collections++;
    }

    public int GetGatherCollections()
    {
        return collections;
    }
}
