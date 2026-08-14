using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Macunbello's soul reapers, Beshmundir Temple. Retail pattern IDCT_SumLich (281698) and its
/// hard-mode twin (281775).
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Every 12 seconds a reaper yanks a random
/// player onto itself, curses them, and reports that player to Macunbello, who answers by
/// devouring them. Neither of our servers spawned these NPCs at all, so the combo did not
/// exist; the reapers' only skill sat unused in the data.
/// </remarks>
[AIName("macunbello_soul_reaper")]
public class MacunbelloSoulReaperAI : AggressiveNpcAI
{
    /// <summary>Designer message id from the retail pattern, scoped to this encounter.</summary>
    public const int CursedMessage = 6980;

    private const int CurseOfSouls = 19050;

    /// <summary>Retail switch_target_by_attacker_indicator: 10% of top hate, plus a flat 10000.</summary>
    private const int HatePercentToAdd = 10;
    private const int HatePointsToAdd = 10000;

    /// <summary>The first curse lands 5s after the reaper engages, then every 12s.</summary>
    private const long FirstCurseDelay = 5000L;
    private const long CurseInterval = 12000L;

    /// <summary>Retail broadcasts the first report 50m and later ones 40m.</summary>
    private const float FirstReportRange = 50f;
    private const float ReportRange = 40f;

    private ScheduledTask? curseTask;
    private bool cursedOnce;

    public MacunbelloSoulReaperAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (curseTask != null)
            return;
        curseTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(
            _ => { OnCurseTick(); return ValueTask.CompletedTask; },
            System.TimeSpan.FromMilliseconds(FirstCurseDelay),
            System.TimeSpan.FromMilliseconds(CurseInterval));
    }

    private void OnCurseTick()
    {
        if (IsDead() || !IsInState(AIState.FIGHT))
            return;

        Creature? victim = GetAggroList().GetTarget(AggroTarget.RANDOM);
        if (victim == null || victim.IsDead())
            return;

        // Yank them onto us the way retail's attacker-indicator target switch does.
        GetAggroList().AddHate(victim, HatePointsToAdd);
        AIActions.TargetCreature(this, victim);
        NpcSkillCasting.QueueAtDataLevel(GetOwner(), CurseOfSouls, NpcSkillTargetAttribute.MOST_HATED);

        NpcMessageBus.Broadcast(GetOwner(), CursedMessage, victim,
            cursedOnce ? ReportRange : FirstReportRange);
        cursedOnce = true;
    }

    protected override void HandleBackHome()
    {
        CancelCurse();
        base.HandleBackHome();
        // Retail despawns the reaper when it drops out of combat rather than letting it idle.
        GetOwner().GetController().Delete();
    }

    protected override void HandleDied()
    {
        CancelCurse();
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        CancelCurse();
        base.HandleDespawned();
    }

    private void CancelCurse()
    {
        if (curseTask != null && !curseTask.IsDone())
            curseTask.Cancel(true);
        curseTask = null;
    }
}
