using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/beshmundirTemple/Door3AI (Gigi).</summary>
[AIName("door3")]
public class Door3AI : ActionItemNpcAI
{
    public Door3AI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        if (player.GetInventory().GetItemCountByItemId(185000091) > 0)
        {
            base.HandleDialogStart(player);
        }
        else
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), DialogPage.NO_RIGHT.Id()));
        }
    }

    protected override void HandleUseItemFinish(Player player)
    {
        AIActions.DeleteOwner(this);
    }
}
