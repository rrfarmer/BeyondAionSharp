namespace Aion.GameServer.Services.Siege;

/// <summary>Java parity: services/siege/SiegeStartRunnable implements Runnable; passed to ThreadPoolManager/CronService which require the Runnable interface.</summary>
public class SiegeStartRunnable : Aion.Commons.Lang.Runnable
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
