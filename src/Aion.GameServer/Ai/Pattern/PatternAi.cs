using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Model.Templates.Walker;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Spawns = Aion.GameServer.World.Spawns;

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
    private readonly long[] timerDue = new long[TimerSlots];

    /// <summary>How many times each slot has been armed and has fired. **Diagnostics only.**</summary>
    /// <remarks>
    /// Two encounters could not be pinned for want of this: Kingspin's accelerator windows, which open
    /// once per fight in retail and once per cry without their guard, and Masto's band cadence, whose
    /// only action is a random target switch. <b>Both are questions about a timer rather than about what
    /// the timer did</b>, so neither the roll seam nor the target seam reaches them.
    /// </remarks>
    private readonly int[] timerArms = new int[TimerSlots];

    private readonly int[] timerFires = new int[TimerSlots];

    /// <summary>How many times a slot has been armed. For tests.</summary>
    public int TimerArmCount(int index)
    {
        lock (gate)
            return timerArms[index];
    }

    /// <summary>How many times a slot has fired. For tests.</summary>
    public int TimerFireCount(int index)
    {
        lock (gate)
            return timerFires[index];
    }

    private int immediateCasts;

    private int spawnsMade;

    /// <summary>
    /// How many npcs this one has placed. For tests.
    /// </summary>
    /// <remarks>
    /// The spawn-side twin of <see cref="ImmediateCastCount"/>, and needed for the same reason. What a
    /// spawner places may be a hazard that casts and removes itself within the tick, so counting the
    /// ground afterwards answers a question about the <i>add</i> rather than about the spawner. This
    /// asks whether the spawner did its job, which is what a wave or beacon pin is actually about.
    /// </remarks>
    public int SpawnCount
    {
        get
        {
            lock (gate)
                return spawnsMade;
        }
    }

    /// <summary>
    /// How many skills this npc has cast down the immediate path. For tests.
    /// </summary>
    /// <remarks>
    /// <b>The only way to see a hazard work.</b> A hazard casts and despawns in the same rung, and the
    /// harness deliberately leaves out the skill engine's execution side, so nothing downstream of the
    /// cast is observable: no effect lands, no damage is dealt, and the npc is gone before the next
    /// sample. <c>DrainQueuedSkills</c> cannot see it either, because the immediate path is precisely
    /// the one that does not use the queue.
    /// <para>
    /// Counted rather than inferred, and in the same spirit as the timer counters above: a question
    /// about whether the runtime did the thing, when the thing itself leaves no trace here.
    /// </para>
    /// </remarks>
    public int ImmediateCastCount
    {
        get
        {
            lock (gate)
                return immediateCasts;
        }
    }

    /// <summary>Retail names four: <c>INTVARI_FIRST</c> through <c>INTVARI_FOURTH</c>.</summary>
    private const int CounterSlots = 4;

    private readonly int[] counters = new int[CounterSlots];
    private readonly Dictionary<int, List<Npc>> spawnGroups = new Dictionary<int, List<Npc>>();

    /// <summary>Adds retail marks <c>despawn_at_attack_state</c>: they belong to the fight, not the world.</summary>
    /// <remarks>
    /// 12,614 of retail's 16,343 spawns carry the flag and <b>7,690 of those are permanent</b>
    /// (<c>live_time=0</c>), so ignoring it is not a detail -- it is every one of those adds staying on
    /// the ground forever once the fight is over. A boss that summons one a second and is fought for ten
    /// minutes leaves six hundred behind.
    /// </remarks>
    private readonly List<Npc> transientSpawns = new List<Npc>();

    /// <summary>True while a handler that ends the encounter is running.</summary>
    /// <remarks>
    /// <c>on_die</c> evaluates immediately before the reset that sweeps fight-scoped adds, so a bequest
    /// placed there was being created and deleted in the same breath -- the death-spawn table's first
    /// two pins failed on exactly that. Retail's <c>despawn_at_attack_state</c> means the add lives as
    /// long as the fight; something the npc leaves <b>because</b> the fight ended is not that, whatever
    /// the flag on its spawn says, because there is no longer a fight for it to belong to.
    /// </remarks>
    private bool ending;

    /// <summary>
    /// Everything the branch currently running has spawned, so a <c>broadcast_message</c> later in the
    /// same branch does not reach it.
    /// </summary>
    /// <remarks>
    /// Retail writes spawn-then-broadcast constantly, and where the spawn is itself a listener the
    /// message is plainly not meant for it — RM-56c lays traps and then tells traps to leave. Our spawn
    /// path makes a summon visible to its spawner immediately, so without this the boss deletes the
    /// arrangement it has just laid. Cleared when the branch finishes, so nothing outside one branch is
    /// affected. See docs/retail-ai-fidelity.md.
    /// </remarks>
    private readonly List<Npc> spawnedThisBranch = new List<Npc>();
    private ScheduledTask? idleTimer;

    /// <summary>The flee in progress, or null. See <see cref="Flee"/>.</summary>
    private ScheduledTask? fleeing;

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

    /// <summary>
    /// Who sent the message being handled, or null outside an <c>on_message</c> event.
    /// </summary>
    /// <remarks>
    /// Retail has no condition for this — where two senders share a message number it discriminates
    /// with <c>is_race</c>. <b>This comment used to say that was unreadable from the dump.</b> It is
    /// not: all 2,879 <c>is_race</c> conditions carry a <c>race_type</c>, and the summariser was
    /// dropping the value. The akaimum still discriminates by npc id, which is exact and needs no
    /// race table, but the reason given here was wrong. See <see cref="AiPattern.When.SeenRace"/> and
    /// docs/retail-ai-fidelity.md.
    /// </remarks>
    public Npc? MessageSender { get; private set; }

    public int HpPercent => GetLifeStats().GetHpPercentage();

    /// <summary>
    /// <c>OBJI_KILLER</c>: the player retail would credit with the kill.
    /// </summary>
    /// <remarks>
    /// Read as most-damage rather than most-hated, because that is what the rest of the server already
    /// treats as the killer — it is the same lookup loot ownership uses. A death branch that spawns on
    /// its killer is a real family of mechanics (the Abyss undead are twenty-one npcs of it), and
    /// nothing in the pattern runtime could say who that was. See docs/retail-ai-fidelity.md.
    /// </remarks>
    public Player? Killer => GetAggroList().GetMostPlayerDamage();

    /// <summary>Retail's <c>on_killed_by_npc</c>: whatever killed this, it was not a player.</summary>
    /// <remarks>
    /// Read from the same list as <see cref="Killer"/> and by the same rule -- whoever dealt the most
    /// damage -- so the two are exclusive by construction rather than by two implementations agreeing.
    /// <para>
    /// <b>Not the same as "no player killed it".</b> An npc that expires, or that nothing ever hit, has
    /// no top damager at all, and retail's handler is about an npc having done it. Reading the absence
    /// of a player as the presence of an npc would fire these branches on every quiet despawn.
    /// </para>
    /// </remarks>
    public Npc? NpcKiller
        => GetAggroList().GetFinalDamageList().GetMostDamage()?.GetAttacker() as Npc;

    /// <summary>Where a flee is heading, or null when this NPC is not running from anything.</summary>
    /// <remarks>
    /// Exposed for pinning. Our harness has no movement, so the only thing a test can read about a
    /// flee is where it was aimed and when it ended — which is also the whole of what the pattern
    /// specifies, since retail gives a duration and not a distance.
    /// </remarks>
    public (float X, float Y)? FleeingTo { get; private set; }

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
        {
            // Only on the transition. Written on every swing this would simply become LastAttacker
            // under another name, and the casts meant for whoever opened the fight would follow the
            // tank around instead. A pin caught exactly that.
            EventTarget = creature;
            Evaluate(Pattern.OnEnterAttack);
        }

        // on_attacked runs on every hit. A branch that should only fire once carries its own flag
        // var, which is how retail writes them -- gating on the event instead would be a different
        // mechanic.
        // Re-entrancy guard. Adding hate notifies the controller, which raises another attack event,
        // which runs this handler again -- so a branch that adds hate on every blow recurses until the
        // engine's recursion cut-off fires. The Catacombs bosses are exactly that shape: retail puts no
        // flag var on their templar rule, because it is meant to accrue for as long as the tank swings.
        //
        // Retail fires on_attacked once per attack, not once per change to the hate list, so ignoring
        // the nested events is the faithful reading as well as the safe one. The village killers hid
        // this for two commits: their branch is once-only, so its flag stopped the recursion after a
        // single pass and the bug looked like correct behaviour.
        if (inOnAttacked)
            return;

        inOnAttacked = true;
        LastAttacker = creature;
        try
        {
            Evaluate(Pattern.OnAttacked);
        }
        finally
        {
            LastAttacker = null;
            inOnAttacked = false;
        }
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
    /// <summary>The player who opened this dialogue, for the duration of an <c>OnTalk</c> evaluation.</summary>
    public Aion.GameServer.Model.GameObjects.Players.Player? Talker { get; private set; }

    /// <summary>
    /// <c>on_talked_by_user</c>. Cleared afterwards so a later branch cannot read a stale talker.
    /// </summary>
    /// <remarks>
    /// Falls through to the base handler whatever the pattern does, so a talk branch never suppresses a
    /// dialogue this npc would otherwise open — retail's gate branches sit above a <c>do_nothing</c>
    /// fallback and the dialogue itself is the client's business, not the pattern's.
    /// </remarks>
    protected override void HandleDialogStart(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        Talker = player;
        try
        {
            Evaluate(Pattern.OnTalk);
        }
        finally
        {
            Talker = null;
        }

        base.HandleDialogStart(player);
    }

    /// <summary>
    /// <c>on_arrived_at_waypoint</c>. Runs before the base handler so a branch sees the arrival before
    /// the shout system does.
    /// </summary>
    /// <summary>Which step of its route this NPC last reached, or -1 if it is not walking one.</summary>
    /// <remarks>
    /// Zero-based, as this port's <c>RouteStep</c> indexes are; <see cref="When.AtWaypoint"/> converts
    /// from retail's one-based numbering so tables can quote retail's own index.
    /// </remarks>
    internal int WaypointIndex =>
        GetOwner().GetMoveController().GetCurrentStep()?.GetStepIndex() ?? -1;

    /// <summary><c>is_last_waypoint</c> — true once the NPC has reached the final step of its route.</summary>
    internal bool AtRouteEnd =>
        GetOwner().GetMoveController().GetCurrentStep()?.IsLastStep() ?? false;

    /// <summary>Whether <paramref name="skillId"/> is off cooldown for this NPC.</summary>
    /// <remarks>
    /// Backs <see cref="When.SkillReady"/>. <b>An NPC with no entry for the skill answers true, and
    /// that is the opposite of what it looks like it should do.</b> The reasoning matters, because the
    /// intuitive version — no entry, so not available — was written first and would have been a
    /// serious silent regression.
    /// <para>
    /// This port's <c>npc_skills</c> data is far thinner than the retail dump the tables are read from:
    /// of the 7,103 npc-and-skill pairs these guards name, only <b>2,124</b> appear in it. Casting does
    /// not care — <see cref="CastSkillAt"/> builds a <c>QueuedNpcSkillTemplate</c> from the id and never
    /// consults the list, so the skill goes out either way. Only this lookup cares.
    /// </para>
    /// <para>
    /// So answering false for a missing entry would have turned roughly 70% of these guards
    /// permanently false, silently killing branches whose action would have worked perfectly well —
    /// and because branch lists are first-match-wins, promoting the rungs beneath them into mechanics
    /// retail never runs. True means "no reason to think it is unavailable", which is the honest answer
    /// when the cooldown data simply is not there, and it leaves behaviour exactly as it was before
    /// this guard existed for every pair the port cannot speak to.
    /// </para>
    /// </remarks>
    internal bool SkillAvailable(int skillId)
    {
        foreach (NpcSkillEntry entry in GetOwner().GetSkillList().GetNpcSkills())
        {
            if (entry.GetSkillId() == skillId)
            {
                return !entry.HasCooldown();
            }
        }

        return true;
    }

    protected override void HandleMoveArrived()
    {
        Evaluate(Pattern.OnArrivedAtWaypoint);
        base.HandleMoveArrived();
    }

    protected override void HandleNotAtHome()
    {
        Evaluate(Pattern.OnEnterReturning);
        base.HandleNotAtHome();
    }

    protected override void HandleBackHome()
    {
        Evaluate(Pattern.OnLeaveAttack);
        Evaluate(Pattern.OnEnterIdle);

        // Retail's `on_leave_return_sp`, and it belongs here rather than beside `OnEnterReturning`:
        // the returning state is left by arriving, which is exactly what `OnBackHome` means in the
        // Java this is ported from -- it sets `AIState.IDLE`.
        Evaluate(Pattern.OnLeaveReturning);

        // **Only if there was a fight to end.** `ResetPattern` wipes a fight's state -- every battle
        // timer, the pending spawns, the flee -- and an npc reaches "back home" the moment it settles
        // after spawning, having never fought anybody. Running it there cancelled the battle timers
        // retail arms in `on_wake_up` before any of them could fire.
        if (inCombat)
        {
            ResetPattern();
        }

        base.HandleBackHome();
    }

    protected override void HandleDied()
    {
        ending = true;
        try
        {
            Evaluate(Pattern.OnDie);
        }
        finally
        {
            ending = false;
        }

        ResetPattern();
        base.HandleDied();
    }

    /// <summary>Retail's <c>on_see_friend_killed_by_user</c>.</summary>
    /// <remarks>
    /// The fallen NPC is not the message parameter -- retail's handler takes no object. It does name
    /// the killer, in a third of its branches: see <see cref="FriendsKiller"/>, which corrects a
    /// claim this remark used to make. See <see cref="FriendDeathNotice"/> for who hears it.
    /// </remarks>
    /// <summary>
    /// Retail's <c>OBJI_FRIEND</c> inside a friend-attacked branch: the one taking the hit.
    /// </summary>
    public Creature? Friend { get; private set; }

    /// <summary>Retail's <c>OBJI_ATTACKER</c> / <c>OBJI_CASTER</c> in the same branches.</summary>
    public Creature? FriendsAttacker { get; private set; }

    /// <summary>Set by <see cref="FriendCombatNotice"/> immediately before it raises the event.</summary>
    internal void NoteFriendInTrouble(Creature friend, Creature attacker)
    {
        Friend = friend;
        FriendsAttacker = attacker;
    }

    /// <summary>Retail's <c>on_see_friend_attacked</c>.</summary>
    protected override void HandleFriendAttacked(Creature hurt)
    {
        try
        {
            Evaluate(Pattern.OnFriendAttacked);
        }
        finally
        {
            Friend = null;
            FriendsAttacker = null;
        }

        base.HandleFriendAttacked(hurt);
    }

    /// <summary>Retail's <c>on_friend_spelled</c>.</summary>
    protected override void HandleFriendSpelled(Creature hurt)
    {
        try
        {
            Evaluate(Pattern.OnFriendSpelled);
        }
        finally
        {
            Friend = null;
            FriendsAttacker = null;
        }

        base.HandleFriendSpelled(hurt);
    }

    /// <summary><c>broadcast_message param_obj=OBJI_ATTACKER</c> from a friend-attacked branch.</summary>
    public void BroadcastAboutFriendsAttacker(int messageType, float range)
        => BroadcastAbout(messageType, range, FriendsAttacker, includeOwnSpawns: false);

    /// <summary>Puts hate on whoever is hitting a friend, and turns to face them.</summary>
    public void HateFriendsAttacker(int hate)
    {
        if (FriendsAttacker is not Creature attacker || attacker.IsDead())
            return;

        GetAggroList().AddHate(attacker, hate);
        GetOwner().SetTarget(attacker);
    }

    protected override void HandleFriendKilled(Creature dead)
    {
        try
        {
            Evaluate(Pattern.OnFriendKilled);
        }
        finally
        {
            FriendsKiller = null;
        }

        base.HandleFriendKilled(dead);
    }

    /// <summary>
    /// <c>broadcast_message param_obj=OBJI_KILLER</c> from a friend-killed branch -- naming whoever
    /// felled the friend rather than whoever felled this NPC.
    /// </summary>
    public void BroadcastAboutFriendsKiller(int messageType, float range)
        => BroadcastAbout(messageType, range, FriendsKiller, includeOwnSpawns: false);

    /// <summary>Puts hate on whoever felled a friend.</summary>
    /// <remarks>
    /// <b>It does not turn to face them</b>, unlike <see cref="HateAttacker"/> and
    /// <see cref="HateCaster"/>. Retail's action here is a bare <c>add_hate_point</c>, and the branches
    /// that use it follow with <c>switch_target target=OBJI_CUR_TARGET</c> — so turning first would
    /// hand that second action the killer instead of whoever the NPC was already facing, and the two
    /// hundred-point payloads would land on the same player. The taygas' pins measure exactly that.
    /// </remarks>
    public void HateFriendsKiller(int hate)
    {
        if (FriendsKiller is not Creature killer || killer.IsDead())
            return;

        GetAggroList().AddHate(killer, hate);
    }

    protected override void HandleDespawned()
    {
        // Before the reset, so a branch here still sees its timers, flags and spawn groups.
        ending = true;
        try
        {
            Evaluate(Pattern.OnDespawn);
        }
        finally
        {
            ending = false;
        }

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

    /// <summary>True while <c>on_attacked</c> is being evaluated. See <see cref="HandleAttack"/>.</summary>
    private bool inOnAttacked;

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
        List<Npc> leaving;
        lock (gate)
        {
            inCombat = false;
            for (int i = 0; i < timers.Length; i++)
                CancelSlot(i);
            if (idleTimer != null && !idleTimer.IsDone())
                idleTimer.Cancel(true);
            idleTimer = null;
            CancelFlee();
            Array.Clear(flags);
            Array.Clear(counters);
            leaving = new List<Npc>(transientSpawns);
            transientSpawns.Clear();
            spawnGroups.Clear();
        }

        // Outside the lock: deleting an npc runs its own controller, which takes other locks.
        foreach (Npc npc in leaving)
            Delete(npc);
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

                try
                {
                    foreach (PatternAction action in branch.Actions)
                        action(this);
                }
                finally
                {
                    spawnedThisBranch.Clear();
                }

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
    public new void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
    {
        if (IsDead() || Pattern.OnMessage.Length == 0)
            return;

        lock (gate)
        {
            CurrentMessage = messageType;
            MessageParam = param;
            MessageSender = sender;
            try
            {
                Evaluate(Pattern.OnMessage);
            }
            finally
            {
                CurrentMessage = -1;
                MessageParam = null;
                MessageSender = null;
            }
        }
    }

    /// <summary>Broadcasts to the rest of the encounter, optionally naming who this NPC is fighting.</summary>
    /// <remarks>
    /// Skips whatever the branch now running has already spawned, by default. See
    /// <see cref="spawnedThisBranch"/> for why, and <paramref name="includeOwnSpawns"/> for when that
    /// default is wrong.
    /// </remarks>
    /// <param name="includeOwnSpawns">
    /// <b>Let the branch's own spawns hear it.</b> The default exclusion was written for RM-56c, which
    /// lays traps and immediately tells traps to leave — there the message is plainly not meant for
    /// what was just made. <b>It is a heuristic, and spawn-then-point is the counter-example</b>: a
    /// lich that calls a servant and names its target in the same branch means that servant, and so
    /// does a corask that drops clodworms and sets them on somebody.
    /// <para>
    /// Kept as an opt-in rather than flipped, because the exclusion is right for every pattern already
    /// relying on it and the tables that need the other behaviour can say so in one word.
    /// </para>
    /// </param>
    public void Broadcast(int messageType, float range, bool aboutTarget, bool includeOwnSpawns = false)
        => BroadcastAbout(messageType, range, aboutTarget ? CurrentTarget : null, includeOwnSpawns);

    /// <summary><c>broadcast_message param_obj=OBJI_KILLER</c> — used from an <c>on_die</c> branch.</summary>
    /// <remarks>
    /// <see cref="Killer"/> is whoever did the most player damage, which is the closest this port has
    /// to the killing blow and is what every other <c>OBJI_KILLER</c> action here already uses.
    /// </remarks>
    public void BroadcastAboutKiller(int messageType, float range)
        => BroadcastAbout(messageType, range, Killer, includeOwnSpawns: false);

    /// <summary><c>broadcast_message param_obj=OBJI_ATTACKER</c>.</summary>
    /// <remarks>
    /// Distinct from naming the target, and retail uses both in the same pattern: the trained beasts
    /// name their <em>attacker</em> on the melee branch and their <em>caster</em> on the spell branch,
    /// which for a beast being focused by two players is two different names.
    /// </remarks>
    public void BroadcastAboutAttacker(int messageType, float range)
        => BroadcastAbout(messageType, range, LastAttacker, includeOwnSpawns: false);

    /// <summary><c>broadcast_message param_obj=OBJI_CASTER</c>.</summary>
    public void BroadcastAboutCaster(int messageType, float range)
        => BroadcastAbout(messageType, range, LastCaster, includeOwnSpawns: false);

    /// <summary><c>broadcast_message param_obj=OBJI_SELF</c> — the message names the sender.</summary>
    /// <remarks>
    /// Not the same as naming nobody. A call that carries no parameter is an announcement; one that
    /// names its sender is a request, and the hearer is expected to act <em>on the sender</em>. Idgel
    /// Dome's wave tank is the case: it broadcasts 22756 naming itself, and the wave's priest reads that
    /// parameter to know who to heal.
    /// </remarks>
    public void BroadcastAboutSelf(int messageType, float range)
        => BroadcastAbout(messageType, range, GetOwner(), includeOwnSpawns: false);

    private void BroadcastAbout(int messageType, float range, VisibleObject? about, bool includeOwnSpawns)
        => NpcMessageBus.Broadcast(GetOwner(), messageType, about, range,
            includeOwnSpawns || spawnedThisBranch.Count == 0 ? null : spawnedThisBranch);

    /// <summary>Puts hate on the NPC that sent a message, and turns to face it.</summary>
    /// <remarks>
    /// Retail's <c>add_hate_point target=OBJI_MESSAGE_SENDER</c>, which is a different object from
    /// <c>OBJI_MESSAGE_PARAM</c> and is how one NPC asks another to shoot <em>it</em>. The Sauro
    /// supply base's flame cannon is aimed this way: the mark the gunner plants on a player announces
    /// itself, and the cannon takes the mark rather than the player standing under it.
    /// </remarks>
    public void HateMessageSender(int hate)
    {
        if (MessageSender is not Npc sender || sender.IsDead())
            return;

        GetAggroList().AddHate(sender, hate);
        GetOwner().SetTarget(sender);
    }

    /// <summary>Turns to whoever a message named, without touching the hate list.</summary>
    /// <remarks>
    /// Retail's bare <c>switch_target target=OBJI_MESSAGE_PARAM</c>, which is a different action from
    /// <see cref="HateMessageTarget"/> and means a different thing: an abyss guard already in a fight
    /// turns towards the player a call names and keeps its own attacker's hate, while one standing
    /// about takes hate and commits. Without hate the turn lasts until the aggro list is consulted
    /// again, which is retail's own weakness in the action rather than ours in porting it.
    /// </remarks>
    public void TargetMessageParam()
    {
        if (MessageParam is Creature named && !named.IsDead())
            GetOwner().SetTarget(named);
    }

    /// <summary><c>switch_target</c> at the other creatures retail names.</summary>
    /// <remarks>
    /// 1,321 uses across seven subjects, of which this port read one -- <c>OBJI_MESSAGE_PARAM</c>, 397
    /// uses -- because <see cref="SwitchTarget"/> takes an <c>AggroTarget</c>, which is a *rank in the
    /// hate list* and cannot name a creature by its part in the event.
    /// <para>
    /// <b>The previous entry called that "a different operation with no helper here", which overstated
    /// it.</b> The operation is different, but <see cref="TargetMessageParam"/> shows the whole of it
    /// is a guarded <c>SetTarget</c>, so each of these is one line. What was actually missing was five
    /// one-line methods, not machinery.
    /// </para>
    /// <para>
    /// The dead check is not decoration: retail switches to the caster or the killer from handlers
    /// that fire as somebody dies, and pointing an NPC at a corpse leaves it holding a target it can
    /// never reach, which reads as a boss that has stopped doing anything.
    /// </para>
    /// <para>
    /// <c>OBJI_CUR_TARGET</c> (70 uses) is refused by the extractor: switching to the creature you are
    /// already targeting is a no-op, so the rung would be a step retail takes and this port cannot
    /// distinguish from doing nothing.
    /// </para>
    /// </remarks>
    public void TargetAttacker()
    {
        if (LastAttacker is Creature who && !who.IsDead())
        {
            GetOwner().SetTarget(who);
        }
    }

    public void TargetSeen()
    {
        if (SeenCreature is Creature who && !who.IsDead())
        {
            GetOwner().SetTarget(who);
        }
    }

    public void TargetCaster()
    {
        if (LastCaster is Creature who && !who.IsDead())
        {
            GetOwner().SetTarget(who);
        }
    }

    public void TargetMessageSender()
    {
        if (MessageSender is Creature who && !who.IsDead())
        {
            GetOwner().SetTarget(who);
        }
    }

    public void TargetKiller()
    {
        if (Killer is Creature who && !who.IsDead())
        {
            GetOwner().SetTarget(who);
        }
    }

    /// <summary>The NPC that just came into view, or null outside an <c>on_see_npc</c> branch.</summary>
    public Creature? SeenCreature { get; private set; }

    /// <summary>Retail's <c>on_see_npc</c>.</summary>
    /// <remarks>
    /// Players arrive through the same engine event, and retail's handler is <c>on_see_npc</c>: the
    /// guard is always a race test, so a player simply fails it. Passing them through anyway keeps the
    /// one event doing one job.
    /// </remarks>
    protected override void HandleCreatureSee(Creature creature)
    {
        base.HandleCreatureSee(creature);
        // Retail splits the event by what was seen, and the split is load-bearing: a trap that fires on
        // seeing a player must not fire on seeing the guard beside it.
        PatternBranch[] branches = creature is Player ? Pattern.OnSeeUser : Pattern.OnSeeNpc;
        if (branches.Length == 0)
            return;

        // RANGE IS PART OF SEEING. The engine event fires when the known list admits an object, which is
        // a much wider radius than an NPC's own sight -- so a trap with srange 1 was firing the moment a
        // raid came anywhere near it rather than when somebody stepped on it. Retail's on_see_user is
        // the NPC's sight, and FriendDeathNotice already reads it the same way.
        int sight = GetOwner().GetObjectTemplate().GetAggroRange();
        if (sight > 0 && !Aion.GameServer.Utils.PositionUtil.IsInRange(GetOwner(), creature, sight))
            return;

        SeenCreature = creature;
        try
        {
            Evaluate(branches);
        }
        finally
        {
            SeenCreature = null;
        }
    }

    /// <summary>Retail's <c>on_see_user_move</c>.</summary>
    /// <remarks>
    /// <b>The extractor recorded that "this port raises no 'a player moved nearby' event". It does.</b>
    /// <c>MovementNotifyTask</c> hands every moving creature to every NPC in its known list, and
    /// <c>HandleCreatureMoved</c> has been the way down to the AI the whole time.
    /// <para>
    /// <b>Range is part of it, exactly as in <see cref="HandleCreatureSee"/>.</b> The engine event
    /// covers the known list, which is far wider than an NPC's sight; without the aggro-range test a
    /// pattern meant to fire when somebody walks up would fire while they were still across the room.
    /// That mistake has already been made once here and the comment on seeing records it.
    /// </para>
    /// <para>
    /// Guarded against re-entrancy like the other handlers, and it returns before doing any work at all
    /// when the pattern has no such rungs -- which is nearly every NPC, on an event that fires for
    /// every step every player takes.
    /// </para>
    /// </remarks>
    protected override void HandleCreatureMoved(Creature creature)
    {
        base.HandleCreatureMoved(creature);
        if (Pattern.OnSeeUserMove.Length == 0 || inOnSeeUserMove || creature is not Player)
        {
            return;
        }

        int sight = GetOwner().GetObjectTemplate().GetAggroRange();
        if (sight > 0 && !Aion.GameServer.Utils.PositionUtil.IsInRange(GetOwner(), creature, sight))
        {
            return;
        }

        inOnSeeUserMove = true;
        SeenCreature = creature;
        try
        {
            Evaluate(Pattern.OnSeeUserMove);
        }
        finally
        {
            SeenCreature = null;
            inOnSeeUserMove = false;
        }
    }

    private bool inOnSeeUserMove;

    /// <summary>Whoever landed the blow being handled, or null outside an <c>on_attacked</c> branch.</summary>
    /// <summary>The creature whose attack put this npc into the fight -- retail's <c>OBJI_EVENT_TARGET</c>.</summary>
    /// <remarks>
    /// Recorded rather than inferred. The obvious shortcut is to call it the most-hated creature, which
    /// is true at the instant combat starts and false a moment later; the creature is right there at the
    /// call site, so there is no reason to guess.
    /// <para>
    /// <b>Set on the transition into combat only.</b> Written on every attack it would simply become
    /// <see cref="LastAttacker"/> under another name, and 1,912 casts meant for whoever opened the fight
    /// would follow the tank around instead.
    /// </para>
    /// </remarks>
    public Creature? EventTarget { get; private set; }

    public Creature? LastAttacker { get; private set; }

    /// <summary>Whoever cast the skill being handled, or null outside an <c>on_spelled</c> branch.</summary>
    public Creature? LastCaster { get; private set; }

    /// <summary>
    /// Retail's <c>OBJI_KILLER</c> inside a friend-killed branch: whoever felled the friend, or null
    /// outside one.
    /// </summary>
    /// <remarks>
    /// <b>This exists because a claim in this file was wrong.</b> The handler was shipped noting that
    /// retail's branches "never name" the killer. They do: <c>OBJI_KILLER</c> appears in <b>41 of the
    /// 129</b> <c>on_see_friend_killed_by_user</c> handlers in the 5.8 files and <b>15 of the 67</b>
    /// <c>on_sense_</c> ones. See docs/retail-ai-fidelity.md.
    /// </remarks>
    public Creature? FriendsKiller { get; private set; }

    /// <summary>Set by <see cref="FriendDeathNotice"/> immediately before it raises the event.</summary>
    internal void NoteFriendsKiller(Creature? killer) => FriendsKiller = killer;

    /// <summary>
    /// The skill that raised the current <c>on_spelled</c>, or 0 outside one. Retail's
    /// <c>is_event_skill_id</c> tests this.
    /// </summary>
    /// <remarks>
    /// <b>Set by <see cref="CreatureController"/> immediately before it raises the event</b>, the same
    /// way <see cref="FriendsKiller"/> reaches its watcher, because the event carries only the caster.
    /// The <c>Effect</c> is what distinguishes a skill from a swing and it exists only at that one
    /// point in the damage path, so the id has to be handed over rather than looked up later.
    /// <para>
    /// Cleared in the same <c>finally</c> as <see cref="LastCaster"/>: a branch that reads it after
    /// the handler has returned is reading the last fight's skill, and a stale id here would fire a
    /// despawn on a creature nobody hit.
    /// </para>
    /// </remarks>
    public int SpelledSkillId { get; private set; }

    /// <summary>Handed the skill id by the damage path just before the event is raised.</summary>
    internal void NoteSpelledSkill(int skillId) => SpelledSkillId = skillId;

    /// <summary>Retail's <c>on_spelled</c>.</summary>
    /// <remarks>
    /// Guarded against re-entrancy for the same reason <c>on_attacked</c> is: a branch that adds hate
    /// notifies the controller, and the controller can come straight back through here.
    /// </remarks>
    protected override void HandleSpelled(Creature caster)
    {
        base.HandleSpelled(caster);
        if (inOnSpelled || Pattern.OnSpelled.Length == 0)
        {
            SpelledSkillId = 0;
            return;
        }

        inOnSpelled = true;
        LastCaster = caster;
        try
        {
            Evaluate(Pattern.OnSpelled);
        }
        finally
        {
            LastCaster = null;
            SpelledSkillId = 0;
            inOnSpelled = false;
        }
    }

    private bool inOnSpelled;

    /// <summary>Puts hate on whoever this NPC is already holding.</summary>
    public void HateTarget(int hate)
    {
        if (CurrentTarget is not Creature target || target.IsDead())
            return;

        GetAggroList().AddHate(target, hate);
    }

    /// <summary>Puts hate on whoever just cast on this NPC and turns to face them.</summary>
    public void HateCaster(int hate)
    {
        if (LastCaster is not Creature caster || caster.IsDead())
            return;

        GetAggroList().AddHate(caster, hate);
        GetOwner().SetTarget(caster);
    }

    /// <summary>Puts hate on whoever just hit this NPC and turns to face them.</summary>
    /// <summary><c>reset_hatepoints</c> — forget everyone on the hate list.</summary>
    /// <remarks>
    /// 214 uses. Retail resets hate to make a boss re-pick rather than to end the fight, and that is
    /// what happens here too: <c>AggroList.Clear</c> empties the list and cancels the hate-reduction
    /// task without touching the AI's state, so the NPC re-acquires from whoever hits it next, exactly
    /// as an aggressive NPC with an empty list does.
    /// </remarks>
    public void ResetHate() => GetAggroList().Clear();

    /// <summary><c>reset_hatepoints is_except_most_hating=TRUE</c> — forget everyone but the tank.</summary>
    /// <remarks>
    /// 45 of the 214, and a different mechanic from the plain reset rather than a variation on it:
    /// the boss keeps the creature it is fighting and drops the rest of the room, which is how retail
    /// sheds accumulated hate from healers and adds without letting go of the tank.
    /// <para>
    /// The kept creature's hate is put back at the value it had. Reading it before the clear and
    /// restoring it after is deliberate — an implementation that removed the others one by one would
    /// leave the hate-reduction task running against a list it no longer matches, and
    /// <c>AggroList.Clear</c> is the only thing that cancels it.
    /// </para>
    /// </remarks>
    public void ResetHateExceptMostHated()
    {
        Creature? keep = GetAggroList().GetTarget(AggroTarget.MOST_HATED);
        int hate = keep == null ? 0 : GetAggroList().GetHate(keep);
        GetAggroList().Clear();
        if (keep != null && hate > 0)
        {
            GetAggroList().AddHate(keep, hate);
        }
    }

    public void HateAttacker(int hate)
    {
        if (LastAttacker is not Creature hitter || hitter.IsDead())
            return;

        GetAggroList().AddHate(hitter, hate);
        GetOwner().SetTarget(hitter);
    }

    /// <summary><c>spawn_on_target target_obj=OBJI_SEEN</c> — on whoever just came into view.</summary>
    public void SpawnOnSeen(int npcId, int spawnId, int count, float range, int liveSeconds)
    {
        if (SeenCreature is Creature seen && !seen.IsDead())
            SpawnAround(seen.GetPosition(), npcId, spawnId, count, range, liveSeconds);
    }

    /// <summary>Puts hate on the NPC just seen and turns to face it.</summary>
    /// <summary><c>add_hate_point target=OBJI_EVENT_TARGET</c> — hate whoever started this fight.</summary>
    /// <remarks>
    /// The last of retail's seven <c>add_hate_point</c> subjects to get a helper. The other six were
    /// written for hand-written classes and the extractor read none of them, taking only the message
    /// parameter -- 752 of the element's 1,793 uses.
    /// </remarks>
    public void HateEventTarget(int hate)
    {
        if (EventTarget is Creature who && !who.IsDead())
        {
            GetAggroList().AddHate(who, hate);
        }
    }

    public void HateSeen(int hate)
    {
        if (SeenCreature is not Creature seen || seen.IsDead())
            return;

        GetAggroList().AddHate(seen, hate);
        GetOwner().SetTarget(seen);
    }

    /// <summary>
    /// <c>add_hate_point target=OBJI_MESSAGE_PARAM</c> — hate on whoever a message named, <b>without</b>
    /// touching the current target.
    /// </summary>
    /// <remarks>
    /// Retail has two ways to answer a call and they are not interchangeable: this one, and
    /// <see cref="HateMessageTarget"/>, which is <c>switch_target</c> and moves the NPC. <b>Across the
    /// 5.8 files the plain form is the common one</b> — 700 answering branches use it alone against 349
    /// that switch — so an NPC already busy with somebody usually notes the call and keeps fighting.
    /// <para>
    /// Having only the switching form was a silent divergence: an answerer mid-fight would drop its
    /// target for whoever a neighbour named, every time, on two thirds of the calls in the game.
    /// </para>
    /// </remarks>
    public void AddHateToMessageTarget(int hate)
    {
        if (MessageParam is not Creature target || target.IsDead())
            return;

        GetAggroList().AddHate(target, hate);
    }

    /// <summary>
    /// <c>switch_target target=OBJI_MESSAGE_PARAM</c> — hate on whoever a message named, and turn to
    /// face them. See <see cref="AddHateToMessageTarget"/> for the form that does not turn.
    /// </summary>
    public void HateMessageTarget(int hate)
    {
        if (MessageParam is not Creature target || target.IsDead())
            return;

        GetAggroList().AddHate(target, hate);
        GetOwner().SetTarget(target);
    }

    /// <summary>
    /// <c>set_condition_spawn_variable</c> — moves one of the counters the world's spawn gates read.
    /// </summary>
    /// <remarks>
    /// Retail's own rule, settled by the fact that across all 12,446 uses in the dump <b>not one</b>
    /// carries a non-zero <c>set</c> and a non-zero <c>modify</c> together: a <paramref name="modify"/>
    /// of zero assigns <paramref name="set"/>, and anything else adds <paramref name="modify"/>.
    /// <para>
    /// The counter belongs to this NPC's own instance of its map. <see cref="Spawns.SpawnVariableRegistry"/>
    /// has the measurements: generic names like <c>v01</c> are written by patterns in nine unrelated
    /// maps, so one store for the server would have them corrupting each other — and 234 of the writing
    /// patterns have their npcs only on instance maps, so one store per map would have two groups
    /// running the same instance sharing a counter.
    /// </para>
    /// </remarks>
    public void SetSpawnVariable(string name, int set, int modify)
        => Spawns.SpawnVariableRegistry
            .For(GetOwner().GetWorldId(), GetOwner().GetInstanceId())
            .Write(name, set, modify);

    /// <summary>
    /// <c>increase_intvar</c> — bumps one of retail's four counters and asks where it landed.
    /// </summary>
    /// <remarks>
    /// A condition with a side effect, like <c>set_flag_var</c>: evaluating it <b>increments</b>. Branch
    /// lists are first-match-wins, so only the rungs actually reached bump their counter, which is what
    /// lets retail write a sequence as consecutive ranges — <c>0..1</c>, then <c>1..2</c>, then
    /// <c>2..3</c> — each rung firing on a successive pass.
    /// <para>
    /// <b>The bound flag is read as retail names it and that reading is inference.</b>
    /// <c>be_true_only_when_hit_the_bound</c> is TRUE in 1,145 of the 1,409 uses in the dump, and is
    /// taken to mean "true only on the pass that reaches <paramref name="upper"/>" rather than "true
    /// while inside the range". The consecutive-range idiom above only works under that reading — with
    /// the other one, a rung guarded <c>0..3</c> would fire three times running. Nothing in the dump
    /// states it outright, so it is written down here rather than left implicit.
    /// </para>
    /// </remarks>
    public bool IncreaseIntVar(int slot, int lower, int upper, bool onlyAtBound)
        => AddToIntVar(slot, 1, lower, upper, onlyAtBound);

    /// <summary><c>add_intvar</c> — the same, by a step retail names rather than by one.</summary>
    /// <remarks>
    /// 153 uses. It carries the identical bound fields as <c>increase_intvar</c> and differs only in
    /// <c>var_to_add</c>, so it is the same condition-with-a-side-effect and reads the bound flag the
    /// same way -- see <see cref="IncreaseIntVar"/>, which is now this with a step of one.
    /// <para>
    /// The step is not always one and not always small: 12 uses add 550. A counter that jumps a range
    /// rather than stepping through it is retail writing "this happened, and it counts for a lot",
    /// and collapsing it to an increment would make those rungs fire on the wrong pass.
    /// </para>
    /// </remarks>
    public bool AddToIntVar(int slot, int step, int lower, int upper, bool onlyAtBound)
    {
        lock (gate)
        {
            int now = counters[slot] += step;
            return onlyAtBound ? now == upper : now >= lower && now <= upper;
        }
    }

    /// <summary>Reads a counter without bumping it. For tests and diagnostics only.</summary>
    public int IntVar(int slot)
    {
        lock (gate)
            return counters[slot];
    }

    // ---- the idle timer ----------------------------------------------------------------------

    /// <summary>
    /// Arms the single idle slot, replacing whatever was in it — or <b>disarms</b> it, for a delay of
    /// zero.
    /// </summary>
    /// <remarks>
    /// Deliberately not gated on combat, unlike <see cref="FireTimer"/>. Its whole purpose is the
    /// business around a fight rather than in it — a controller retiring once it has spawned its
    /// wave, an orb calling out on a heartbeat — and half its uses are on NPCs that never fight at all.
    /// <para>
    /// <b>A zero delay stops the timer.</b> This remark used to say it meant "next tick", which was a
    /// guess, and it was wrong. Retail uses <c>set_idle_timer</c> 6,093 times; <b>1,090 carry
    /// <c>delay=0</c> and 1,006 of those sit inside <c>on_idle_timer</c></b>, re-arming the timer that
    /// just fired. Retail has no separate cancel action — only <c>add_battle_timer</c> and this — so
    /// zero is the only way a pattern can stop a cycle it started.
    /// </para>
    /// <para>
    /// What settles it is the shape of those rungs. <c>Ab1_N_ControlNoShowNPC_08</c> is a three-stage
    /// spawn alarm: two flag-guarded rungs each fire once and re-arm at 120 seconds, and then an
    /// <b>unguarded</b> fallback prints the last message and arms zero. Read as "next tick" that
    /// message repeats every tick for the life of the NPC, and <b>457 of the 1,006 are unguarded like
    /// it</b>. Read as "stop", the alarm ends after three stages, which is plainly what it is for.
    /// A further 41 rungs carry zero as their <em>only</em> action, which is a deliberate shutdown rung
    /// under this reading and a no-op busy loop under the other.
    /// </para>
    /// </remarks>
    public void SetIdleTimer(int delayMillis)
    {
        lock (gate)
        {
            if (idleTimer != null && !idleTimer.IsDone())
                idleTimer.Cancel(true);

            idleTimer = null;
            if (delayMillis <= 0)
                return;

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
            long dueAt = System.Environment.TickCount64 + delayMillis;
            if (timers[index] != null && !timers[index]!.IsDone() && timerDue[index] <= dueAt)
                return;
            timerDue[index] = dueAt;
            timerArms[index]++;
            CancelSlot(index);
            timers[index] = ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                FireTimer(index);
                return ValueTask.CompletedTask;
            }, delayMillis);
        }
    }

    /// <summary>Runs the branches a battle timer's firing selects.</summary>
    /// <remarks>
    /// <b>There is deliberately no state gate here, and there used to be one:
    /// <c>IsInState(AIState.FIGHT)</c>.</b> It was the second half of why a marker npc's clock never
    /// ran — <see cref="HandleBackHome"/> cancelled the timers outright, and any survivor fired into
    /// this and returned.
    /// <para>
    /// Retail draws no such line: a battle timer fires when it fires, and retail arms them from
    /// <c>on_wake_up</c> on npcs that never fight anybody. Kingspin's webs are the clearest case — a
    /// web arms a settle timer, that arms a sweep, and the sweep is what catches whoever is standing
    /// on it. None of it could run.
    /// </para>
    /// <para>
    /// <b>Narrowing the gate to "fighting, or never fought" was tried and was still wrong.</b> A web
    /// is an aggressive class with one metre of sight, so a player standing on it — the exact case the
    /// sweep exists for — aggros it: <c>inCombat</c> goes true while the state stays <c>IDLE</c>, and
    /// every clock stopped again. Measured rather than reasoned: with the player at sixty metres the
    /// whole chain ran, at one metre none of it did.
    /// </para>
    /// <para>
    /// What stops a rotation ticking on an npc that has finished fighting is
    /// <see cref="ResetPattern"/>, which cancels every timer when it dies or reaches home. That was
    /// always the real mechanism; the state check was a second one that also caught markers.
    /// </para>
    /// </remarks>
    private void FireTimer(int index)
    {
        lock (gate)
        {
            timerFires[index]++;
            timers[index] = null;
            if (IsDead())
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

    /// <summary>Reads a flag without touching it. For tests and diagnostics only.</summary>
    /// <remarks>
    /// Every other way in is a test-and-<em>something</em>, so a probe that asked whether a flag was set
    /// changed the answer by asking. That made two failure modes indistinguishable from outside: a flag
    /// that is never set and a flag that is set and then cleared both show up as a one-shot branch
    /// firing every time. <b>Same rationale as <see cref="FleeingTo"/></b> — the state was always there,
    /// and only the reading of it was missing.
    /// </remarks>
    public bool IsFlagSet(int flag)
    {
        lock (gate)
            return flags[flag];
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

    /// <summary>Reads a counter without touching it.</summary>
    /// <remarks>
    /// Every other counter op here mutates, which is right for retail's test-and-set guards and wrong
    /// for a pattern that has to look at the same counter from five branches in one event. See
    /// <see cref="AiPattern.When.CountEquals"/> for why that shape needs a read-only test.
    /// </remarks>
    public bool CounterEquals(int counter, int value)
    {
        lock (gate)
            return counters[counter] == value;
    }

    /// <summary><c>increase_intvar</c> as an action: adds one and holds the result in range.</summary>
    public void IncrementCounter(int counter, int low, int high)
    {
        lock (gate)
            counters[counter] = Math.Clamp(counters[counter] + 1, low, high);
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

    /// <summary>
    /// Replaces this NPC's percent roll. **For tests only**, and per instance rather than global.
    /// </summary>
    /// <remarks>
    /// Retail's <c>test_probability</c> appears 7,747 times in the 5.8 data, and this port had been
    /// omitting it: a branch retail rolls for fired every time. Restoring the guards is right and was
    /// blocked on verification rather than on the code -- a rolled branch makes a pin that counts
    /// occurrences flaky, and rewriting those pins around rates costs the precision that made them
    /// useful.
    /// <para>
    /// So the roll gets a seam. A pin can force it to pass, force it to fail, or seed it, and keep its
    /// exact counts either way. <b>Per instance and not static</b>: xUnit runs collections in parallel,
    /// and a static hook would leak between them.
    /// </para>
    /// </remarks>
    public Func<int, bool>? RollOverride { get; set; }

    /// <summary><c>test_probability</c>: true on a percent roll.</summary>
    public virtual bool RollPercent(int percent)
        => RollOverride is { } roll ? roll(percent) : Rnd.Chance() < percent;

    // ---- actions -----------------------------------------------------------------------------

    /// <summary>Queues one of this NPC's own skills, at the level its npc_skills entry gives it.</summary>
    public void CastSkill(int skillId, NpcSkillTargetAttribute target)
    {
        if (!IsDead())
            NpcSkillCasting.QueueAtDataLevel(GetOwner(), skillId, target);
    }

    /// <summary>
    /// Queues a skill at one particular creature: retail's role targets, which name the creature
    /// involved in the event rather than a place in the hate list.
    /// </summary>
    /// <remarks>
    /// The aim is taken now, when the branch runs, and travels with the queued entry -- see
    /// <see cref="AimedSkillEntry"/>. Resolving it later out of the aggro list, which is all this port
    /// could do before, finds whoever is convenient at drain time instead of the creature retail named.
    /// <para>
    /// A role with nobody in it does nothing. <c>on_spelled</c> can fire with no caster left, and a cast
    /// with no target is not a cast at the most-hated creature -- it is a cast that does not happen.
    /// </para>
    /// </remarks>
    /// <summary><c>is_user_flying</c>: whether a particular creature is in the air.</summary>
    /// <remarks>
    /// Retail asks this 663 times, overwhelmingly about the creature that opened the fight -- a boss
    /// that behaves differently against someone who pulled it from the air and cannot be reached the
    /// usual way.
    /// <para>
    /// <b>Gliding does not count.</b> This port distinguishes <c>FLYING</c> from <c>GLIDING</c>, and
    /// retail's condition names flying; whether its own engine folded gliding in is not something the
    /// pattern data says, so the narrower reading is taken and recorded here rather than guessed wide.
    /// </para>
    /// </remarks>
    public bool IsAirborne(Creature? who)
        => who is Player player && player.IsInFlyState(Model.GameObjects.State.FlyState.FLYING);

    /// <summary>
    /// <c>use_skill_by_attacker_indicator restricted_range=TRUE</c> — rank the hate list, but only
    /// among the creatures the skill can actually reach.
    /// </summary>
    /// <remarks>
    /// 53 uses, refused until now on the grounds that the skill queue picks its target when it drains
    /// and takes no range bound. That is true of the <i>unaimed</i> path; <see cref="CastSkillAt"/>
    /// resolves a creature now and sends it with the entry, which is exactly what this needs.
    /// <para>
    /// <b>Retail states no distance</b> — <c>restricted_range</c> is a bare TRUE — so the reach is the
    /// skill's own <c>first_target_range</c>. A skill with no template, or one declaring no range,
    /// falls back to the <i>unrestricted</i> pick rather than passing zero through, on the same
    /// principle as <see cref="When.SkillReady"/>: where this port has no data, it does not invent a
    /// bound. Passing zero would not mean "nobody" in any case — <c>PositionUtil.IsInRange</c> is
    /// called center-to-center=false here, so both bound radii are added and a zero range still
    /// reaches anything touching a large boss. Falling back is about not inventing a number, not about
    /// avoiding an empty result.
    /// </para>
    /// <para>
    /// The difference this makes is the whole point of the element. <c>ATTACKERI_RANDOM_ONE</c> over
    /// the whole hate list picks the healer standing at the back as often as the tank; restricted, it
    /// picks among whoever is actually close enough to be hit.
    /// </para>
    /// </remarks>
    public void CastSkillOnRankedInReach(AggroTarget which, int skillId)
    {
        if (IsDead())
        {
            return;
        }

        SkillTemplate? template = DataManager.SKILL_DATA.GetSkillTemplate(skillId);
        int reach = template?.GetProperties()?.firstTargetRange ?? 0;
        Creature? aim = reach > 0
            ? GetAggroList().GetTarget(which, reach)
            : GetAggroList().GetTarget(which);
        CastSkillAt(aim, skillId);
    }

    public void CastSkillAt(Creature? aim, int skillId)
    {
        if (IsDead() || aim == null || aim.IsDead())
            return;

        int level = NpcSkillCasting.LevelOf(GetOwner(), skillId);
        GetOwner().QueueSkill(new AimedSkillEntry(
            new QueuedNpcSkillTemplate(skillId, level, 0, NpcSkillTargetAttribute.NONE), aim));
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
        if (IsDead())
            return;

        lock (gate)
            immediateCasts++;
        NpcSkillCasting.UseOnSelfNow(GetOwner(), skillId);
    }

    /// <summary>Casts one of this NPC's own skills at somebody else now, bypassing the queue.</summary>
    /// <remarks>
    /// The aimed twin of <see cref="CastSkillNow"/>, and it exists for the same reason: a branch that
    /// casts and then despawns the caster leaves a queued cast nobody will ever drain. Kingspin's webs
    /// are exactly that shape — root whoever stepped on it, cry, vanish.
    /// </remarks>
    public void CastSkillAtNow(Creature? aim, int skillId)
    {
        if (IsDead() || aim == null || aim.IsDead())
        {
            return;
        }

        lock (gate)
            immediateCasts++;
        NpcSkillCasting.UseOnNow(GetOwner(), aim, skillId);
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

    /// <summary>
    /// Replaces this NPC's target selection. **For tests only**, and per instance like
    /// <see cref="RollOverride"/>.
    /// </summary>
    /// <remarks>
    /// <c>AggroTarget.RANDOM</c> can re-pick the creature already targeted, so "the target changed" is a
    /// coin flip and a pin that counts switches is flaky. Masto's four band cadences are unpinnable for
    /// exactly that reason: each band fires a random switch, and a fire is only visible when the pick
    /// happens to land elsewhere.
    /// <para>
    /// <see cref="RollOverride"/> does not reach this, because a random switch never goes through
    /// <see cref="RollPercent"/> — it goes through the aggro list. This is the same seam for the other
    /// source of randomness, so a pin can make a fire observable without making the encounter
    /// deterministic in production.
    /// </para>
    /// </remarks>
    public Func<AggroTarget, Creature?>? TargetPickOverride { get; set; }

    public void SwitchTarget(AggroTarget which)
    {
        Creature? next = TargetPickOverride is { } pick
            ? pick(which)
            : GetAggroList().GetTarget(which);
        if (next != null)
            GetOwner().SetTarget(next);
    }

    /// <summary>
    /// <c>display_system_message</c> — a line to everyone on the map instance, not a line the NPC says.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="Say"/> and not interchangeable with it. A shout is
    /// <c>SM_SYSTEM_MESSAGE(ChatType.NPC, …)</c> broadcast within fifty metres and attributed to the
    /// NPC; this is the plain form sent to the whole instance and attributed to nobody. Retail uses the
    /// first 932 times and the second 375, and they read very differently in play: one is a monster
    /// talking, the other is the encounter telling the raid what just happened.
    /// <para>
    /// The message ids come from the client's own <c>strings.xml</c> --
    /// <c>tools/client-extract/out/string_ids.tsv</c> resolves all 3,492 the patterns use.
    /// </para>
    /// <para>
    /// <b>The difference from <see cref="Say"/> is not pinned.</b> Swapping this for the shout form
    /// changes which packet goes out and to whom, and nothing in the harness observes packets -- a
    /// mutation that does exactly that survives. The two are kept apart on the strength of the retail
    /// elements being distinct, not on a test, and that is worth knowing before trusting it.
    /// </para>
    /// </remarks>
    public void SystemMessage(int messageId, int delayMillis = 0)
    {
        if (!IsDead())
            PacketSendUtility.BroadcastToMap(GetOwner(), messageId, delayMillis);
    }

    public void Say(int messageId, int delayMillis = 0)
    {
        if (!IsDead())
            PacketSendUtility.BroadcastMessage(GetOwner(), messageId, delayMillis);
    }

    /// <summary><c>goto_waypoint</c> — begin walking the route named on this NPC's spawn.</summary>
    /// <summary>
    /// <c>goto_waypoint</c>: walk this npc's own route from a given step.
    /// </summary>
    /// <remarks>
    /// Retail's waypoint is an index into the npc's route rather than a named path, so this is the
    /// ordinary route walk with a starting step. An npc that is not a path walker, or a step past the
    /// end of its route, does nothing -- retail's patterns are shared across npcs and not every one of
    /// them has the route the pattern assumes.
    /// <para>
    /// <b>Retail's <c>move_type</c> is carried.</b> This paragraph used to say the port's route walking
    /// had one speed and that the extractor refused the 210 uses asking for a run. Both stopped being
    /// true when <see cref="GotoWaypointRunning"/> and <see cref="ContinueRouteRunning"/> were written:
    /// <c>MOVETYPE_RUN</c> now emits those instead of being refused. This method is the walking half.
    /// </para>
    /// </remarks>
    public void GotoWaypoint(int step)
    {
        if (!IsDead())
            WalkManager.StartRouteWalkingAt(this, step);
    }

    /// <summary>
    /// <c>goto_waypoint move_type=MOVETYPE_RUN</c> — the same route, taken at running pace.
    /// </summary>
    /// <remarks>
    /// <b>This port was recorded as having "one route speed" for several entries, and it does not.</b>
    /// <c>NpcMoveController</c> picks its movement mask from <c>CreatureState.WALK_MODE</c>:
    /// <c>EmoteManager.EmoteStartWalking</c> sets it, and <c>EmoteStartReturning</c> and
    /// <c>EmoteStartFollowing</c> unset it and broadcast <c>CHANGE_SPEED</c> — which is running.
    /// <para>
    /// <c>EternalBastionAssaulterNpcAI</c> has done exactly this by hand since it was written: start
    /// the walk, unset the state, send the emote. Three lines, and the same three are used here rather
    /// than a second way of moving. 210 <c>goto_waypoint</c> uses and 186 <c>goto_next_waypoint</c>
    /// uses were refused for want of them.
    /// </para>
    /// </remarks>
    public void GotoWaypointRunning(int step)
    {
        if (IsDead())
        {
            return;
        }

        WalkManager.StartRouteWalkingAt(this, step);
        RunRatherThanWalk();
    }

    /// <summary>
    /// <c>goto_next_waypoint move_type=MOVETYPE_RUN</c> — carry on to the next point, running.
    /// </summary>
    /// <remarks>
    /// The walking form of this is <see cref="AiPattern"/>'s <c>Do.ContinueRoute</c> and does nothing,
    /// because arriving already advances the route. The running form is not nothing: the advance
    /// happens either way, and this changes the pace it happens at.
    /// </remarks>
    public void ContinueRouteRunning()
    {
        if (!IsDead())
        {
            RunRatherThanWalk();
        }
    }

    /// <summary>Drops the NPC out of walk mode and tells the client its speed changed.</summary>
    private void RunRatherThanWalk()
    {
        GetOwner().UnsetState(CreatureState.WALK_MODE);
        PacketSendUtility.BroadcastPacket(
            GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetOwner().GetObjectId()));
    }

    public void StartWalking()
    {
        if (GetOwner().IsPathWalker())
            WalkManager.StartWalking(this);
    }

    /// <summary>
    /// <c>goto_next_waypoint</c> for an npc that is <b>not walking</b> — the case where the element is
    /// an instruction rather than a description.
    /// </summary>
    /// <remarks>
    /// <b>This action was a deliberate no-op, and the reasoning behind that was right about the case it
    /// considered and silent about this one.</b> For an npc already on its route, arriving advances the
    /// route by itself, so a rung that advanced it again would make the patrol visit every other point;
    /// that argument still holds, and the <c>WALKING</c> test below is what keeps it holding.
    /// <para>
    /// But <c>BIDF5_R2_Runner</c> — nine npcs — stands still. Its <c>on_wake_up</c> is empty, its
    /// <c>on_see_user</c> and <c>on_see_user_move</c> rungs display <c>STR_MSG_IDF5_R2_RUNNER_START</c>
    /// and call <c>goto_next_waypoint</c>, and its <c>on_arrived_at_waypoint</c> despawns it at the last
    /// point. <b>The whole race is that call.</b> Against a no-op the runner never left the line, the
    /// message never showed, and nothing about it read as broken — it simply stood there.
    /// </para>
    /// <para>
    /// The route is started rather than <see cref="StartWalking"/> called, because that tries random
    /// walking first and an npc may carry both a walker id and a random-walk range.
    /// </para>
    /// </remarks>
    public void ContinueRoute()
    {
        if (IsDead() || IsInState(AIState.WALKING) || !GetOwner().IsPathWalker())
        {
            return;
        }

        WalkManager.StartRouteWalking(this);
    }

    /// <summary>
    /// Retail's <c>attack_most_hating</c> with <c>SKILLI_NONE</c>: stop whatever you are doing and fight
    /// the top of your hate list.
    /// </summary>
    /// <remarks>
    /// The half that matters even with an empty hate list is <b>stopping</b>. A patrol rung that ends a
    /// march has to take the NPC out of WALKING, or the walker loops it back to the first point and the
    /// march never ends -- routes default to <c>LoopType.NORMAL</c> and most carry no <c>loop_type</c> at
    /// all.
    /// </remarks>
    public void AttackMostHating()
    {
        WalkManager.StopWalking(this);
        if (GetOwner().GetAggroList().GetTarget(AggroTarget.MOST_HATED) is not Creature mostHated)
            return;
        SetStateIfNot(AIState.FIGHT);
        GetOwner().SetTarget(mostHated);
        OnCreatureEvent(AiEventType.Attack, mostHated);
    }

    public void DespawnSelf() => AIActions.DeleteOwner(this);

    /// <summary>Spawns one NPC per listed coordinate.</summary>
    public void SpawnAt(int npcId, int spawnId, int liveSeconds, params SpawnSpot[] spots)
        => SpawnAt(npcId, spawnId, liveSeconds, false, spots);

    /// <summary>As above, for a spawn retail marks <c>despawn_at_attack_state</c>.</summary>
    public void SpawnAt(int npcId, int spawnId, int liveSeconds, bool untilFightEnds,
        params SpawnSpot[] spots)
    {
        foreach (SpawnSpot spot in spots)
            Track(spawnId, liveSeconds, Spawn(npcId, spot.X, spot.Y, spot.Z, spot.Heading), untilFightEnds);
    }

    /// <summary>
    /// <c>SPAWN_LOCATION_WAY_POINT_START</c> — placed at the first step of a named route, walking it.
    /// </summary>
    /// <remarks>
    /// <b>The most-used spawn location retail has that this engine could not express.</b> 881 spawns
    /// across the 5.8 pattern dump carry it, and every one of them means the same thing: the add does not
    /// appear at the boss, it appears at the mouth of a corridor and comes down it. A port that placed
    /// those adds at the summoner's feet would be describing a different fight — the walk <i>is</i> the
    /// mechanic, because it is the time the raid gets to react.
    /// <para>
    /// The route is retail's own <c>pathname</c>, resolved through the walker data under its retail name.
    /// <b>If no route by that name is loaded the add still spawns, at the summoner, and stands</b> — the
    /// same fallback Tiamat's rush wave takes, on the same reasoning: an add in the wrong place is a
    /// smaller error than an add that never arrives, and 123 of retail's 467 pattern route names are
    /// undefined in retail's own shipped data, so the fallback is reachable through no fault of the port.
    /// </para>
    /// </remarks>
    public void SpawnOnPath(int npcId, int spawnId, string pathName, float range, int liveSeconds)
    {
        WalkerTemplate? route = DataManager.WALKER_DATA.GetWalkerTemplate(pathName);
        List<RouteStep>? steps = route?.GetRouteSteps();
        if (steps == null || steps.Count == 0)
        {
            SpawnNear(npcId, spawnId, 1, range, liveSeconds);
            return;
        }

        RouteStep start = steps[0];
        float x = start.GetX();
        float y = start.GetY();
        if (range > 0f)
        {
            // Retail's spawn_range scatters around the start point rather than moving the start point,
            // so the walk still begins at step zero -- the offset is only how far off the line it begins.
            double angle = Rnd.NextFloat(360f) * Math.PI / 180.0;
            float distance = Rnd.NextFloat(range);
            x += (float)(Math.Cos(angle) * distance);
            y += (float)(Math.Sin(angle) * distance);
        }

        VisibleObject? spawned = Spawn(npcId, x, y, start.GetZ(), 0);
        Track(spawnId, liveSeconds, spawned);
        if (spawned is not Npc walker)
            return;

        walker.GetSpawn()?.SetWalkerId(pathName);
        if (walker.GetAi() is NpcAI ai)
            WalkManager.StartWalking(ai);
    }

    /// <summary>Spawns around this NPC, scattered within <paramref name="range"/> metres.</summary>
    public void SpawnNear(int npcId, int spawnId, int count, float range, int liveSeconds,
        bool untilFightEnds = false)
        => SpawnAround(GetPosition(), npcId, spawnId, count, range, liveSeconds, untilFightEnds);

    /// <summary>
    /// <c>spawn ... SPAWN_LOCATION_MY_POINT</c> with retail's <c>dir</c>: at this NPC's feet, facing a
    /// direction of its own rather than inheriting the spawner's.
    /// </summary>
    /// <remarks>
    /// <see cref="SpawnNear"/> hands the new NPC the spawner's heading, which is right for a wave of adds
    /// and wrong for anything that has to point somewhere. Tiamat Stronghold's siege weapons are the case
    /// that forced it: each destroyed Vritra cannon leaves a player-usable one behind, and retail gives
    /// every one of the eleven its own <c>dir</c> — 165, 50, 90, 35, 50, 0, 153, 40, 150, 105, 0 — because
    /// a siege weapon aimed the wrong way is furniture. Degrees, converted by
    /// <see cref="PositionUtil.ConvertAngleToHeading"/>.
    /// </remarks>
    public void SpawnFacing(int npcId, int spawnId, int degrees, int liveSeconds)
    {
        WorldPosition here = GetPosition();
        VisibleObject? spawned = Spawn(npcId, here.GetX(), here.GetY(), here.GetZ(),
            (sbyte)PositionUtil.ConvertAngleToHeading(degrees));
        Track(spawnId, liveSeconds, spawned);
    }

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
        AttackAfterSpawn.NextTick(summon, victim, hate);
    }

    /// <summary>Spawns around whoever this NPC is facing, which is where <c>spawn_on_target</c> puts them.</summary>
    public void SpawnOnTarget(int npcId, int spawnId, int count, float range, int liveSeconds)
        => SpawnOnTarget(npcId, spawnId, count, range, liveSeconds, attackHate: 0);

    /// <summary>
    /// <c>flee_from</c>: run directly away from whoever this NPC is fighting, for a number of seconds.
    /// </summary>
    /// <remarks>
    /// <b>Retail specifies a duration, not a distance</b> — <c>&lt;seconds&gt;</c> and nothing else —
    /// so how far the NPC gets is its own run speed times that time, which is what this computes.
    /// It is the fourth most common action in the 5.8 files after waypoints (353 uses across 226
    /// patterns) and it was the largest piece of vocabulary this port was missing.
    /// <para>
    /// Standing exactly on its target it has no direction to run in, and picks the way it is already
    /// facing rather than dividing by zero.
    /// </para>
    /// <para>
    /// <c>push_state</c> is not translated: retail pushes the AI state so the NPC returns to what it
    /// was doing, and ours never left — it keeps its hate list and its timers throughout, and the
    /// move controller is simply told to stop when the clock runs out.
    /// </para>
    /// </remarks>
    public void Flee(int seconds) => FleeFrom(seconds, CurrentTarget);

    /// <summary>
    /// <c>flee_from from=OBJI_SEEN</c> — run from what just came into view rather than from whatever
    /// this NPC is fighting.
    /// </summary>
    /// <remarks>
    /// The distinction is the whole of a skittish npc. A drakie that has never fought has no target, so
    /// the target-based flee is a no-op for exactly the creature the action exists for — which is what
    /// the first run of the drake-mark pins measured.
    /// </remarks>
    public void FleeFromSeen(int seconds) => FleeFrom(seconds, SeenCreature);

    /// <summary>
    /// <c>flee_from from=OBJI_MESSAGE_PARAM</c> — run from whoever a message named.
    /// </summary>
    /// <remarks>
    /// The black claw tamers' use of it is the clearest: their tayga names its killer as it dies, and
    /// the tamer runs from that player rather than from whatever it was fighting.
    /// </remarks>
    public void FleeFromMessageParam(int seconds) => FleeFrom(seconds, MessageParam as Creature);

    /// <summary>The remaining <c>flee_from</c> subjects, all reading a role this class already tracks.</summary>
    /// <remarks>
    /// <c>flee_from</c> is 353 uses. The three above covered 262 of them and the rest named the
    /// attacker, the caster, the killer, the event target, the message sender or the talker — every one
    /// a creature <see cref="PatternAi"/> already holds, so these are one line apiece over the same
    /// <c>FleeFrom</c> helper.
    /// <para>
    /// <c>OBJI_SELF</c> (3 uses) is refused by the extractor: fleeing from yourself has no direction,
    /// and <c>FleeFrom</c> would fall through to its heading fallback and run the NPC forwards, which
    /// looks like a working mechanic and is not one.
    /// </para>
    /// </remarks>
    public void FleeFromAttacker(int seconds) => FleeFrom(seconds, LastAttacker);

    /// <summary><c>flee_from from=OBJI_ATTACKER</c> read on <c>on_see_friend_attacked</c>.</summary>
    /// <remarks>
    /// Retail reuses one role name for two creatures. On <c>on_attacked</c> the attacker is whoever hit
    /// this NPC; on <c>on_see_friend_attacked</c> it is whoever hit the friend, and those are separate
    /// fields here. The extractor picks between them by handler.
    /// </remarks>
    public void FleeFromFriendsAttacker(int seconds) => FleeFrom(seconds, FriendsAttacker);

    public void FleeFromCaster(int seconds) => FleeFrom(seconds, LastCaster);

    public void FleeFromKiller(int seconds) => FleeFrom(seconds, Killer);

    public void FleeFromEventTarget(int seconds) => FleeFrom(seconds, EventTarget);

    public void FleeFromMessageSender(int seconds) => FleeFrom(seconds, MessageSender);

    public void FleeFromTalker(int seconds) => FleeFrom(seconds, Talker);

    private void FleeFrom(int seconds, Creature? fleeFrom)
    {
        if (seconds <= 0 || IsDead() || fleeFrom is not Creature from)
            return;

        lock (gate)
        {
            CancelFlee();

            WorldPosition here = GetPosition();
            float dx = here.GetX() - from.GetX();
            float dy = here.GetY() - from.GetY();
            float length = MathF.Sqrt((dx * dx) + (dy * dy));
            if (length < 0.001f)
            {
                double facing = here.GetHeading() * 3.0 * Math.PI / 180.0;
                dx = (float)Math.Cos(facing);
                dy = (float)Math.Sin(facing);
                length = 1f;
            }

            float distance = GetOwner().GetGameStats().GetMovementSpeedFloat() * seconds;
            float x = here.GetX() + (dx / length * distance);
            float y = here.GetY() + (dy / length * distance);

            FleeingTo = (x, y);
            GetOwner().GetMoveController().MoveToPoint(x, y, here.GetZ());

            fleeing = ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                StopFleeing();
                return ValueTask.CompletedTask;
            }, seconds * 1000);
        }
    }

    private void StopFleeing()
    {
        lock (gate)
        {
            fleeing = null;
            FleeingTo = null;
            GetOwner().GetMoveController().AbortMove();
            if (!IsDead())
                Evaluate(Pattern.OnStopFleeing);
        }
    }

    private void CancelFlee()
    {
        if (fleeing != null && !fleeing.IsDone())
            fleeing.Cancel(true);
        fleeing = null;
        FleeingTo = null;
    }

    /// <summary><c>spawn_on_target target_obj=OBJI_KILLER</c>.</summary>
    public void SpawnOnKiller(int npcId, int spawnId, int count, float range, int liveSeconds)
    {
        if (Killer is Player killer)
            SpawnAround(killer.GetPosition(), npcId, spawnId, count, range, liveSeconds);
    }

    /// <summary>
    /// <c>spawn_on_target</c>, optionally with <c>attack_target_after_spawn</c>: the adds land on
    /// whoever this NPC is fighting and, when <paramref name="attackHate"/> is non-zero, arrive
    /// already fighting that same player.
    /// </summary>
    /// <remarks>
    /// The difference is not cosmetic. A guard's ranger trap that engages the player it lands on is a
    /// thing you have to deal with; the same trap standing inert is a thing you walk away from.
    /// </remarks>
    /// <inheritdoc cref="SpawnOnAttacker" path="/param[@name='validDistance']"/>
    public void SpawnOnTarget(int npcId, int spawnId, int count, float range, int liveSeconds,
        int attackHate, float validDistance = 0f)
    {
        Creature? target = CurrentTarget;
        if (target == null)
            return;

        if (validDistance > 0f && !IsInRange(target, (int)System.Math.Ceiling(validDistance)))
            return;

        if (attackHate <= 0)
        {
            SpawnAround(target.GetPosition(), npcId, spawnId, count, range, liveSeconds);
            return;
        }

        var placed = new List<Npc>();
        SpawnAroundInto(placed, target.GetPosition(), npcId, spawnId, count, range, liveSeconds);
        foreach (Npc summon in placed)
            AttackAfterSpawn.NextTick(summon, target, attackHate);
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
                AttackAfterSpawn.NextTick(summon, target, attackHate);
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
    /// <param name="validDistance">
    /// Retail's <c>valid_distance</c>: how far from this NPC the chosen attacker may be and still get
    /// one. Beyond it the spawn is skipped entirely.
    /// </param>
    /// <remarks>
    /// <b><paramref name="range"/> and <paramref name="validDistance"/> are different numbers and were
    /// once confused here.</b> Range is retail's <c>spawn_range</c>, the scatter around the target;
    /// valid distance is the eligibility radius around the caster. Miladi's succubi carry
    /// <c>spawn_range=0</c> and <c>valid_distance=50</c>, and reading the fifty as scatter put her adds
    /// up to fifty metres from the player they are supposed to land on — which is the whole mechanic.
    /// </remarks>
    public void SpawnOnAttacker(AggroTarget which, int npcId, int spawnId, float range, int liveSeconds,
        int attackHate = 0, float validDistance = 0f)
    {
        Creature? target = GetAggroList().GetTarget(which);
        if (target == null)
            return;

        // Rounded up, so a target exactly at the boundary is still eligible; retail's numbers are whole
        // metres and the int overload is the only range check this codebase has.
        if (validDistance > 0f && !IsInRange(target, (int)System.Math.Ceiling(validDistance)))
            return;

        if (attackHate <= 0)
        {
            SpawnAround(target.GetPosition(), npcId, spawnId, 1, range, liveSeconds);
            return;
        }

        // The fourth and last of retail's spawn placements to learn attack_target_after_spawn. Xasta's
        // trap is why: ten million hate is what keeps it on the player it picked rather than peeling
        // to whoever is tanking.
        var placed = new List<Npc>();
        SpawnAroundInto(placed, target.GetPosition(), npcId, spawnId, 1, range, liveSeconds);
        foreach (Npc summon in placed)
            AttackAfterSpawn.NextTick(summon, target, attackHate);
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
    /// distinction cannot be observed.
    /// <para>
    /// <b>An asymmetric case now exists</b> and the question is still open. Captain Adhati's waves sit
    /// at offsets like (8, 8) and (3, -2), which under a rotating interpretation would land somewhere
    /// else entirely. He stands on a fixed mark on the Dreadgion facing one way, so the two readings
    /// cannot be told apart from the pattern alone, and this keeps the world-axis one it has always
    /// had. Settling it needs observation of the live encounter, not more data.
    /// </para>
    /// </remarks>
    public void SpawnOffset(int npcId, int spawnId, float dx, float dy, int liveSeconds, float dz = 0f,
        bool untilFightEnds = false)
    {
        WorldPosition at = GetPosition();
        Track(spawnId, liveSeconds,
            Spawn(npcId, at.GetX() + dx, at.GetY() + dy, at.GetZ() + dz, (sbyte)at.GetHeading()));
    }

    private void SpawnAround(WorldPosition at, int npcId, int spawnId, int count, float range,
        int liveSeconds, bool untilFightEnds = false)
        => SpawnAroundInto(null, at, npcId, spawnId, count, range, liveSeconds, untilFightEnds);

    /// <summary>The same, collecting what was placed for callers that have to do something with it.</summary>
    private void SpawnAroundInto(List<Npc>? placed, WorldPosition at, int npcId, int spawnId,
        int count, float range, int liveSeconds, bool untilFightEnds = false)
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
            Track(spawnId, liveSeconds, spawned, untilFightEnds);
            if (placed != null && spawned is Npc npc)
                placed.Add(npc);
        }
    }

    /// <summary>Files a spawn under its spawn id so a later <c>despawn</c> can find it.</summary>
    private void Track(int spawnId, int liveSeconds, VisibleObject? spawned,
        bool untilFightEnds = false)
    {
        if (spawned is not Npc npc)
            return;

        lock (gate)
        {
            spawnsMade++;
            if (untilFightEnds && !ending)
                transientSpawns.Add(npc);
            if (!spawnGroups.TryGetValue(spawnId, out List<Npc>? group))
                spawnGroups[spawnId] = group = new List<Npc>();
            group.Add(npc);
            spawnedThisBranch.Add(npc);

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

    /// <summary>
    /// <c>despawn_by_nameid</c> — remove up to <paramref name="maxCount"/> NPCs of one kind within
    /// <paramref name="radius"/> metres.
    /// </summary>
    /// <remarks>
    /// Retail's verb names its target by client devname; a ported class resolves that to an npc id at
    /// porting time, exactly as it already does for every <c>npc_nameid</c> on a spawn. All three
    /// arguments are retail's own and all three are bounded — across the 5.8 dump the radius runs 2 to
    /// 100 metres and the count 1 to 100 — so this is a local sweep and never a map-wide wipe.
    /// <para>
    /// <b>The owner is not excluded.</b> Retail's element carries no such exemption, and it never
    /// needs one: of 849 uses in the dump, <b>none</b> names the devname of the NPC running it. Left
    /// faithful rather than guarded, with the measurement recorded so the choice is not mistaken for
    /// an oversight.
    /// </para>
    /// <para>
    /// Matches are collected before any are removed. Deleting mid-enumeration would mutate the known
    /// list this is walking.
    /// </para>
    /// </remarks>
    public void DespawnKind(int npcId, float radius, int maxCount)
    {
        if (maxCount <= 0)
            return;

        Npc owner = GetOwner();
        var doomed = new List<Npc>();
        foreach (VisibleObject candidate in NpcMessageBus.Nearby(owner))
        {
            if (doomed.Count >= maxCount)
                break;
            if (candidate is not Npc npc || npc.IsDead() || npc.GetNpcId() != npcId)
                continue;
            if (!PositionUtil.IsInRange(owner, npc, radius))
                continue;

            doomed.Add(npc);
        }

        foreach (Npc npc in doomed)
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
