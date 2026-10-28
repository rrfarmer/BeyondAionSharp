using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Retail;

/// <summary>Actual enabled-cohort spawn identities for local play evidence. Source table
/// assertions and headless tests must not be relabeled as an observed client encounter.</summary>
internal static class SkyTempleNpcDiagnostics
{
    private static readonly ILogger Log = AionLog.For(nameof(SkyTempleNpcDiagnostics));
    internal static void Spawned(Npc npc)
    {
        if (!SkyTempleNpcProfileCatalog.TryGet(npc, out var profile)) return;
        Log.LogInformation("RETAIL_SKYTEMPLE_SPAWN npc={Npc} object={Object} map={Map} instance={Instance} level={Level} hp={Hp} maxHp={MaxHp} pdef={Pdef} mdef={Mdef} x={X} y={Y} z={Z} ai={Ai} generation={Generation} record={Record}",
            npc.GetNpcId(), npc.GetObjectId(), npc.GetWorldId(), npc.GetInstanceId(), npc.GetLevel(),
            npc.GetLifeStats().GetCurrentHp(), npc.GetLifeStats().GetMaxHp(),
            npc.GetGameStats().GetPDef().GetCurrent(), npc.GetGameStats().GetMDef().GetCurrent(),
            npc.GetX(), npc.GetY(), npc.GetZ(), npc.GetAi().GetType().Name, profile.Generation, profile.SourceRecordSha256);
    }

    internal static void Regenerated(Npc npc, int before, int after, int requested)
    {
        if (!SkyTempleNpcProfileCatalog.TryGet(npc, out var profile)) return;
        Log.LogInformation("RETAIL_SKYTEMPLE_REGEN npc={Npc} object={Object} map={Map} instance={Instance} before={Before} after={After} requested={Requested} state={State} life={Life} generation={Generation}",
            npc.GetNpcId(), npc.GetObjectId(), npc.GetWorldId(), npc.GetInstanceId(), before, after,
            requested, npc.GetAi().GetState(), npc.GetController().RetailLifeEpoch, profile.Generation);
    }
}
