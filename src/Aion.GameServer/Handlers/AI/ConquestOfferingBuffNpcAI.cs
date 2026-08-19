using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The conquest offering buff npcs (856175-856178 and their siblings). Retail pattern
/// <c>F4_Rotation_Buff_NPC</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/ConquestOfferingBuffNpcAI (@author Yeats). Retail-sourced correction below; see
/// docs/retail-ai-fidelity.md. Found by <c>audit_timer_drift.py</c>.
/// <para>
/// <b>It stood sixty-five seconds and retail gives it sixty.</b> Every rung of its <c>on_wake_up</c>
/// ladder sets an idle timer of <b>60000</b>, and <c>on_idle_timer</c> is a bare <c>despawn_self</c>.
/// Five seconds is a small thing on its own; it is here because it is the same five seconds as
/// <see cref="ConquestOfferingPortalAI"/> was carrying against a hundred and eighty, and one number
/// appears to have been used for both.
/// </para>
/// <para>
/// <b>Not translated:</b> the ladder itself. Retail picks one of several wake-up shouts by
/// <c>test_probability percent=30</c>, each setting a different pair of flag vars — so which greeting
/// it gives decides which of its buffs are available afterwards. This port sends one message and offers
/// the same thing every time.
/// </para>
/// </remarks>
[AIName("conquest_offering_buff_npc")]
public class ConquestOfferingBuffNpcAI : ActionItemNpcAI
{
    /// <summary>Retail's <c>set_idle_timer</c>, the same on every rung of its wake-up ladder.</summary>
    public const long BuffNpcLifeMillis = 60_000L;

    private readonly AtomicBoolean used = new AtomicBoolean(false);
    private ScheduledTask despawnTask;

    public ConquestOfferingBuffNpcAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        SendWakeUpMsg();
        despawnTask = ThreadPoolManager.GetInstance().Schedule(ct => { GetOwner().GetController().Delete(); return System.Threading.Tasks.ValueTask.CompletedTask; }, BuffNpcLifeMillis);
    }

    protected override void HandleUseItemFinish(Player player)
    {
        if (used.CompareAndSet(false, true))
        {
            SendTalkedMsg();
            int skillId = 21924 + Rnd.Get(0, 3);
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), skillId, 1, player).UseSkill();
            GetOwner().GetController().Delete();
        }
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTask();
    }

    protected override void HandleDespawned()
    {
        CancelTask();
        base.HandleDespawned();
    }

    private void CancelTask()
    {
        if (despawnTask != null && !despawnTask.IsDone())
            despawnTask.Cancel(true);
    }

    private void SendWakeUpMsg()
    {
        int msg = (1501279 + (Rnd.Get(0, 2) * 2));
        PacketSendUtility.BroadcastMessage(GetOwner(), msg, 1500);
    }

    private void SendTalkedMsg()
    {
        int msg = (1501280 + (Rnd.Get(0, 2) * 2));
        PacketSendUtility.BroadcastMessage(GetOwner(), msg);
    }
}
