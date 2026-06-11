using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Model.Team.League;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Restrictions;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_GROUP_DISTRIBUTION (Lyahim, Simple, xTz). Distributes kinah across group/alliance/league by partyType. PlayerGroupService/PlayerAllianceService/LeagueService red-tolerated.</summary>
public class CM_GROUP_DISTRIBUTION : AionClientPacket
{
    private long amount;
    private byte partyType;

    public CM_GROUP_DISTRIBUTION(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        amount = ReadQ();
        partyType = ReadC();
    }

    protected override void RunImpl()
    {
        if (amount < 2)
            return;

        Player player = GetConnection().GetActivePlayer();

        if (!PlayerRestrictions.CanTrade(player))
            return;

        switch (partyType)
        {
            case 1:
                if (player.IsInAlliance())
                {
                    PlayerAllianceService.DistributeKinahInGroup(player, amount);
                }
                else
                {
                    PlayerGroupService.DistributeKinah(player, amount);
                }
                break;
            case 2:
                PlayerAllianceService.DistributeKinah(player, amount);
                break;
            case 3:
                LeagueService.DistributeKinah(player, amount);
                break;
        }
    }
}
