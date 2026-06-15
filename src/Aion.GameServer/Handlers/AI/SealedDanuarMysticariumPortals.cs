using Aion.GameServer.Ai;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Portal;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Created by Yeats on 22.02.2016.
/// </summary>
[AIName("sealed_danuar_mysticarium_portal")]
public class SealedDanuarMysticariumPortals : PortalDialogAI
{
    public SealedDanuarMysticariumPortals(Npc owner) : base(owner)
    {
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (GetNpcId() == 730721 || GetNpcId() == 730722)
        {
            PortalPath portalPath = DataManager.PORTAL2_DATA.GetPortalDialogPath(GetNpcId(), dialogActionId, player);
            WorldMapInstance instance = InstanceService.GetRegisteredInstance(300480000, player.GetObjectId());
            if (instance == null)
            {
                if (!player.GetPortalCooldownList().IsPortalUseDisabled(300480000))
                {
                    if (player.GetInventory().DecreaseByItemId(185000223, 1))
                    {
                        PortalService.Port(portalPath, player, GetOwner());
                        return true;
                    }
                    else
                    {
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_CANT_ENTER_WITHOUT_ITEM());
                        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
                        return true;
                    }
                }
                else
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANNOT_MAKE_INSTANCE_COOL_TIME());
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
                    return true;
                }
            }
            else
            {
                PortalService.Port(portalPath, player, GetOwner());
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
                return true;
            }
        }
        else
        {
            if (!GetOwner().IsInInstance())
            {
                return true;
            }
            if (GetNpcId() == 731583)
            {
                if (dialogActionId == DialogAction.SETPRO1)
                {
                    AIActions.HandleUseItemFinish(this, player);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0, questId));
                }
            }
        }
        return true;
    }
}
