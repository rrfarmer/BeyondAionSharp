using Aion.GameServer.Ai;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Network.Aion.ServerPackets;
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
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId != DialogAction.SETPRO1)
            return true;
        if (!player.GetInventory().DecreaseByItemId(186000124, 1))
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1097));
        }
        else
        {
            IInstanceHandler handler = GetPosition().GetWorldMapInstance().GetInstanceHandler();
            InstanceScore instance = handler.GetInstanceScore();
            if (instance != null)
            {
                CruciblePlayerReward reward = (CruciblePlayerReward)instance.GetPlayerReward(player.GetObjectId());
                if (reward != null)
                {
                    reward.SetPlayerDefeated(false);
                }
            }
            handler.PortToStartPosition(player);
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
            PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_FRIENDLY_MOVE_COMBATAREA_IDARENA(player.GetName()));
        }
        return true;
    }
}
