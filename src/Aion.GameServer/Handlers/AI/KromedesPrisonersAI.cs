using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/kromedesTrial/KromedesPrisonersAI (Gigi).</summary>
[AIName("krprisoners")]
public class KromedesPrisonersAI : NpcAI
{
    public KromedesPrisonersAI(Npc owner)
        : base(owner)
    {
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SETPRO1)
        {
            AIActions.DeleteOwner(this);
        }
        else if (dialogActionId == DialogAction.SELECT1_1)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1012));
        }

        return true;
    }

    protected override void HandleDialogStart(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }
}
