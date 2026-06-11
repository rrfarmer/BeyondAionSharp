using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LEGION_UPDATE_TITLE (sweetkr). Updates a player's legion title (objId/legionId/name/rank). LegionRank red-tolerated.</summary>
public class SM_LEGION_UPDATE_TITLE : AionServerPacket
{
    private readonly int playerObjectId;
    private readonly int legionId;
    private readonly string legionName;
    private readonly LegionRank rank;

    public SM_LEGION_UPDATE_TITLE(int playerObjectId, int legionId, string legionName, LegionRank rank)
    {
        this.playerObjectId = playerObjectId;
        this.legionId = legionId;
        this.legionName = legionName;
        this.rank = rank;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(playerObjectId);
        WriteD(legionId);
        WriteS(legionName);
        WriteC(rank.GetRankId());
    }
}
