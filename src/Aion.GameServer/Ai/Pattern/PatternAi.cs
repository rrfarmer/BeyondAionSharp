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
public abstract class PatternAi : AggressiveNpcAI, INpcMessageListener
{
    /// <summary>Retail gives every NPC thirty battle-timer slots and thirty-two flag vars.</summary>
    private const int TimerSlots = 30;
    private const int FlagSlots = 32;

    private readonly ScheduledTask?[] timers = new ScheduledTask?[TimerSlots];
    private readonly bool[] flags = new bool[FlagSlots];

    /// <summary>Retail names four: <c>INTVARI_FIRST</c> through <c>INTVARI_FOURTH</c>.</summary>
    private const int CounterSlots = 4;

    private readonly int[] counters = new int[CounterSlots];
    private readonly Dictionary<int, List<Npc>> spawnGroups = new Dictionary<int, List<Npc>>();
    private ScheduledTask? idleTimer;

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

    /// <summary>The message being handled, or -1 outside an <c>on_message</c> event.</summary>
    public int CurrentMessage { get; private set; } = -1;

    /// <summary>The object that message carried — usually the player the sender is complaining about.</summary>
    public VisibleObject? MessageParam { get; private set; }

    public int HpPercent => GetLifeStats().GetHpPercentage();

    public Creature? CurrentTarget => GetOwner().GetTarget() as Creature
        ?? GetAggroList().GetTarget(AggroTarget.MOST_HATED);

    // ---- the event surface -------------------------------------------------------------------

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        Evaluate(Pattern.OnWakeUp);
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (EnterCombat())
            Evaluate(Pattern.OnEnterAttack);

