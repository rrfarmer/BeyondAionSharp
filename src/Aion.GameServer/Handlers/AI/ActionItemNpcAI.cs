using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/ActionItemNpcAI (xTz, vlog).</summary>
[AIName("useitem")]
public class ActionItemNpcAI : NpcAI
{
    protected readonly int startBarAnimation = 1;
    protected readonly int cancelBarAnimation = 2;
    private readonly List<ItemUseObserver> observers = new List<ItemUseObserver>();

    public ActionItemNpcAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        if (DialogService.IsInteractionAllowed(player, GetOwner()))
            HandleUseItemStart(player);
    }

    protected void HandleUseItemStart(Player player)
    {
        int talkDelayInMs = GetTalkDelayInMs();
        if (talkDelayInMs > 0)
        {
            ItemUseObserver observer = new ActionItemUseObserver(this, player);

            player.GetObserveController().AddObserver(observer);
            lock (observers)
            {
                observers.Add(observer);
            }
            PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), GetObjectId(), talkDelayInMs, startBarAnimation));
            PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.START_QUESTLOOT, 0, GetObjectId()), true);
            player.GetController().AddTask(TaskId.ACTION_ITEM_NPC, ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.END_QUESTLOOT, 0, GetObjectId()), true);
                PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), GetObjectId(), talkDelayInMs, cancelBarAnimation));
                player.GetObserveController().RemoveObserver(observer);
                lock (observers)
                {
                    observers.Remove(observer);
                }
                HandleUseItemFinish(player);
                return System.Threading.Tasks.ValueTask.CompletedTask;
            }, (long)talkDelayInMs));
        }
        else
        {
            HandleUseItemFinish(player);
        }
    }

    protected virtual void HandleUseItemFinish(Player player)
    {
        AIActions.HandleUseItemFinish(this, player);
    }

    protected virtual int GetTalkDelayInMs()
    {
        return GetObjectTemplate().GetTalkDelay() * 1000;
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        lock (observers)
        {
            for (int i = observers.Count - 1; i >= 0; i--)
            {
                ItemUseObserver observer = observers[i];
                observers.RemoveAt(i);
                observer.Abort();
            }
        }
    }

    private sealed class ActionItemUseObserver : ItemUseObserver
    {
        private readonly ActionItemNpcAI ai;
        private readonly Player player;

        public ActionItemUseObserver(ActionItemNpcAI ai, Player player)
        {
            this.ai = ai;
            this.player = player;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(TaskId.ACTION_ITEM_NPC);
            PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.END_QUESTLOOT, 0, ai.GetObjectId()), true);
            PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), ai.GetObjectId(), 0, ai.cancelBarAnimation));
            lock (ai.observers)
            {
                ai.observers.Remove(this);
            }
            player.GetObserveController().RemoveObserver(this);
        }
    }
}
