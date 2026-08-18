using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/abyssal_splinter/PazuzuAI (@author Luzien).</summary>
[AIName("pazuzu")]
public class PazuzuAI : AggressiveNpcAI
{
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);
    private ScheduledTask? task;

    public PazuzuAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome.CompareAndSet(true, false))
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 342219);
            StartTask();
        }
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        CancelTask();
        isHome.Set(true);
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTask();
        PacketSendUtility.BroadcastMessage(GetOwner(), 1500003);
    }

    /// <summary>
    /// Retail <c>IDAbRe_Core_NamedC</c>: the water worms carry <c>live_time</c> 71 and the branch that
    /// summons them re-arms its timer at 72 seconds.
    /// </summary>
    /// <remarks>
    /// <b>They never expired here</b>, and the "only if none are standing" guard below is what that cost:
    /// with permanent worms the guard never passes again, so the whole seventy-second cycle ran exactly
    /// once per fight and then did nothing. Retail has no such guard — it does not need one, because its
    /// worms die a second before the next batch is due.
    /// <para>
    /// With both of retail's numbers the guard becomes harmless and the rhythm matches: the batch dies at
    /// 71 seconds and the check at 72 finds an empty room. <b>Retail also rolls 30 percent and splits the
    /// branch across HP bands, neither of which this class models</b> — recorded rather than invented.
    /// </para>
    /// </remarks>
    private const int WormLife = 71;
    private const int WormCycleMillis = 72000;

    private void StartTask()
    {
        task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19145, 55, GetOwner());
            if (GetPosition().GetWorldMapInstance().GetNpc(281909) == null)
            {
                Npc worms = (Npc)SpawnFor(281909, 651.351990f, 326.425995f, 465.523987f, (sbyte)8, WormLife);
                SpawnFor(281909, 666.604980f, 314.497009f, 465.394012f, (sbyte)27, WormLife);
                SpawnFor(281909, 685.588989f, 342.955994f, 465.908997f, (sbyte)68, WormLife);
                SpawnFor(281909, 651.322021f, 346.554993f, 465.563995f, (sbyte)111, WormLife);
                SpawnFor(281909, 666.7373f, 314.2235f, 465.38953f, (sbyte)30, WormLife);

                SkillEngine.SkillEngine.GetInstance().GetSkill(worms, 19291, 55, GetOwner());
            }
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(0), System.TimeSpan.FromMilliseconds(WormCycleMillis));
    }

    private void CancelTask()
    {
        if (task != null && !task.IsCancelled)
        {
            task.Cancel(true);
        }
    }

    protected override void HandleDespawned()
    {
        CancelTask();
        base.HandleDespawned();
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.REWARD_LOOT:
            case AIQuestion.REWARD_AP:
                return false;
            default:
                return base.Ask(question);
        }
    }
}