        // on_attacked runs on every hit. A branch that should only fire once carries its own flag
        // var, which is how retail writes them -- gating on the event instead would be a different
        // mechanic.
        Evaluate(Pattern.OnAttacked);
    }

    /// <summary>
    /// Retail splits this into two events — <c>on_leave_attack_state</c> as the fight ends and
    /// <c>on_enter_idle_state</c> once the NPC is home — and fires them in that order. Our engine has
    /// one moment for both, so both run here, leave-attack first.
    /// </summary>
    /// <remarks>
    /// Patterns put different work in each: Golden Tatar clears its adds on leaving the fight and only
    /// re-opens its door on going idle, while Derakanak and Asaratu do everything in the idle handler.
    /// Running only one of the two silently dropped whichever half a pattern happened to use — which is
    /// what happened before this was wired: <c>OnLeaveAttack</c> was declared and never evaluated.
    /// </remarks>
    protected override void HandleBackHome()
    {
        Evaluate(Pattern.OnLeaveAttack);
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

    /// <summary>
    /// Retail's <c>is_npc_state(NPCI_SELF, NPC_STATE_ATTACK)</c> against <c>NPC_STATE_IDLE</c>.
    /// </summary>
    /// <remarks>
    /// The same latch <c>on_enter_attack_state</c> rides, exposed because retail branches on it
    /// directly 968 times across the 5.8 files — an NPC that is already fighting answers a call
    /// differently from one standing idle.
    /// </remarks>
    public bool InCombat
    {
        get
        {
            lock (gate)
                return inCombat;
        }
    }

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
            if (idleTimer != null && !idleTimer.IsDone())
                idleTimer.Cancel(true);
            idleTimer = null;
            Array.Clear(flags);
            Array.Clear(counters);
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

    // ---- messages --------------------------------------------------------------------------

    /// <summary>Receives a <c>broadcast_message</c> from another NPC of this encounter.</summary>
    /// <remarks>
    /// Unlike a battle timer this is not gated on being in combat: retail uses messages to start
    /// fights as well as to coordinate them, and a listener that ignored them out of combat could
    /// never be pulled by one.
    /// </remarks>
    public void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
    {
        if (IsDead() || Pattern.OnMessage.Length == 0)
            return;

        lock (gate)
        {
            CurrentMessage = messageType;
            MessageParam = param;
            try
            {
                Evaluate(Pattern.OnMessage);
            }
            finally
            {
                CurrentMessage = -1;
                MessageParam = null;
            }
        }
    }

    /// <summary>Broadcasts to the rest of the encounter, optionally naming who this NPC is fighting.</summary>
    public void Broadcast(int messageType, float range, bool aboutTarget)
        => NpcMessageBus.Broadcast(GetOwner(), messageType, aboutTarget ? CurrentTarget : null, range);

    /// <summary>Puts hate on whoever a message named and turns to face them.</summary>
    public void HateMessageTarget(int hate)
    {
        if (MessageParam is not Creature target || target.IsDead())
            return;

        GetAggroList().AddHate(target, hate);
        GetOwner().SetTarget(target);
    }

    // ---- the idle timer ----------------------------------------------------------------------

    /// <summary>Arms the single idle slot, replacing whatever was in it.</summary>
    /// <remarks>
    /// Deliberately not gated on combat, unlike <see cref="FireTimer"/>. Its whole purpose is the
    /// business around a fight rather than in it — a controller retiring once it has spawned its
    /// wave, an orb calling out on a heartbeat — and half its uses are on NPCs that never fight at
    /// all. A zero delay is retail's way of saying "next tick", so it is scheduled rather than run
    /// inline; running it inline would evaluate a branch from inside the event that set it.
    /// </remarks>
    public void SetIdleTimer(int delayMillis)
    {
        lock (gate)
        {
            if (idleTimer != null && !idleTimer.IsDone())
                idleTimer.Cancel(true);

            idleTimer = ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                FireIdleTimer();
                return ValueTask.CompletedTask;
            }, delayMillis);
        }
    }

    private void FireIdleTimer()
    {
        lock (gate)
        {
            idleTimer = null;
            if (!IsDead())
                Evaluate(Pattern.OnIdleTimer);
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

    // ---- counters ----------------------------------------------------------------------------

    /// <summary>Test-and-set while below: the "are my summons all dead" branch, and its bookkeeping.</summary>
    public bool TestAndSetCounterIfBelow(int counter, int comparand, int setTo)
    {
        lock (gate)
        {
            if (counters[counter] >= comparand)
                return false;
            counters[counter] = setTo;
            return true;
        }
    }

    /// <summary>Test-and-set while above: the mirror, for the branch that answers "some are alive".</summary>
    public bool TestAndSetCounterIfAbove(int counter, int comparand, int setTo)
    {
        lock (gate)
        {
            if (counters[counter] <= comparand)
                return false;
            counters[counter] = setTo;
            return true;
        }
    }

    /// <summary>Takes one off a counter, clamped, and always passes. See <c>When.Decrement</c>.</summary>
    public bool DecrementCounter(int counter, int low, int high)
    {
        lock (gate)
        {
            counters[counter] = Math.Clamp(counters[counter] - 1, low, high);
            return true;
        }
    }

    /// <summary>The counters, for tests to read. Not part of the pattern vocabulary.</summary>
    public int Counter(int counter)
    {
        lock (gate)
            return counters[counter];
    }

    public virtual bool RollPercent(int percent) => Rnd.Chance() < percent;

    // ---- actions -----------------------------------------------------------------------------

    /// <summary>Queues one of this NPC's own skills, at the level its npc_skills entry gives it.</summary>
    public void CastSkill(int skillId, NpcSkillTargetAttribute target)
    {
        if (!IsDead())
            NpcSkillCasting.QueueAtDataLevel(GetOwner(), skillId, target);
    }

    /// <summary>
    /// Casts one of this NPC's own skills on itself now, bypassing the queue.
    /// </summary>
    /// <remarks>
    /// For NPCs that never fight. The queue is drained by the attack loop and only while the NPC has a
    /// target it hates, so a marker — a flame patch, a summon spot, a trap that appears, goes off and
    /// leaves — queues its one cast and never fires it. The summon spots that shipped with Tahabata's
    /// rebuild sat on an unfired 18222 until this existed.
    /// <para>
    /// <b>Chosen by the table rather than inferred.</b> Making <see cref="CastSkill"/> pick the
    /// immediate path whenever the NPC was out of combat looked tidier and was wrong: bosses buff
    /// themselves from <c>on_wake_up</c> too, and switching those from queued to immediate changed the
    /// behaviour of four fights that had nothing to do with markers. Retail draws no such distinction —
    /// <c>use_skill</c> is <c>use_skill</c> — so this is a runtime accommodation, and a table asks for
    /// it explicitly or does not get it.
    /// </para>
    /// </remarks>
    public void CastSkillNow(int skillId)
    {
        if (!IsDead())
            NpcSkillCasting.UseOnSelfNow(GetOwner(), skillId);
    }

    /// <summary>
    /// Casts this NPC's one and only skill on itself — retail's <c>SKILLI_INDEX_0</c> where the list
    /// holds nothing else.
    /// </summary>
    /// <remarks>
    /// The single-entry check is the whole point. Resolving a skill index by its position in our
    /// npc_skills is unreliable and has been proven wrong more than once, so this refuses rather than
    /// guesses: point it at an NPC with two skills and it does nothing. That keeps a shared trap class
    /// usable across the NPCs where index 0 is unambiguous without letting it quietly pick the wrong
    /// skill on the ones where it is not.
    /// </remarks>
    public void CastOnlySkillOnSelf()
    {
        Aion.GameServer.Model.Skill.NpcSkillList? skills = GetOwner().GetSkillList();
        if (skills == null || skills.GetNpcSkills().Count != 1)
            return;

        CastSkillNow(skills.GetNpcSkills()[0].GetSkillId());
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

    /// <summary>
    /// <c>spawn_on_target target_obj=OBJI_SELF</c> with <c>attack_target_after_spawn</c>: an NPC that
    /// appears at this one's feet and immediately attacks <em>it</em>.
    /// </summary>
    /// <remarks>
    /// Retail's way of making something fight without a player having touched it. The spawner is the
    /// victim, so the flag starts <em>the spawner's</em> fight — its <c>on_enter_attack_state</c> runs and
    /// its battle timers begin — which is the whole point wherever it is used: a gate that feeds a room
    /// on a timer needs to be in combat, and nobody is going to attack a gate first.
    /// <para>
    /// <paramref name="hate"/> is retail's <c>hatepoints_to_add</c>. The values are absurd on purpose
    /// (100,000 for the Abyssal Reliquary gates, up to 99,999,999 elsewhere): they are meant to outrank
    /// anything a raid can build, so the summon stays locked on its spawner rather than peeling.
    /// </para>
    /// </remarks>
    public void SpawnAsMyEnemy(int npcId, int spawnId, int liveSeconds, int hate)
    {
        WorldPosition here = GetPosition();
        VisibleObject? spawned = Spawn(npcId, here.GetX(), here.GetY(), here.GetZ(), (sbyte)here.GetHeading());
        Track(spawnId, liveSeconds, spawned);
        if (spawned is not Npc summon)
            return;

        Npc victim = GetOwner();
        // Deferred by a tick, and it has to be. Every use of this op is on `on_wake_up`, which runs from
        // inside the owner's own BringIntoWorld -- so a state flip made here is overwritten by the rest
        // of that spawn path, which leaves the NPC IDLE. Scheduling it is the same answer SetIdleTimer
        // gives to a zero delay: next tick, not inline.
        ProvokeNextTick(summon, victim, hate);
    }

    /// <summary>
    /// Puts a fresh summon into a fight with whoever it was dropped on, as
    /// <c>attack_target_after_spawn</c> does, one tick from now.
    /// </summary>
    /// <remarks>
    /// <b>Deferred, and it has to be for the <c>OBJI_SELF</c> form.</b> Those all sit on
    /// <c>on_wake_up</c>, which runs from inside the owner's own <c>BringIntoWorld</c> — a state flip
    /// made there is overwritten by the rest of that spawn path and the NPC ends up IDLE. Scheduling is
    /// the same answer <see cref="SetIdleTimer"/> gives to a zero delay: next tick, not inline. The
    /// other forms do not need it, and share it anyway so one op has one behaviour.
    /// </remarks>
    private static void ProvokeNextTick(Npc summon, Creature victim, int hate)
        => ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            Provoke(summon, victim, hate);
            return ValueTask.CompletedTask;
        }, 0L);

    /// <summary>Starts the fight <c>attack_target_after_spawn</c> describes.</summary>
    /// <remarks>
    /// The summon's side is unconditional. Retail's engine makes it attack, and here it may be a passive
    /// <c>general</c> NPC that never swings on its own, so waiting for it to act would leave the pair
    /// standing next to each other. What the flag means is that these two are now fighting.
    /// <para>
    /// The victim's side runs only for an NPC victim, because only an NPC has an AI to put into the
    /// fight — and for the <c>OBJI_SELF</c> form that half <em>is</em> the point: the spawner's own
    /// <c>on_enter_attack_state</c> is what the summon exists to trigger. A player victim needs nothing:
    /// being attacked is already handled everywhere else.
    /// </para>
    /// <para>
    /// Order matters within each side, and it is the order the harness uses to start a fight by hand: the
    /// state flip has to land before the hate, or <c>AddHate</c>'s own aggro handling flips it first and
    /// the Attack event no longer takes the path that runs <c>on_enter_attack_state</c>.
    /// </para>
    /// </remarks>
    private static void Provoke(Npc summon, Creature victim, int hate)
    {
        if (summon.IsDead() || victim.IsDead())
            return;

        summon.GetKnownList().Add(victim);
        victim.GetKnownList().Add(summon);

        summon.GetAi().SetStateIfNot(AIState.FIGHT);
        summon.SetTarget(victim);
        summon.GetAggroList().AddHate(victim, hate);

        if (victim is not Npc npcVictim)
            return;

        npcVictim.GetAi().SetStateIfNot(AIState.FIGHT);
        npcVictim.SetTarget(summon);
        npcVictim.GetAggroList().AddHate(summon, hate);
        npcVictim.GetAi().OnCreatureEvent(AiEventType.Attack, summon);
    }

    /// <summary>Spawns around whoever this NPC is facing, which is where <c>spawn_on_target</c> puts them.</summary>
    public void SpawnOnTarget(int npcId, int spawnId, int count, float range, int liveSeconds)
        => SpawnOnTarget(npcId, spawnId, count, range, liveSeconds, attackHate: 0);

    /// <summary>
    /// <c>spawn_on_target</c>, optionally with <c>attack_target_after_spawn</c>: the adds land on
    /// whoever this NPC is fighting and, when <paramref name="attackHate"/> is non-zero, arrive
    /// already fighting that same player.
    /// </summary>
    /// <remarks>
    /// The difference is not cosmetic. A guard's ranger trap that engages the player it lands on is a
    /// thing you have to deal with; the same trap standing inert is a thing you walk away from.
    /// </remarks>
    public void SpawnOnTarget(int npcId, int spawnId, int count, float range, int liveSeconds, int attackHate)
    {
        Creature? target = CurrentTarget;
        if (target == null)
            return;

        if (attackHate <= 0)
        {
            SpawnAround(target.GetPosition(), npcId, spawnId, count, range, liveSeconds);
            return;
        }

        var placed = new List<Npc>();
        SpawnAroundInto(placed, target.GetPosition(), npcId, spawnId, count, range, liveSeconds);
        foreach (Npc summon in placed)
            ProvokeNextTick(summon, target, attackHate);
    }

    /// <summary>Puts one add on each valid target, which is what makes a raid-wide drop raid-wide.</summary>
    /// <remarks>
    /// <c>valid_distance</c> is a filter on who counts, not a spawn radius: a target further away than it
    /// simply gets nothing. Falling back to the current target when the list is empty would turn a
    /// raid-wide mechanic into a tank-only one, so an empty list spawns nothing.
    /// </remarks>
    /// <summary>
    /// Retail <c>spawn_on_multi_target</c>: one spawn per target, most-hated first, capped.
    /// </summary>
    /// <remarks>
    /// <paramref name="maxTargets"/> is retail's <c>total_set_to_spawn</c> and <paramref name="order"/>
    /// its <c>order_in_attacker_list</c>. Neither is optional. Every <c>spawn_on_multi_target</c> in
    /// the retail files carries both, and neither has a safe default: uncapped, a full alliance takes
    /// one hazard each, so Tiamat's Fissurefang dropped twenty-four earthquakes where retail drops
    /// three — and which players the cap keeps is the mechanic. A paralysis eye on two random players
    /// is a different fight from one on the two tanks.
    /// </remarks>
    public void SpawnOnEachTarget(int npcId, int spawnId, float validDistance, float range,
        int liveSeconds, int maxTargets, MultiTargetOrder order, int attackHate = 0)
    {
        AggroList aggro = GetAggroList();
        IEnumerable<Creature> valid = aggro.StreamValidTargets(validDistance);
        IEnumerable<Creature> ordered = order switch
        {
            MultiTargetOrder.Descending => valid.OrderByDescending(t => aggro.GetHate(t)),
            MultiTargetOrder.Ascending => valid.OrderBy(t => aggro.GetHate(t)),
            _ => Shuffle(valid),
        };

        foreach (Creature target in ordered.Take(maxTargets).ToList())
        {
            if (attackHate <= 0)
            {
                SpawnAround(target.GetPosition(), npcId, spawnId, 1, range, liveSeconds);
                continue;
            }

            // Each add is paired with the player it was placed on, not with the raid at large: this is
            // the op that puts one hazard on each of several people, and each hazard is theirs.
            var placed = new List<Npc>();
            SpawnAroundInto(placed, target.GetPosition(), npcId, spawnId, 1, range, liveSeconds);
            foreach (Npc summon in placed)
                ProvokeNextTick(summon, target, attackHate);
        }
    }

    /// <summary>Overridable so a test can make <c>ORDERI_RANDOM</c> deterministic.</summary>
    protected virtual IEnumerable<Creature> Shuffle(IEnumerable<Creature> targets)
    {
        List<Creature> pool = targets.ToList();
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Rnd.Get(0, i);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        return pool;
    }

    /// <summary>Puts an add on one attacker chosen the way the pattern names them.</summary>
    public void SpawnOnAttacker(AggroTarget which, int npcId, int spawnId, float range, int liveSeconds)
    {
        Creature? target = GetAggroList().GetTarget(which);
        if (target != null)
            SpawnAround(target.GetPosition(), npcId, spawnId, 1, range, liveSeconds);
    }

    /// <summary>
    /// Retail <c>SPAWN_LOCATION_RELATIVE</c>: a fixed offset from where this NPC stands.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="SpawnNear"/>, which scatters inside a radius. Patterns use this to put
    /// something at a specific bearing — four cardinal points around a boss, a wall across one side of
    /// a room — so the offset has to be exact rather than random.
    /// <para>
    /// Taken as a world-axis offset from the NPC's position. Whether retail rotates it by the NPC's
    /// heading is not settled here; for the four-way symmetric placements this was written for, the
    /// distinction cannot be observed, and no ported pattern yet depends on an asymmetric one.
    /// </para>
    /// </remarks>
    public void SpawnOffset(int npcId, int spawnId, float dx, float dy, int liveSeconds)
    {
        WorldPosition at = GetPosition();
        Track(spawnId, liveSeconds,
            Spawn(npcId, at.GetX() + dx, at.GetY() + dy, at.GetZ(), (sbyte)at.GetHeading()));
    }

    private void SpawnAround(WorldPosition at, int npcId, int spawnId, int count, float range, int liveSeconds)
        => SpawnAroundInto(null, at, npcId, spawnId, count, range, liveSeconds);

    /// <summary>The same, collecting what was placed for callers that have to do something with it.</summary>
    private void SpawnAroundInto(List<Npc>? placed, WorldPosition at, int npcId, int spawnId,
        int count, float range, int liveSeconds)
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

            VisibleObject? spawned = Spawn(npcId, x, y, at.GetZ(), (sbyte)at.GetHeading());
            Track(spawnId, liveSeconds, spawned);
            if (placed != null && spawned is Npc npc)
                placed.Add(npc);
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
