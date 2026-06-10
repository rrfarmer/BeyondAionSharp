using Aion.GameServer.Model.Team.Legion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_GM_SHOW_LEGION_INFO (Yeats). GM variant of SM_LEGION_INFO. SM_LEGION_INFO base red-tolerated (not yet ported).</summary>
public class SM_GM_SHOW_LEGION_INFO : SM_LEGION_INFO
{
    public SM_GM_SHOW_LEGION_INFO(Legion legion)
        : base(legion)
    {
    }
}
