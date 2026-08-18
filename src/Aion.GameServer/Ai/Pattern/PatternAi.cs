using System;
using System.Linq;
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
            Evaluate(Pattern.OnEnterAttack);

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
        Evaluate(Pattern.OnDespawn);
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
    public void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
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

    /// <summary>Whoever landed the blow being handled, or null outside an <c>on_attacked</c> branch.</summary>
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

    /// <summary>Retail's <c>on_spelled</c>.</summary>
    /// <remarks>
    /// Guarded against re-entrancy for the same reason <c>on_attacked</c> is: a branch that adds hate
    /// notifies the controller, and the controller can come straight back through here.
    /// </remarks>
    protected override void HandleSpelled(Creature caster)
    {
        base.HandleSpelled(caster);
        if (inOnSpelled || Pattern.OnSpelled.Length == 0)
            return;

        inOnSpelled = true;
        LastCaster = caster;
        try
        {
            Evaluate(Pattern.OnSpelled);
        }
        finally
        {
            LastCaster = null;
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
    public void SpawnOnAttacker(AggroTarget which, int npcId, int spawnId, float range, int liveSeconds,
        int attackHate = 0)
    {
        Creature? target = GetAggroList().GetTarget(which);
        if (target == null)
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
    public void SpawnOffset(int npcId, int spawnId, float dx, float dy, int liveSeconds, float dz = 0f)
    {
        WorldPosition at = GetPosition();
        Track(spawnId, liveSeconds,
            Spawn(npcId, at.GetX() + dx, at.GetY() + dy, at.GetZ() + dz, (sbyte)at.GetHeading()));
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
