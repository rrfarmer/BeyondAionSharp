using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model;

namespace Aion.GameServer.Retail;

/// <summary>Exact source/context reviews, never a general PET or malformed-data
/// fallback. Full evidence: stats-only-20261025-01/context-worker/REPORT.md.</summary>
internal static class RetailNpcStatSourceExceptions
{
    internal static bool IsUnownedRiftGenerator(NpcTemplate host, string sourceHash) =>
        host.GetTemplateId() == 209486 && host.GetNpcTemplateType() == NpcTemplateType.ABYSS_GUARD
        && host.GetAiName() == "battle_cycle" && host.GetTribe() == TribeClass.PET
        && sourceHash == "ec3dde39c34a8e853f00d9c245075090c6b9dab1b0d95f9a3a0431731a30f559";
}
