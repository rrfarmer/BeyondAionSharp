using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Model.Team.Common.Service;
using Aion.GameServer.Model.Team.League;
using Aion.GameServer.Network.Aion;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_PLAYER_STATUS_INFO (Lyahim, ATracer, Simple, xTz). Team command dispatcher (LFG / alliance group change / league alliance move / generic). TeamCommand/PlayerTeamCommandService red-tolerated.</summary>
public class CM_PLAYER_STATUS_INFO : AionClientPacket
{
    private int commandCode;
    private int selectedObjectId;
    private int allianceGroupId;
    private int secondObjectId;

    public CM_PLAYER_STATUS_INFO(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        commandCode = ReadUC();
        selectedObjectId = ReadD();
        allianceGroupId = ReadD();
        secondObjectId = ReadD();
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        TeamCommand command = TeamCommandExtensions.GetCommand(commandCode);
        switch (command)
        {
            case TeamCommand.GROUP_SET_LFG:
                activePlayer.SetLookingForGroup(selectedObjectId == 2);
                break;
            case TeamCommand.ALLIANCE_CHANGE_GROUP:
                PlayerAllianceService.ChangeMemberGroup(activePlayer, selectedObjectId, secondObjectId, allianceGroupId);
                break;
            case TeamCommand.LEAGUE_ALLIANCE_MOVE:
                LeagueService.MoveAlliance(activePlayer, selectedObjectId, allianceGroupId);
                break;
            default:
                PlayerTeamCommandService.ExecuteCommand(activePlayer, command, selectedObjectId);
                break;
        }
    }
}
