using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/empyreanCrucible/EmpyreanArbiterAI (@author xTz).</summary>
[AIName("empyreanarbiter")]
public class EmpyreanArbiterAI : NpcAI
{
    public EmpyreanArbiterAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        if (player.GetInventory().GetFirstItemByItemId(186000124) != null)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
        }
        else
        {
            // to do
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
        }
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        int instanceId = GetPosition().GetInstanceId();

        if (dialogActionId == DialogAction.SETPRO1 && player.GetInventory().DecreaseByItemId(186000124, 1))
        {
            switch (GetNpcId())
            {
                case 799573:
                    TeleportService.TeleportTo(player, 300300000, instanceId, 358.2547f, 349.26443f, 96.09108f, (byte)59);
                    break;
                case 205426:
                    TeleportService.TeleportTo(player, 300300000, instanceId, 1260.15f, 812.34f, 358.6056f, (byte)90);
                    break;
                case 205427:
                    TeleportService.TeleportTo(player, 300300000, instanceId, 1616.0248f, 154.43837f, 126f, (byte)10);
                    break;
                case 205428:
                    TeleportService.TeleportTo(player, 300300000, instanceId, 1793.9233f, 796.92f, 469.36542f, (byte)60);
                    break;
                case 205429:
                    TeleportService.TeleportTo(player, 300300000, instanceId, 1776.4169f, 1749.9952f, 303.69553f, (byte)0);
                    break;
                case 205430:
                    TeleportService.TeleportTo(player, 300300000, instanceId, 1328.935f, 1742.0771f, 316.74188f, (byte)0);
                    break;
                case 205431:
                    TeleportService.TeleportTo(player, 300300000, instanceId, 1760.9441f, 1278.033f, 394.23764f, (byte)0);
                    break;
            }
            InstanceScore instance = GetPosition().GetWorldMapInstance().GetInstanceHandler().GetInstanceScore();
            if (instance != null)
            {
                CruciblePlayerReward reward = (CruciblePlayerReward)instance.GetPlayerReward(player.GetObjectId());
                if (reward != null)
                {
                    reward.SetPlayerDefeated(false);
                }
            }
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));

            GetPosition().GetWorldMapInstance().ForEachPlayer(p =>
            {
                PacketSendUtility.SendPacket(p, SM_SYSTEM_MESSAGE.STR_MSG_FRIENDLY_MOVE_COMBATAREA_IDARENA(player.GetName()));
            });
        }
        return true;
    }
}
