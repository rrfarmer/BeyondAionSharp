using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Ai.Pattern;

/// <summary>
/// Runs a translated retail AI pattern: battle timers, first-match-wins branches, and the flag vars
/// that turn a repeating threshold into a one-shot step.
/// </summary>
/// <remarks>
/// Retail bosses are not written as HP ladders. A boss arms a battle timer when it enters combat, and
/// each timer branch arms the next one, so a fight is a chain of timers whose links are chosen by the
/// boss's current health regime. Reproducing that per boss in hand-written C# means re-deriving the
/// same machinery every time; over half the retail adds our server never spawns — 427 of 805 — belong
/// to bosses of exactly this shape. This runs the structure once so each boss is a table.
/// <para>
/// What it deliberately does not do: decide anything. Skill indices, npc ids, coordinates and message
/// ids are resolved per boss in that boss's table, where the reasoning can be cited against
/// <c>docs/retail-ai-fidelity.md</c>. A boss whose indices cannot be resolved should not get a table
/// with guesses in it.
/// </para>
/// <para>
/// Battle timers only run in combat, matching retail, and everything is cancelled and reset on death,
/// despawn and reset — so a boss that resets replays its steps rather than starting mid-fight.
/// </para>
/// </remarks>
public abstract class PatternAi : AggressiveNpcAI
{
    /// <summary>Retail gives every NPC thirty battle-timer slots and thirty-two flag vars.</summary>
    private const int TimerSlots = 30;
    private const int FlagSlots = 32;

    private readonly ScheduledTask?[] timers = new ScheduledTask?[TimerSlots];
    private readonly bool[] flags = new bool[FlagSlots];
    private readonly Dictionary<int, List<Npc>> spawnGroups = new Dictionary<int, List<Npc>>();

    /// <summary>Guards the timer slots, the flags and the spawn groups against concurrent AI events.</summary>
    /// <remarks>
    /// Re-entrant on purpose: a branch's actions run while this is held and almost always re-arm the
    /// timer that fired them, which comes straight back through <see cref="ArmTimer"/>.
    /// </remarks>
    private readonly object gate = new object();

    protected PatternAi(Npc owner)
        : base(owner)
    {
    }

    /// <summary>This NPC's translated pattern. Build it once per class in a static field.</summary>
    protected abstract AiPattern Pattern { get; }

    /// <summary>Which timer slot is being serviced, or -1 outside a battle-timer event.</summary>
    public int FiredTimer { get; private set; } = -1;

    public int HpPercent => GetLifeStats().GetHpPercentage();

    public Creature? CurrentTarget => GetOwner().GetTarget() as Creature
        ?? GetAggroList().GetTarget(AggroTarget.MOST_HATED);

