using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services.Summons;

/// <summary>Java parity: services/summons/TrapService (Sykra). Tracks up to TRAP_LIMIT_PER_OWNER traps per owner; registerTrap (offer + delete excess oldest) and unregisterTrap (remove by obj id, drop empty queues). ConcurrentHashMap->ConcurrentDictionary; Queue/ConcurrentLinkedQueue->ConcurrentQueue (offer->Enqueue, poll->TryDequeue); computeIfAbsent->GetOrAdd; queue.removeIf->drain+re-enqueue survivors; values().removeIf(isEmpty)->iterate keys + TryRemove. Trap red-tolerated.</summary>
public class TrapService
{
    private const int TRAP_LIMIT_PER_OWNER = 2;
    private static readonly ConcurrentDictionary<int, ConcurrentQueue<Trap>> registeredTraps = new ConcurrentDictionary<int, ConcurrentQueue<Trap>>();

    public static void RegisterTrap(int ownerObjId, Trap trap, bool removeExcessTraps)
    {
        if (trap == null || trap.IsDead())
            return;
        ConcurrentQueue<Trap> traps = registeredTraps.GetOrAdd(ownerObjId, objId => new ConcurrentQueue<Trap>());
        traps.Enqueue(trap);
        if (removeExcessTraps)
        {
            while (!traps.IsEmpty && traps.Count > TRAP_LIMIT_PER_OWNER)
            {
                if (traps.TryDequeue(out Trap firstPlacedTrap) && firstPlacedTrap != null)
                    firstPlacedTrap.GetController().Delete();
            }
        }
    }

    public static void UnregisterTrap(int trapObjId)
    {
        ICollection<ConcurrentQueue<Trap>> allTraps = registeredTraps.Values;
        foreach (ConcurrentQueue<Trap> traps in allTraps)
        {
            // Java: traps.removeIf(trap -> trap.getObjectId() == trapObjId) on a FIFO queue -> drain and re-enqueue survivors
            int count = traps.Count;
            for (int i = 0; i < count; i++)
            {
                if (traps.TryDequeue(out Trap trap))
                {
                    if (trap.GetObjectId() != trapObjId)
                        traps.Enqueue(trap);
                }
            }
        }
        // Java: allTraps.removeIf(Collection::isEmpty) -> the values() view drops empty queues from the map
        foreach (KeyValuePair<int, ConcurrentQueue<Trap>> kv in registeredTraps.ToArray())
            if (kv.Value.IsEmpty)
                registeredTraps.TryRemove(kv.Key, out _);
    }
}
