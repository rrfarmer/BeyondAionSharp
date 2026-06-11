namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>Java parity: model/gameobjects/player/PortalCooldown.</summary>
public class PortalCooldown
{
    private int worldId;
    private long reuseTime;
    private int enterCount;

    public PortalCooldown(int worldId, long reuseTime, int enterCount)
    {
        this.worldId = worldId;
        this.reuseTime = reuseTime;
        this.enterCount = enterCount;
    }

    public void IncreaseEnterCount()
    {
        this.enterCount++;
    }

    public void DecreaseEnterCount()
    {
        this.enterCount--;
    }

    public int GetWorldId()
    {
        return worldId;
    }

    public long GetReuseTime()
    {
        return reuseTime;
    }

    public int GetEnterCount()
    {
        return enterCount;
    }
}
