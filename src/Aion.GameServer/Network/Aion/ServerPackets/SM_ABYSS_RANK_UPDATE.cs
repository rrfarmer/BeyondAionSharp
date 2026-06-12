using Aion.GameServer.Utils.Stats;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ABYSS_RANK_UPDATE (Nemiroff). Broadcasts a player's abyss-rank change (0), team id (1), or mentor-status (2). Converges AbyssPointsService SM_ABYSS_RANK_UPDATE(0, player). switch-on-action. AionServerPacket/AbyssRank red-tolerated.</summary>
public class SM_ABYSS_RANK_UPDATE : AionServerPacket
{
    private readonly Player player;
    private readonly int action;

    public SM_ABYSS_RANK_UPDATE(int action, Player player)
    {
        this.action = action;
        this.player = player;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(action);
        WriteD(player.GetObjectId());
        switch (action)
        {
            case 0: // Abyss rank change
                WriteD(player.GetAbyssRank().GetRank().GetId());
                break;
            case 1: // Team objectId
                WriteD(player.GetCurrentTeamId());
                break;
            case 2: // Mentor status change
                if (player.IsMentor())
                    WriteD(1);
                else
                    WriteD(0);
                break;
        }
    }
}
