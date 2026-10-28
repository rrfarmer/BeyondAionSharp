using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Retail;

// Ordinary ownership admission. Full-cohort interop and optional effect cleanup
// stay in their existing modules rather than becoming base-kernel dependencies.
internal static partial class RetailNpcStatCatalog
{
    internal static bool TryGet(Npc npc, out SkyTempleNpcProfile profile)
    {
        profile = null!;
        if (npc is SummonedObject || npc.GetSpawn() is not { } spawn || spawn.GetCreatorId() != 0
            || npc.GetCreatorId() != 0 || npc.GetSummonOwner() is not null
            || npc.GetGameStats()?.StatisticsContextRetired == true) return false;
        SkyTempleNpcProfile? full = null;
        ResolveFullProfile(npc, ref full);
        if (full is not null) return false;
        return TryGet(npc.GetWorldId(), npc.GetNpcId(), out profile);
    }

    internal static bool TryGetCombatProfile(Npc npc, out SkyTempleNpcProfile profile)
    {
        SkyTempleNpcProfile? full = null;
        ResolveFullProfile(npc, ref full);
        if (full is not null) { profile = full; return true; }
        return TryGet(npc, out profile);
    }

    internal static void RestoreRetiredModifiers(Npc npc) => RetireAdditionalModifiers(npc);
    static partial void ResolveFullProfile(Npc npc, ref SkyTempleNpcProfile? profile);
    static partial void RetireAdditionalModifiers(Npc npc);
}
