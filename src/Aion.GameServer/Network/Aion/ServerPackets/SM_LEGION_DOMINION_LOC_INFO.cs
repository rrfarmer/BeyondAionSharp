using Aion.GameServer.Model.LegionDominion;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LEGION_DOMINION_LOC_INFO (Neon, Yeats). Sends each legion-dominion location with owning legion + emblem (id/type/argb) + name. Converges PlayerEnterWorldService. LegionDominionLocation/Legion/LegionEmblem/services red-tolerated.</summary>
public class SM_LEGION_DOMINION_LOC_INFO : AionServerPacket
{
    // Java parity (writeImpl audited 1:1 vs game-server/src/com/aionemu/gameserver/network/aion/serverpackets/SM_LEGION_DOMINION_LOC_INFO.java): 2026-06-17
    // TIER-2 audit-only: reads live LegionDominionService + LegionService singletons + full Legion/emblem graph (no bounded unit seam).
    protected override void WriteImpl(AionConnection con)
    {
        WriteH(LegionDominionService.GetInstance().GetLegionDominions().Count);
        foreach (LegionDominionLocation loc in LegionDominionService.GetInstance().GetLegionDominions())
        {
            Legion legion = loc.GetLegionId() == 0 ? null : LegionService.GetInstance().GetLegion(loc.GetLegionId());
            LegionEmblem emblem = legion == null ? new LegionEmblem() : legion.GetLegionEmblem();
            WriteD(loc.GetLocationId());
            WriteD(loc.GetLegionId());
            WriteC(emblem.GetEmblemId());
            WriteC(emblem.GetEmblemType().GetValue());
            WriteC(emblem.GetColor_a());
            WriteC(emblem.GetColor_r());
            WriteC(emblem.GetColor_g());
            WriteC(emblem.GetColor_b());
            WriteS(legion == null ? null : legion.GetName());
        }
    }
}
