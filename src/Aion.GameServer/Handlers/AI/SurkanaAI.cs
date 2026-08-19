using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// recieve only 1 dmg with each attack(handled by super) Aggro the whole room on attack.
/// Java parity: ai/instance/dredgion/SurkanaAI (Luzien).
/// </summary>
/// <remarks>
/// Retail-sourced addition; see docs/retail-ai-fidelity.md. <c>Dread_Surkana</c> carries <b>twelve</b>
/// one-shot bands — six on <c>on_attacked</c> and six on <c>on_spelled</c>, at 90, 75, 60, 45, 30 and
/// 15 — and each drops one <c>BIDAb1_Dreadgion_SurkanaNPC</c> beside it. That npc runs <c>NTrap_A</c>,
/// so cracking a surkana is supposed to litter the deck with traps.
/// <para>
/// <b>None of it happened here.</b> The Java class carries the other half of each rung — the room
/// aggro, this port's reading of retail's <c>broadcast_message 6835</c> naming the attacker — and no
/// trap at all.
/// </para>
/// <para>
/// <b>The two sets are separate one-shots and stay separate.</b> Retail gives each band its own flag
/// per handler, so a surkana hit and then cast on at one health drops <i>two</i> traps. Collapsing
/// them into six would be tidier and wrong.
/// </para>
/// <para>
/// <b><see cref="TrapsDropped"/> exists because the traps cannot be counted any other way.</b>
/// <see cref="NTrapAI"/> is "the trap that goes off the moment it appears" — it casts and leaves — so
/// by the time a test looks at the map there is nothing there. An earlier attempt at this work was
/// reverted on the strength of exactly that: the implementation ran correctly and the pin counted
/// survivors, of which there are never any.
/// </para>
/// </remarks>
[AIName("surkana")]
public class SurkanaAI : OneDmgNoActionAI
{
    /// <summary>Retail <c>BIDAb1_Dreadgion_SurkanaNPC</c>, which runs <c>NTrap_A</c>.</summary>
    internal const int Trap = 281287;

    /// <summary>One band: the threshold it opens at, how far the trap lands, how long it lasts.</summary>
    internal readonly record struct Band(int Percent, float Range, int LiveSeconds);

    /// <summary>Retail's <c>on_attacked</c> six. The 60 band differs from its neighbours in both fields.</summary>
    internal static readonly Band[] WhenStruck =
    [
        new Band(90, 2f, 15), new Band(75, 2f, 15), new Band(60, 1f, 20),
        new Band(45, 2f, 15), new Band(30, 2f, 15), new Band(15, 2f, 15),
    ];

    /// <summary>Retail's <c>on_spelled</c> six, which are not the same numbers.</summary>
    internal static readonly Band[] WhenSpelled =
    [
        new Band(90, 1f, 10), new Band(75, 1f, 10), new Band(60, 1f, 10),
        new Band(45, 1f, 10), new Band(30, 1f, 15), new Band(15, 1f, 10),
    ];

    private readonly bool[] struckFired = new bool[WhenStruck.Length];
    private readonly bool[] spelledFired = new bool[WhenSpelled.Length];

    private int trapsDropped;

    /// <summary>How many traps this surkana has laid. For tests; see the class remarks.</summary>
    internal int TrapsDropped => trapsDropped;

    public SurkanaAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        // roomaggro
        CheckForSupport(creature);
        DropTraps(WhenStruck, struckFired);
    }

    protected override void HandleSpelled(Creature caster)
    {
        base.HandleSpelled(caster);
        CheckForSupport(caster);
        DropTraps(WhenSpelled, spelledFired);
    }

    /// <summary>Opens every band this hit has crossed, each once.</summary>
    /// <remarks>
    /// A single blow can cross more than one threshold, and retail's rungs are independent one-shots
    /// rather than a ladder, so each unfired band below the current health opens on the same hit.
    /// </remarks>
    private void DropTraps(Band[] bands, bool[] fired)
    {
        int hp = GetLifeStats().GetHpPercentage();
        for (int i = 0; i < bands.Length; i++)
        {
            if (fired[i] || hp >= bands[i].Percent)
                continue;

            fired[i] = true;
            trapsDropped++;
            Expire(RndSpawnInRange(Trap, bands[i].Range), bands[i].LiveSeconds);
        }
    }

    private void CheckForSupport(Creature creature)
    {
        GetKnownList().ForEachNpc(npc =>
        {
            if (!npc.IsDead() && IsInRange(npc, 25))
                npc.GetAi().OnCreatureEvent(AiEventType.CREATURE_AGGRO, creature);
        });
    }
}
