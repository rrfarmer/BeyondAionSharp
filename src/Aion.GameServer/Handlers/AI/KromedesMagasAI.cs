using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/kromedesTrial/KromedesMagasAI (@author Gigi).</summary>
[AIName("krmagas")]
public class KromedesMagasAI : NpcAI
{
    public KromedesMagasAI(Npc owner)
        : base(owner)
    {
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SETPRO1)
        {
            if (player.GetInventory().GetItemCountByItemId(185000109) > 0)
            {
                PacketSendUtility.SendPacket(player, new SM_PLAY_MOVIE(false, 0, player.GetRace() == Race.ELYOS ? 18602 : 28602, 454, true));
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
            }
            else
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), DialogPage.NO_RIGHT.Id()));
        }
        else if (dialogActionId == DialogAction.SELECT1_1)
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1012));
        return true;
    }

    protected override void HandleDialogStart(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }
}
