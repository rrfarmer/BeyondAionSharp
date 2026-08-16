using System.Threading.Tasks;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Ai.Pattern;

/// <summary>
/// Retail's <c>attack_target_after_spawn</c> with <c>hatepoints_to_add</c>: a summon that arrives
/// already fighting whoever it was dropped on.
/// </summary>
/// <remarks>
/// 384 spawns across the 5.8 files carry it, and it is the difference between a hazard that lands on
/// a player and one that lands on a player <em>and turns on them</em>. See docs/retail-ai-fidelity.md.
/// <para>
/// It lives outside <see cref="PatternAi"/> because not every NPC that needs it runs a translated
/// pattern — Unstable Yamennes is a Java-parity class with hand-written timers and the same op in its
/// retail pattern.
/// </para>
/// </remarks>
public static class AttackAfterSpawn
{
    /// <summary>Starts the fight one tick from now.</summary>
    /// <remarks>
    /// <b>Deferred, and it has to be for the <c>OBJI_SELF</c> form.</b> Those all sit on
    /// <c>on_wake_up</c>, which runs from inside the owner's own <c>BringIntoWorld</c> — a state flip
    /// made there is overwritten by the rest of that spawn path and the NPC ends up IDLE. Scheduling is
    /// the same answer <c>set_idle_timer</c> gives to a zero delay: next tick, not inline. The other
    /// forms do not need it, and share it anyway so one op has one behaviour.
    /// </remarks>
    public static void NextTick(Npc summon, Creature victim, int hate)
        => ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            Now(summon, victim, hate);
            return ValueTask.CompletedTask;
        }, 0L);

    /// <summary>Puts the two into a fight with each other.</summary>
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
    /// Order matters within each side, and it is the order the test harness uses to start a fight by
    /// hand: the state flip has to land before the hate, or <c>AddHate</c>'s own aggro handling flips it
    /// first and the Attack event no longer takes the path that runs <c>on_enter_attack_state</c>.
    /// </para>
    /// </remarks>
    public static void Now(Npc summon, Creature victim, int hate)
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
}
