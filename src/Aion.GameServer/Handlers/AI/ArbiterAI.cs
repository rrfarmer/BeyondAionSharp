using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/crucibleChallenge/ArbiterAI (@author xTz).</summary>
[AIName("arbiter")]
public class ArbiterAI : NpcAI
{
    public ArbiterAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        if (player.GetInventory().GetFirstItemByItemId(186000134) != null)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
        }
        else
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
        }
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        int instanceId = GetPosition().GetInstanceId();
        if (dialogActionId == DialogAction.SETPRO1 && player.GetInventory().DecreaseByItemId(186000134, 1))
        {
            switch (GetNpcId())
            {
                case 205682:
                    TeleportService.TeleportTo(player, 300320000, instanceId, 357.10208f, 1662.702f, 95.9803f, (byte)60);
                    break;
                case 205683:
                    TeleportService.TeleportTo(player, 300320000, instanceId, 1796.5513f, 306.9967f, 469.25f, (byte)60);
                    break;
                case 205684:
                    TeleportService.TeleportTo(player, 300320000, instanceId, 1324.433f, 1738.2279f, 316.476f, (byte)70);
                    break;
                case 205663:
                    TeleportService.TeleportTo(player, 300320000, instanceId, 1270.8877f, 237.93307f, 405.38028f, (byte)60);
                    break;
                case 205686:
                    TeleportService.TeleportTo(player, 300320000, instanceId, 357.98798f, 349.19116f, 96.09108f, (byte)60);
                    break;
                case 205687:
                    TeleportService.TeleportTo(player, 300320000, instanceId, 1759.5004f, 1273.5414f, 389.11743f, (byte)10);
                    break;
                case 205685:
                    TeleportService.TeleportTo(player, 300320000, instanceId, 1283.1246f, 791.6683f, 436.6403f, (byte)60);
                    break;
            }
        }

        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
        return true;
    }
}
