using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Network.Aion;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_SHOW_DIALOG (alexa026, Avol, ATracer). Requests an NPC dialog; drops hide effects if the NPC can't be talked to while invisible. Npc/TalkInfo red-tolerated.</summary>
public class CM_SHOW_DIALOG : AionClientPacket
{
    private int targetObjectId;

    public CM_SHOW_DIALOG(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        targetObjectId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player.IsProtectionActive())
            player.GetController().StopProtectionActiveTask();

        if (player.IsTrading())
            return;

        if (player.GetKnownList().GetObject(targetObjectId) is Npc target)
        {
            TalkInfo talkInfo = target.GetObjectTemplate().GetTalkInfo();
            if (talkInfo != null && !talkInfo.IsCanTalkInvisible() && player.IsInAnyHide())
                player.GetEffectController().RemoveHideEffects();
            target.GetController().OnDialogRequest(player);
        }
    }
}
