using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Autogroup;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author xTz
/// </summary>
[AIName("t_a_g_portal")]
public class TAGPortalAI : PortalDialogAI
{
    public TAGPortalAI(Npc owner) : base(owner)
    {
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (questId != 0)
        {
            base.OnDialogSelect(player, dialogActionId, questId, extendedRewardIndex);
            return true;
        }
        int worldId = dialogActionId switch
        {
            DialogAction.SETPRO1 => 300430000,
            DialogAction.SETPRO2 => 300420000,
            DialogAction.SETPRO3 => 300570000,
            _ => 0,
        };
        AutoGroupType? agt = AutoGroupTypeExtensions.GetAutoGroupByWorld(player.GetLevel(), worldId);
        if (agt != null)
        {
            PacketSendUtility.SendPacket(player, new SM_AUTO_GROUP(agt.Value.GetTemplate().GetMaskId()));
        }
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
        return true;
    }
}
