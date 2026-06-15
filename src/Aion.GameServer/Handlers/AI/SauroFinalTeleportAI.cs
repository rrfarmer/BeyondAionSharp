using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Cheatkiller
/// </summary>
[AIName("saurohiddenpassage")]
public class SauroFinalTeleportAI : NpcAI
{
    public SauroFinalTeleportAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        SwitchWay(player, dialogActionId);
        return true;
    }

    private void SwitchWay(Player player, int dialogActionId)
    {
        switch (dialogActionId)
        {
            case DialogAction.SELECT_BOSS_LEVEL3:
                CheckKeys(player, 1);
                break;
            case DialogAction.SELECT_BOSS_LEVEL4:
                CheckKeys(player, 2);
                break;
        }
    }

    private void CheckKeys(Player player, long keyCount)
    {
        Item keys = player.GetInventory().GetFirstItemByItemId(185000179);
        int portal = (int)(730875 + keyCount);
        if (keys != null && keys.GetItemCount() >= keyCount)
        {
            Spawn(portal, 127.5f, 432.8f, 151, (sbyte)119);
            player.GetInventory().DecreaseByItemId(185000179, keys.GetItemCount());
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
            AIActions.DeleteOwner(this);
            PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDVritra_Base_DoorOpen_09());
        }
        else
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1352));
        }
    }
}
