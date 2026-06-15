using Aion.GameServer.Ai;
using Aion.GameServer.Custom.Instance;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/portals/CustomInstanceTeleporter (Estrayl).</summary>
[AIName("custom_instance_teleporter")]
public class CustomInstanceTeleporter : ActionItemNpcAI
{
    public CustomInstanceTeleporter(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        if (player.GetLevel() < 65)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_INSTANCE_ENTER_LEVEL());
            return;
        }
        if (!CustomInstanceService.GetInstance().CanEnter(player.GetObjectId()))
        {
            PacketSendUtility.SendMessage(player, "You have already done this instance for today. You can re-enter it after 9 AM.",
                ChatType.BRIGHT_YELLOW_CENTER);
            return;
        }
        CustomInstanceService.GetInstance().OnEnter(player);
    }
}
