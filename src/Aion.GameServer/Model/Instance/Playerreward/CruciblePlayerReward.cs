namespace Aion.GameServer.Model.Instance.Playerreward;

/// <summary>Java parity: model/instance/playerreward/CruciblePlayerReward.</summary>
public class CruciblePlayerReward : InstancePlayerReward
{
    private int spawnPosition;
    private bool isRewarded = false;
    private int insignia;
    private bool isPlayerLeave = false;
    private bool isPlayerDefeated = false;

    public CruciblePlayerReward(int objectId)
        : base(objectId)
    {
    }

    public bool IsRewarded()
    {
        return isRewarded;
    }

    public void SetRewarded()
    {
        isRewarded = true;
    }

    public void SetInsignia(int insignia)
    {
        this.insignia = insignia;
    }

    public int GetInsignia()
    {
        return insignia;
    }

    public void SetSpawnPosition(int spawnPosition)
    {
        this.spawnPosition = spawnPosition;
    }

    public int GetSpawnPosition()
    {
        return spawnPosition;
    }

    public bool IsPlayerLeave()
    {
        return isPlayerLeave;
    }

    public void SetPlayerLeave()
    {
        isPlayerLeave = true;
    }

    public void SetPlayerDefeated(bool value)
    {
        isPlayerDefeated = value;
    }

    public bool IsPlayerDefeated()
    {
        return isPlayerDefeated;
    }
}
