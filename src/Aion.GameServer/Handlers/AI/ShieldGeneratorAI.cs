using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// On retail you can fake charge waves - means waves will spawn when players begin to charge.
/// But there is still no logical need to implement double-checks for this special case, so we
/// spawn them event-based via IlluminaryObeliskInstance.onSpawn().
///
/// Java parity: ai/instance/illuminaryObelisk/ShieldGeneratorAI (@author Estrayl).
/// </summary>
public abstract class ShieldGeneratorAI : GeneralNpcAI
{
    private readonly AtomicBoolean isUnderCharge = new AtomicBoolean();
    private readonly AtomicBoolean isVortexSpawned = new AtomicBoolean();
    protected readonly List<Npc> charges = new List<Npc>();
    protected int chargeCount = 0;
    private long lastAttackedTime;

    protected abstract SM_SYSTEM_MESSAGE GetAttackMsg();

    protected abstract SM_SYSTEM_MESSAGE GetChargeMsg();

    protected abstract SM_SYSTEM_MESSAGE GetGateMsg();

    protected abstract SM_SYSTEM_MESSAGE GetDestructionMsg();

    protected abstract void HandleChargeComplete();

    protected abstract void HandleVortexSpawn();

    protected abstract void HandleVortexDespawn();

    public ShieldGeneratorAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastAttackedTime > 10000)
        {
            lastAttackedTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Shout(GetAttackMsg());
        }
    }

    protected void Shout(SM_SYSTEM_MESSAGE msg)
    {
        PacketSendUtility.BroadcastToMap(GetOwner(), msg);
    }

    protected override void HandleDialogStart(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SETPRO1)
        {
            if (chargeCount > 2)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_OBJ_CHARGE_END());
                return false;
            }
            if (player.GetInventory().GetItemCountByItemId(164000289) == 0)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_OBJ_NOITEM());
                return false;
            }
            if (isVortexSpawned.CompareAndSet(false, true))
            {
                HandleVortexSpawn();
            }
            if (isUnderCharge.CompareAndSet(false, true))
            {
                HandleCharging(player);
            }
        }
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
        return true;
    }

    private void HandleCharging(Player player)
    {
        ChargeObserver observer = new ChargeObserver(this, player);
        player.GetObserveController().Attach(observer);
        PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), GetObjectId(), 20000, 1));
        PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.START_QUESTLOOT, 0, GetObjectId()), true);
        Shout(GetChargeMsg());
        player.GetController().AddTask(TaskId.ACTION_ITEM_NPC, ThreadPoolManager.GetInstance().Schedule(() =>
        {
            PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.END_QUESTLOOT, 0, GetObjectId()), true);
            PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), GetObjectId(), 20000, 2));
            player.GetObserveController().RemoveObserver(observer);
            if (player.GetInventory().DecreaseByItemId(164000289, 1))
            {
                HandleChargeComplete();
                chargeCount++;
            }
            isUnderCharge.Set(false);
        }, 20000L));
    }

    private sealed class ChargeObserver : ItemUseObserver
    {
        private readonly ShieldGeneratorAI ai;
        private readonly Player player;

        public ChargeObserver(ShieldGeneratorAI ai, Player player)
        {
            this.ai = ai;
            this.player = player;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(TaskId.ACTION_ITEM_NPC);
            PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.END_QUESTLOOT, 0, ai.GetObjectId()), true);
            PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), ai.GetObjectId(), 0, 2));
            player.GetObserveController().RemoveObserver(this);
            ai.isUnderCharge.Set(false);
        }
    }

    private void DeleteNpcs()
    {
        foreach (Npc s in charges)
        {
            s.GetController().Delete();
        }
    }

    protected override void HandleDespawned()
    {
        DeleteNpcs();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        DeleteNpcs();
        Shout(GetDestructionMsg());
        HandleVortexDespawn();
        base.HandleDied();
    }
}
