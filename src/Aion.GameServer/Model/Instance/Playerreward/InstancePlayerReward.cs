namespace Aion.GameServer.Model.Instance.Playerreward;

/// <summary>Java parity: model/instance/playerreward/InstancePlayerReward.</summary>
public class InstancePlayerReward
{
    private int points;
    private int playerPvPKills;
    private int playerMonsterKills;
    private readonly int objectId;

    public InstancePlayerReward(int objectId)
    {
        this.objectId = objectId;
    }

    public int GetOwnerId()
    {
        return objectId;
    }

    public int GetPoints()
    {
        return points;
    }

    public int GetPvPKills()
    {
        return playerPvPKills;
    }

    public int GetMonsterKills()
    {
        return playerMonsterKills;
    }

    public void AddPoints(int points)
    {
        this.points += points;
    }

    public void SetPoints(int points)
    {
        this.points = points;
    }

    public void AddPvPKill()
    {
        playerPvPKills++;
    }

    public void AddMonsterKillToPlayer()
    {
        playerMonsterKills++;
    }
}
