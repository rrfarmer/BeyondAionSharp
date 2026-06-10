using System.Collections.Generic;
using Aion.GameServer.Model.LegionDominion;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_LEGION_DOMINION_REQUEST_RANKING (Yeats). Requests the legion-dominion ranking for a stonespear location (1-6). LegionDominionService red-tolerated.</summary>
public class CM_LEGION_DOMINION_REQUEST_RANKING : AionClientPacket
{
    int stonespearId;

    public CM_LEGION_DOMINION_REQUEST_RANKING(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        stonespearId = ReadD();
    }

    protected override void RunImpl()
    {
        if (stonespearId >= 1 && stonespearId <= 6)
        { //idk sometimes it sends different bytes! TODO
            LegionDominionLocation location = LegionDominionService.GetInstance().GetLegionDominionLoc(stonespearId);
            Legion legion = GetConnection().GetActivePlayer().GetLegion();
            SendPacket(new SM_LEGION_DOMINION_RANK(location, legion));
        }
    }
}
