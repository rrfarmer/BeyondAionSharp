using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates.Challenge;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CHALLENGE_LIST (Rolandas). Requests legion/town challenge-task list. ChallengeTaskService/ChallengeType/AuditLogger red-tolerated.</summary>
public class CM_CHALLENGE_LIST : AionClientPacket
{
    public CM_CHALLENGE_LIST(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    int action;
    int taskOwner;
    int ownerType;
    int playerId;
    int dateSince;

    protected override void ReadImpl()
    {
        action = ReadUC();
        taskOwner = ReadD();
        ownerType = ReadUC();
        playerId = ReadD();
        dateSince = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (ownerType == 1)
        {
            if (player.GetLegion() == null)
            {
                AuditLogger.Log(player, "tried to receive legion challenge task without legion");
                return;
            }
            ChallengeTaskService.GetInstance().ShowTaskList(player, ChallengeType.LEGION, taskOwner);
        }
        else
        {
            ChallengeTaskService.GetInstance().ShowTaskList(player, ChallengeType.TOWN, taskOwner);
        }
    }
}
