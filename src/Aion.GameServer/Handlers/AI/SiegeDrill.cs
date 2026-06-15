using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Handler;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author xTz
/// </summary>
[AIName("siege_drill")]
public class SiegeDrill : NpcAI
{
    protected int startBarAnimation = 1;
    protected int cancelBarAnimation = 2;
    private readonly AtomicBoolean isUsed = new AtomicBoolean(false);

    public SiegeDrill(Npc owner) : base(owner)
    {
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 0;
    }

    protected override void HandleDialogStart(Player player)
    {
        if (isUsed.Get())
        {
            return;
        }
        HandleUseItemStart(player);
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        MoveEventHandler.OnMoveArrived(this);
    }

    protected override void HandleAttack(Creature creature)
    {
        // do nothing
    }

    protected void HandleUseItemStart(Player player)
    {
        int delay = GetTalkDelay();
        if (delay > 1)
        {
            ItemUseObserver observer = new SiegeDrillObserver(this, player);

            player.GetObserveController().Attach(observer);
            PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), GetObjectId(), GetTalkDelay(), startBarAnimation));
            PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.START_QUESTLOOT, 0, GetObjectId()), true);
            player.GetController().AddTask(TaskId.ACTION_ITEM_NPC, ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.END_QUESTLOOT, 0, GetObjectId()), true);
                PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), GetObjectId(), GetTalkDelay(), cancelBarAnimation));
                player.GetObserveController().RemoveObserver(observer);
                HandleUseItemFinish(player);
                return ValueTask.CompletedTask;
            }, (long)delay));
        }
        else
        {
            HandleUseItemFinish(player);
        }
    }

    protected void HandleUseItemFinish(Player player)
    {
        if (!player.GetInventory().DecreaseByItemId(185000174, 1))
        {
            PacketSendUtility.BroadcastToMap(GetOwner(), 1401932);
            return;
        }
        if (isUsed.CompareAndSet(false, true))
        {
            GetOwner().GetSpawn().SetWalkerId("2A2D7F6EA351DCCEAA3CD097B9311ED308DCD2EC");
            WalkManager.StartWalking(this);
            GetOwner().SetState(CreatureState.ACTIVE, true);
            PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                Npc npc = GetPosition().GetWorldMapInstance().GetNpc(233189);
                if (npc != null)
                {
                    GetOwner().GetSpawn().SetWalkerId(null);
                    WalkManager.StopWalking(this);
                    GetOwner().SetTarget(npc);

                    SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20778, 65, npc).UseSkill();
                    ThreadPoolManager.GetInstance().Schedule(ct2 =>
                    {
                        AIActions.DeleteOwner(this);
                        return ValueTask.CompletedTask;
                    }, 5000L);
                }
                return ValueTask.CompletedTask;
            }, 4800L);
        }
    }

    protected int GetTalkDelay()
    {
        return GetObjectTemplate().GetTalkDelay() * 1000;
    }

    private sealed class SiegeDrillObserver : ItemUseObserver
    {
        private readonly SiegeDrill ai;
        private readonly Player player;

        public SiegeDrillObserver(SiegeDrill ai, Player player)
        {
            this.ai = ai;
            this.player = player;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(TaskId.ACTION_ITEM_NPC);
            PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.END_QUESTLOOT, 0, ai.GetObjectId()), true);
            PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), ai.GetObjectId(), 0, ai.cancelBarAnimation));
            player.GetObserveController().RemoveObserver(this);
        }
    }
}