    // ---- the event surface -------------------------------------------------------------------

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (EnterCombat())
            Evaluate(Pattern.OnEnterAttack);
    }

    protected override void HandleBackHome()
    {
        Evaluate(Pattern.OnEnterIdle);
        ResetPattern();
        base.HandleBackHome();
    }

    protected override void HandleDied()
    {
        Evaluate(Pattern.OnDie);
        ResetPattern();
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        ResetPattern();
        base.HandleDespawned();
    }

    /// <summary>True on the call that takes this NPC from out-of-combat into combat.</summary>
    /// <remarks>
    /// <c>on_enter_attack_state</c> fires once per fight, and <c>HandleAttack</c> fires on every swing,
    /// so the transition has to be latched rather than inferred from the event.
    /// </remarks>
    private bool EnterCombat()
    {
        lock (gate)
        {
            if (inCombat)
                return false;
            inCombat = true;
            return true;
        }
    }

    private bool inCombat;

    /// <summary>Leaves the fight: cancels every timer, drops the flags, and forgets the spawns.</summary>
    /// <remarks>
    /// The spawns themselves are despawned by the pattern's own <c>on_die</c> / <c>on_enter_idle_state</c>
    /// branches, which run before this. Forgetting them here only stops a later reset from deleting
    /// NPCs that a fresh fight has since spawned.
    /// <para>
    /// A spawn's <c>live_time</c> is deliberately not cancelled: the lifetime belongs to the NPC that was
    /// spawned, not to whoever spawned it. Cancelling here would strand every add whose group no branch
    /// despawns, which is a worse leak than an extra pending deletion — and the deletion is a no-op once
    /// something else has removed the NPC.
    /// </para>
    /// </remarks>
    private void ResetPattern()
    {
        lock (gate)
        {
            inCombat = false;
            for (int i = 0; i < timers.Length; i++)
                CancelSlot(i);
            Array.Clear(flags);
            spawnGroups.Clear();
        }
    }

    // ---- branch evaluation -------------------------------------------------------------------

    /// <summary>Runs the first branch whose conditions all pass, in the order retail evaluates them.</summary>
    private void Evaluate(PatternBranch[] branches)
    {
        if (branches.Length == 0)
            return;

        lock (gate)
        {
            foreach (PatternBranch branch in branches)
            {
                // Short-circuiting is not an optimisation here: a test-and-set guard behind a failing
                // one must not consume its flag, or the step it protects is lost.
                bool matched = true;
                foreach (PatternCondition condition in branch.Conditions)
                {
                    if (!condition(this))
                    {
                        matched = false;
                        break;
                    }
                }

                if (!matched)
                    continue;

                foreach (PatternAction action in branch.Actions)
                    action(this);
                return;
            }
        }
    }

    // ---- battle timers -----------------------------------------------------------------------

    /// <summary>Arms one timer slot, replacing whatever was in it.</summary>
    public void ArmTimer(int index, int delayMillis)
    {
        if (index < 0 || index >= TimerSlots)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"battle timer slots are 0..{TimerSlots - 1}");

        lock (gate)
        {
            CancelSlot(index);
            timers[index] = ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                FireTimer(index);
                return ValueTask.CompletedTask;
            }, delayMillis);
        }
    }

    private void FireTimer(int index)
    {
        lock (gate)
        {
            timers[index] = null;
            if (IsDead() || !IsInState(AIState.FIGHT))
                return;

            FiredTimer = index;
            try
            {
                Evaluate(Pattern.OnBattleTimer);
            }
            finally
            {
                FiredTimer = -1;
            }
        }
    }

    private void CancelSlot(int index)
    {
        ScheduledTask? task = timers[index];
        if (task != null && !task.IsDone())
            task.Cancel(true);
        timers[index] = null;
    }

    // ---- flag vars ---------------------------------------------------------------------------

    /// <summary>Test-and-set: true only the first time, which is what makes a branch a one-shot step.</summary>
    public bool TestAndSetFlag(int flag)
    {
        lock (gate)
        {
            if (flags[flag])
                return false;
            flags[flag] = true;
            return true;
        }
    }

    /// <summary>Test-and-unset: true only while the flag is set, clearing it.</summary>
    public bool TestAndUnsetFlag(int flag)
    {
        lock (gate)
        {
            if (!flags[flag])
                return false;
            flags[flag] = false;
            return true;
        }
    }

    public virtual bool RollPercent(int percent) => Rnd.Chance() < percent;

    // ---- actions -----------------------------------------------------------------------------

    /// <summary>Queues one of this NPC's own skills, at the level its npc_skills entry gives it.</summary>
    public void CastSkill(int skillId, NpcSkillTargetAttribute target)
    {
        if (!IsDead())
            NpcSkillCasting.QueueAtDataLevel(GetOwner(), skillId, target);
    }

    public void SwitchTarget(AggroTarget which)
    {
        Creature? next = GetAggroList().GetTarget(which);
        if (next != null)
            GetOwner().SetTarget(next);
    }

    public void Say(int messageId, int delayMillis = 0)
    {
        if (!IsDead())
            PacketSendUtility.BroadcastMessage(GetOwner(), messageId, delayMillis);
    }

    public void DespawnSelf() => AIActions.DeleteOwner(this);

    /// <summary>Spawns one NPC per listed coordinate.</summary>
    public void SpawnAt(int npcId, int spawnId, int liveSeconds, params SpawnSpot[] spots)
    {
        foreach (SpawnSpot spot in spots)
            Track(spawnId, liveSeconds, Spawn(npcId, spot.X, spot.Y, spot.Z, spot.Heading));
    }

    /// <summary>Spawns around this NPC, scattered within <paramref name="range"/> metres.</summary>
    public void SpawnNear(int npcId, int spawnId, int count, float range, int liveSeconds)
        => SpawnAround(GetPosition(), npcId, spawnId, count, range, liveSeconds);

    /// <summary>Spawns around whoever this NPC is facing, which is where <c>spawn_on_target</c> puts them.</summary>
    public void SpawnOnTarget(int npcId, int spawnId, int count, float range, int liveSeconds)
    {
        Creature? target = CurrentTarget;
        if (target != null)
            SpawnAround(target.GetPosition(), npcId, spawnId, count, range, liveSeconds);
    }

    private void SpawnAround(WorldPosition at, int npcId, int spawnId, int count, float range, int liveSeconds)
    {
        for (int i = 0; i < count; i++)
        {
            float x = at.GetX();
            float y = at.GetY();
            if (range > 0f)
            {
                double angle = Rnd.NextFloat(360f) * Math.PI / 180.0;
                float distance = Rnd.NextFloat(range);
                x += (float)(Math.Cos(angle) * distance);
                y += (float)(Math.Sin(angle) * distance);
            }

            Track(spawnId, liveSeconds, Spawn(npcId, x, y, at.GetZ(), (sbyte)at.GetHeading()));
        }
    }

    /// <summary>Files a spawn under its spawn id so a later <c>despawn</c> can find it.</summary>
    private void Track(int spawnId, int liveSeconds, VisibleObject? spawned)
    {
        if (spawned is not Npc npc)
            return;

        lock (gate)
        {
            if (!spawnGroups.TryGetValue(spawnId, out List<Npc>? group))
                spawnGroups[spawnId] = group = new List<Npc>();
            group.Add(npc);

            if (liveSeconds <= 0)
                return;

            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                Remove(spawnId, npc);
                return ValueTask.CompletedTask;
            }, liveSeconds * 1000L);
        }
    }

    /// <summary>Despawns everything filed under one spawn id.</summary>
    public void DespawnGroup(int spawnId)
    {
        List<Npc> group;
        lock (gate)
        {
            if (!spawnGroups.TryGetValue(spawnId, out List<Npc>? tracked))
                return;
            group = new List<Npc>(tracked);
            tracked.Clear();
        }

        foreach (Npc npc in group)
            Delete(npc);
    }

    private void Remove(int spawnId, Npc npc)
    {
        lock (gate)
        {
            if (spawnGroups.TryGetValue(spawnId, out List<Npc>? group))
                group.Remove(npc);
        }

        Delete(npc);
    }

    private static void Delete(Npc npc)
    {
        if (npc.IsSpawned())
            npc.GetController().DeleteIfAliveOrCancelRespawn();
    }

    /// <summary>Everything currently alive under one spawn id, for tables that need to look.</summary>
    public IReadOnlyList<Npc> Spawned(int spawnId)
    {
        lock (gate)
        {
            return spawnGroups.TryGetValue(spawnId, out List<Npc>? group)
                ? new List<Npc>(group)
                : (IReadOnlyList<Npc>)Array.Empty<Npc>();
        }
    }
}
