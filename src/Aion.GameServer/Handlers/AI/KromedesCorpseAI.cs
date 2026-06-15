using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/kromedesTrial/KromedesCorpseAI (Gigi).</summary>
[AIName("krcorpse")]
public class KromedesCorpseAI : NpcAI
{
    public KromedesCorpseAI(Npc owner)
        : base(owner)
    {
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SELECT1_1)
        {
            if (player.GetInventory().GetItemCountByItemId(164000141) < 1)
            {
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1012));
                ItemService.AddItem(player, 164000141, 1);
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_IDCROMEDE_SKILL_01()); // TODO: more sys messages
            }
            else
            {
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), DialogPage.NO_RIGHT.Id()));
            }
        }

        return true;
    }

    protected override void HandleDialogStart(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }
}
