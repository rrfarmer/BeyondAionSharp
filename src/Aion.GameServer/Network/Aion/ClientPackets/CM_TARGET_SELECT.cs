using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_TARGET_SELECT (SoulKeeper, Sweetkr, KID). Selects a target via click/hotkey/chat, including assist (target-of-target). VisibleObject/AuditLogger red-tolerated.</summary>
public class CM_TARGET_SELECT : AionClientPacket
{
    /// <summary>Target object id that client wants to select or 0 if wants to unselect</summary>
    private int targetObjectId;
    private bool selectTargetOfTarget;

    public CM_TARGET_SELECT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        targetObjectId = ReadD();
        selectTargetOfTarget = ReadC() == 1;
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        VisibleObject newTarget;
        if (selectTargetOfTarget)
        {
            if (player.GetTarget() == null)
            {
                SendPacket(SM_SYSTEM_MESSAGE.STR_ASSISTKEY_THIS_IS_ASSISTKEY());
                return;
            }
            newTarget = player.GetTarget().GetTarget();
            if (newTarget == null)
            {
                SendPacket(SM_SYSTEM_MESSAGE.STR_ASSISTKEY_NO_USER());
                return;
            }
            if (!newTarget.Equals(player) && !player.GetKnownList().Sees(newTarget))
            {
                SendPacket(player.GetKnownList().Knows(newTarget) ? SM_SYSTEM_MESSAGE.STR_ASSISTKEY_NO_USER() : SM_SYSTEM_MESSAGE.STR_ASSISTKEY_TOO_FAR());
                return;
            }
        }
        else if (targetObjectId == 0)
        {
            newTarget = null;
        }
        else if (targetObjectId == player.GetObjectId())
        {
            newTarget = player;
        }
        else
        {
            newTarget = player.GetKnownList().GetObject(targetObjectId);
            if (newTarget == null && player.IsInTeam() && player.GetCurrentTeam().HasMember(targetObjectId))
                newTarget = player.GetCurrentTeam().GetMember(targetObjectId).GetObject();
            else if (newTarget != null && !player.Equals(newTarget) && !player.GetKnownList().Sees(newTarget))
            {
                AuditLogger.Log(player, "possibly used radar hack: trying to target invisible " + newTarget);
                newTarget = null;
            }
        }
        player.SetTarget(newTarget);
    }
}
