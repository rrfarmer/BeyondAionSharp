using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/levinshor/VocolithAI (@author Yeats, Neon).</summary>
[AIName("LDF4_Advance_Vocolith")]
public class VocolithAI : GeneralNpcAI
{
    private readonly AtomicBoolean used = new AtomicBoolean();

    public VocolithAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        if (!used.Get())
        {
            ItemUseObserver observer = new VocolithItemUseObserver(this, player);
            int delay = 1500;
            player.GetObserveController().Attach(observer);
            PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), GetObjectId(), delay, 1));
            PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.START_QUESTLOOT, 0, GetObjectId()), true);
            player.GetController().AddTask(TaskId.ACTION_ITEM_NPC, ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.END_QUESTLOOT, 0, GetObjectId()), true);
                PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), GetObjectId(), delay, 2));
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
                player.GetObserveController().RemoveObserver(observer);
                return System.Threading.Tasks.ValueTask.CompletedTask;
            }, (long)delay));
        }
    }

    private class VocolithItemUseObserver : ItemUseObserver
    {
        private readonly VocolithAI _ai;
        private readonly Player _player;

        public VocolithItemUseObserver(VocolithAI ai, Player player)
        {
            _ai = ai;
            _player = player;
        }

        public override void Abort()
        {
            _player.GetController().CancelTask(TaskId.ACTION_ITEM_NPC);
            PacketSendUtility.BroadcastPacket(_player, new SM_EMOTION(_player, EmotionType.END_QUESTLOOT, 0, _ai.GetObjectId()), true);
            PacketSendUtility.SendPacket(_player, new SM_USE_OBJECT(_player.GetObjectId(), _ai.GetObjectId(), 0, 2));
            _player.GetObserveController().RemoveObserver(this);
        }
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SETPRO1)
        {
            if (player.GetInventory().GetItemCountByItemId(185000216) == 0)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_FNAMED_FAIL());
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), DialogPage.NO_RIGHT.Id()));
                return true;
            }
            else if (used.CompareAndSet(false, true))
            {
                HandleUsed(player);
            }
        }
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
        return true;
    }

    private void HandleUsed(Player player)
    {
        if (player.GetInventory().DecreaseByItemId(185000216, 1))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_FNAMED_SPAWN_ITEM());
            SpawnRandomBoss();
        }
        else
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_FNAMED_FAIL());
            used.Set(false);
        }
    }

    private void SpawnRandomBoss()
    {
        Npc vocolith = GetOwner();
        AIActions.Die(this); // kill vocolith
        RespawnService.ScheduleDecayTask(vocolith, 7500); // schedule late despawn to show full death animation
        ThreadPoolManager.GetInstance().Schedule(ct => // schedule boss spawn
        {
            int npcId = 235217 + Rnd.Get(0, 3);
            Npc boss = (Npc)Spawn(npcId, vocolith.GetX(), vocolith.GetY(), vocolith.GetZ(), (sbyte)vocolith.GetHeading());
            PacketSendUtility.BroadcastToMap(boss, SM_SYSTEM_MESSAGE.STR_MSG_LDF4_ADVANCE_FNAMED_SPAWN());
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 5000L);
    }
}
