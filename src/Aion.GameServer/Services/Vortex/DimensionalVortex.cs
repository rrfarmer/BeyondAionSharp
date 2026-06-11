using System.Collections.Generic;
using Aion.GameServer.Utils;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Vortex;
using Aion.GameServer.Services;

namespace Aion.GameServer.Services.Vortex;

/// <summary>Java parity: services/vortex/DimensionalVortex&lt;VL extends VortexLocation&gt; (Source) abstract base. C# generic constraint where VL : VortexLocation; AtomicBoolean finished; synchronized(this)->lock double-start guard; abstract invasion/player/defender/invader hooks; initRiftGenerator (find generator npc 209487/209486, DeathObserver->stopInvasion, NullPointer->NullReference); spawn/despawn via VortexService. VortexLocation/VortexService/DeathObserver red-tolerated.</summary>
public abstract class DimensionalVortex<VL> where VL : VortexLocation
{
    private readonly VL vortexLocation;
    private readonly AtomicBoolean finished = new AtomicBoolean();
    private bool started;

    protected abstract void StartInvasion();

    protected abstract void StopInvasion();

    public abstract void AddPlayer(Player player, bool isInvader);

    public abstract void KickPlayer(Player player, bool isInvader);

    public abstract void UpdateDefenders(Player defender);

    public abstract void UpdateInvaders(Player invader);

    public abstract Dictionary<int, Player> GetDefenders();

    public abstract Dictionary<int, Player> GetInvaders();

    public DimensionalVortex(VL vortexLocation)
    {
        this.vortexLocation = vortexLocation;
    }

    public void Start()
    {
        bool doubleStart = false;

        lock (this)
        {
            if (started)
            {
                doubleStart = true;
            }
            else
            {
                started = true;
            }
        }

        if (doubleStart)
        {
            return;
        }

        StartInvasion();
    }

    public void Stop()
    {
        if (finished.CompareAndSet(false, true))
        {
            StopInvasion();
        }
    }

    protected void InitRiftGenerator()
    {
        Npc gen = null;
        foreach (VisibleObject obj in GetVortexLocation().GetSpawned())
        {
            int npcId = ((Npc)obj).GetNpcId();
            if (npcId == 209487 || npcId == 209486)
            {
                gen = (Npc)obj;
            }
        }

        if (gen == null)
        {
            throw new System.NullReferenceException("No generator was found in loc:" + GetVortexLocationId());
        }
        gen.GetObserveController().Attach(new DeathObserver(_ => VortexService.GetInstance().StopInvasion(GetVortexLocationId())));
    }

    protected void Spawn(VortexStateType type)
    {
        VortexService.GetInstance().Spawn(GetVortexLocation(), type);
    }

    protected void Despawn()
    {
        VortexService.GetInstance().Despawn(GetVortexLocation());
    }

    public bool IsFinished()
    {
        return finished.Get();
    }

    public VL GetVortexLocation()
    {
        return vortexLocation;
    }

    public int GetVortexLocationId()
    {
        return vortexLocation.GetId();
    }
}
