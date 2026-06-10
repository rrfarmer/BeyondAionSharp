using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>Java parity: controllers/observer/AbstractQuestZoneObserver (Rolandas) : ActionObserver. AtomicBoolean→int+Interlocked.Exchange (getAndSet(true)→Exchange(ref,1)==0; set(false)→Exchange(ref,0)); anonymous Runnable→Execute(ct=>{try/finally;ValueTask}); currentTimeMillis→UtcNow.ToUnixTimeMilliseconds; Vector3f.clone→Clone. Vector3f/ZoneTemplate red-tolerated.</summary>
public abstract class AbstractQuestZoneObserver : ActionObserver
{
    protected readonly Player player;
    protected readonly Vector3f startPos;
    protected readonly long startTime;
    protected readonly ZoneTemplate observedZone;
    protected Vector3f oldPos;
    protected int stepCount;
    private int isRunning;

    public AbstractQuestZoneObserver(Player player, ZoneTemplate zoneTemplate) : base(ObserverType.ALL)
    {
        this.player = player;
        this.startPos = new Vector3f(player.GetX(), player.GetY(), player.GetZ());
        this.oldPos = startPos.Clone();
        this.startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        this.observedZone = zoneTemplate;
    }

    public override void Moved()
    {
        if (Interlocked.Exchange(ref isRunning, 1) == 0)
        {
            ThreadPoolManager.GetInstance().Execute(ct =>
            {
                try
                {
                    Vector3f currentPos = new Vector3f(player.GetX(), player.GetY(), player.GetZ());
                    Vector3f center = new Vector3f(observedZone.GetSphere().GetX(), observedZone.GetSphere().GetY(), observedZone.GetSphere().GetZ());
                    float distance = startPos.Distance(currentPos);
                    float distanceFromCenter = center.Distance(currentPos);
                    if (oldPos.Distance(currentPos) > 1)
                    {
                        stepCount++;
                        oldPos = currentPos;
                    }
                    OnMoved(distance, distanceFromCenter, stepCount, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime);
                }
                finally
                {
                    Interlocked.Exchange(ref isRunning, 0);
                }
                return ValueTask.CompletedTask;
            });
        }
    }

    public abstract void OnMoved(float distanceScouted, float distanceToCenter, int steps, long timeSpent);
}
