using System;
using System.Threading.Tasks;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services.Rift;

/// <summary>Java parity: services/rift/RiftOpenRunnable. Java Runnable → plain class with Run(); schedule(Runnable,ms)→Schedule(ct-lambda, TimeSpan).</summary>
public class RiftOpenRunnable : Runnable
{
    private readonly int worldId;
    private readonly bool guards;

    public RiftOpenRunnable(int worldId, bool guards)
    {
        this.worldId = worldId;
        this.guards = guards;
    }

    public void Run()
    {
        RiftService.GetInstance().PrepareRiftOpening(worldId, guards);
        // Scheduled rifts close
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            RiftService.GetInstance().CloseAutoCloseableRifts(worldId);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(RiftService.GetInstance().GetDuration() * 3540 * 1000));
        // Broadcast rift spawn on map
        RiftInformer.SendRiftsInfo(worldId);
    }
}
