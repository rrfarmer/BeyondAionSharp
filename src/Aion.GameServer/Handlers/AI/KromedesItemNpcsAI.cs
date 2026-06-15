using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/kromedesTrial/KromedesItemNpcsAI (@author Gigi, xTz).</summary>
[AIName("krobject")]
public class KromedesItemNpcsAI : ActionItemNpcAI
{
    public KromedesItemNpcsAI(Npc owner)
        : base(owner)
    {
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SELECT1_1)
        {
            switch (GetNpcId())
            {
                case 730325:
                    if (player.GetInventory().GetItemCountByItemId(164000142) < 1)
                    {
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1012));
                        ItemService.AddItem(player, 164000142, 1);
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_IDCROMEDE_SKILL_01()); // TODO: more sys messages
                    }
                    else
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), DialogPage.NO_RIGHT.Id()));
                    break;
                case 730340:
                    if (player.GetInventory().GetItemCountByItemId(164000140) < 1)
                    {
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1012));
                        ItemService.AddItem(player, 164000140, 1);
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_IDCROMEDE_SKILL_01()); // TODO: more sys messages
                    }
                    else
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), DialogPage.NO_RIGHT.Id()));
                    break;
                case 730341:
                    if (player.GetInventory().GetItemCountByItemId(164000143) < 1)
                    {
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1012));
                        ItemService.AddItem(player, 164000143, 1);
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_IDCROMEDE_SKILL_01()); // TODO: more sys messages
                    }
                    else
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), DialogPage.NO_RIGHT.Id()));
                    break;
            }
        }
        return true;
    }

    protected override void HandleUseItemFinish(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }
}
