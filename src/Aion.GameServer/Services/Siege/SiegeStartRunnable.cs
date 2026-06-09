namespace Aion.GameServer.Services.Siege;

/// <summary>Java parity: services/siege/SiegeStartRunnable. Java Runnable → plain class with Run() (passed as method-group to ThreadPoolManager).</summary>
public class SiegeStartRunnable
{
    private readonly int locationId;

    public SiegeStartRunnable(int locationId)
    {
        this.locationId = locationId;
    }

    public void Run()
    {
        SiegeService.GetInstance().CheckSiegeStart(GetLocationId());
    }

    public int GetLocationId()
    {
        return locationId;
    }
}
