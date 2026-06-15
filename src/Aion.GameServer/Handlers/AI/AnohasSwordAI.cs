using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Controllers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Ritsu, Estrayl, Neon
/// </summary>
[AIName("anohas_sword")]
public class AnohasSwordAI : NpcAI
{
    public AnohasSwordAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        int fortressLegionId = SiegeService.GetInstance().GetFortress(7011).GetLegionId();
        if (player.GetLegion() != null && player.GetLegion().GetLegionId() == fortressLegionId && player.GetLegionMember().IsBrigadeGeneral())
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 10));
        else
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId != DialogAction.SETPRO1)
            return false;

        if (!player.GetInventory().DecreaseByItemId(185000215, 1))
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), DialogPage.NO_RIGHT.Id()));
            return false;
        }

        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 2375));
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_LDF5_FORTRESS_NAMED_SPAWN_ITEM());
        World.World.GetInstance().ForEachPlayer(receiver =>
        {
            PacketSendUtility.SendPacket(receiver, SM_SYSTEM_MESSAGE.STR_MSG_LDF5_FORTRESS_NAMED_SPAWN());
            PacketSendUtility.SendPacket(receiver, SM_SYSTEM_MESSAGE.STR_MSG_LDF5_FORTRESS_NAMED_SPAWN_SYSTEM());
        });
        Npc sword = GetOwner();
        NpcController swordController = sword.GetController();
        AIActions.Die(this); // kill sword (starts animation)
        swordController.AddTask(TaskId.DECAY, ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            PacketSendUtility.BroadcastPacket(sword, new SM_EMOTION(sword, EmotionType.RESURRECT));
            PacketSendUtility.BroadcastPacket(sword, new SM_EMOTION(sword, EmotionType.DIE));
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(10 * 1000), System.TimeSpan.FromMilliseconds(10 * 1000))); // disable despawn, repeat special die animation instead until anoha spawns
        Spawn(702618, 791.38214f, 488.93655f, 143.47617f, (sbyte)30); // map icon
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            Spawn(855263, 791.38214f, 488.93655f, 143.47517f, (sbyte)30); // Berserk Anoha
            swordController.Delete(); // despawn sword
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMinutes(30));
        return true;
    }

    public override bool Ask(AIQuestion question)
    {
        return question == AIQuestion.ALLOW_RESPAWN ? false : base.Ask(question);
    }
}
