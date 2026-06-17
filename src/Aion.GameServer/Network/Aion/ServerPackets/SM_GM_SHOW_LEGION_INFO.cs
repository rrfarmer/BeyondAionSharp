using Aion.GameServer.Model.Team.Legion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_GM_SHOW_LEGION_INFO (Yeats). GM variant of SM_LEGION_INFO. SM_LEGION_INFO base red-tolerated (not yet ported).</summary>
public class SM_GM_SHOW_LEGION_INFO : SM_LEGION_INFO
{
    // Java parity (audited 1:1 vs game-server/src/com/aionemu/gameserver/network/aion/serverpackets/SM_GM_SHOW_LEGION_INFO.java): 2026-06-17. Empty GM subclass of SM_LEGION_INFO (inherits writeImpl verbatim).
    public SM_GM_SHOW_LEGION_INFO(Legion legion)
        : base(legion)
    {
    }
}
