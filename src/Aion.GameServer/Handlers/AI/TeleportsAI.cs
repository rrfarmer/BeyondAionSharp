using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/engulfedOphidianBridgeInstance/TeleportsAI (cheatkiller).</summary>
[AIName("engulfedophidianteleport")]
public class TeleportsAI : ActionItemNpcAI
{
    public TeleportsAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (CheckScroll(player))
        {
            if (dialogActionId == DialogAction.SETPRO1)
            {
                // TP North
                TeleportService.TeleportTo(player, 301210000, 576.3873f, 462.34897f, 618.9187f, (byte)26);
                player.GetInventory().DecreaseByItemId(164000279, 1);
            }
            else if (dialogActionId == DialogAction.SETPRO2)
            {
                // TP South
                TeleportService.TeleportTo(player, 301210000, 608.5137f, 518.11066f, 591.4151f, (byte)0);
                player.GetInventory().DecreaseByItemId(164000279, 1);
            }
        }
        else
        {
            PacketSendUtility.BroadcastToMap(GetOwner(), 1402004);
        }
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
        return true;
    }

    private bool CheckScroll(Player player)
    {
        Item key = player.GetInventory().GetFirstItemByItemId(164000279);
        if (key != null && key.GetItemCount() >= 1)
        {
            return true;
        }
        return false;
    }
}
