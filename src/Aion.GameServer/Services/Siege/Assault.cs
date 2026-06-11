using System;
using System.Threading.Tasks;
using Aion.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services.Siege;

/// <summary>Java parity: services/siege/Assault&lt;SiegeType extends Siege&lt;?&gt;&gt; (Luzien, Estrayl) abstract base for Balaur assaults. AtomicBoolean isStarted; startAssault schedules handleAssault after delay; finishAssault cancels tasks + onAssaultFinish(captured && balaur); spawnAssaulter (heading/distance offset trig placement + aggro). Siege&lt;?&gt;->Siege&lt;SiegeLocation&gt; invariance bound; Future->ScheduledTask; method-ref this::handleAssault->ct-lambda; schedule(...,delay,SECONDS)->TimeSpan.FromSeconds; Math.toRadians->x*PI/180; Math.cos/sin->Math.Cos/Sin. Assaulter/SiegeNpc/SpawnEngine red-tolerated.</summary>
public abstract class Assault<SiegeType> where SiegeType : Siege<SiegeLocation>
{
    private readonly AtomicBoolean isStarted = new AtomicBoolean();
    protected readonly SiegeLocation siegeLocation;
    protected readonly SiegeNpc boss;
    protected readonly int locationId;
    protected readonly int worldId;

    protected ScheduledTask dredgionTask, spawnTask;

    public Assault(SiegeType siege)
    {
        this.siegeLocation = siege.GetSiegeLocation();
        this.boss = siege.GetBoss();
        this.locationId = siege.GetSiegeLocationId();
        this.worldId = siege.GetSiegeLocation().GetWorldId();
    }

    public int GetWorldId()
    {
        return worldId;
    }

    public void StartAssault(int delay)
    {
        if (isStarted.CompareAndSet(false, true))
            dredgionTask = ThreadPoolManager.GetInstance().Schedule(ct => { HandleAssault(); return ValueTask.CompletedTask; }, TimeSpan.FromSeconds(delay));
    }

    public void FinishAssault(bool captured)
    {
        if (dredgionTask != null && !dredgionTask.IsDone())
            dredgionTask.Cancel(true);
        if (spawnTask != null && !spawnTask.IsDone())
            spawnTask.Cancel(true);

        OnAssaultFinish(captured && siegeLocation.GetRace() == SiegeRace.BALAUR);
    }

    protected abstract void OnAssaultFinish(bool captured);

    protected abstract void HandleAssault();

    protected void SpawnAssaulter(Assaulter a, SiegeNpc target)
    {
        int headingOffset = a.GetHeadingOffset() * 10;
        float randomDirection = Rnd.Get(-headingOffset, headingOffset) / 10f + target.GetSpawn().GetHeading();
        double radian = randomDirection * 3d * Math.PI / 180;
        float x1 = (float)(target.GetX() + Math.Cos(radian) * a.GetDistanceOffset());
        float y1 = (float)(target.GetY() + Math.Sin(radian) * a.GetDistanceOffset());

        Npc spawned = (Npc)SpawnEngine.SpawnObject(SpawnEngine.NewSiegeSpawn(GetWorldId(), a.GetNpcId(), locationId, SiegeRace.BALAUR,
            SiegeModType.ASSAULT, x1, y1, target.GetZ() + 0.5f, (byte)0), 1);
        spawned.GetAggroList().AddHate(target, 100000);
    }

    protected string GetBossNpcL10n()
    {
        if (boss != null && boss.GetObjectTemplate() != null)
            return boss.GetObjectTemplate().GetL10n();
        return "";
    }
}
