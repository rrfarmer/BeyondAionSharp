using System.Collections.Concurrent;
using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// An invisible controller that calls a Vritra trooper into the Runatorium. Retail patterns
/// <c>BIDRuneWP_Main_CallVritra*</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Eight of these stand in Infinity Shard
/// (300800000) — they are in our spawn data and were doing nothing, because their template pointed
/// at no AI at all. Each one wakes, puts a trooper on the floor and removes itself two seconds later.
/// <para>
/// <b>The cascade is a weighted pick, not ten rolls.</b> Four of the eight carry ten branches at
/// equal priority, each with its own <c>test_probability</c>, and one unguarded branch beneath them.
/// Retail stops at the first branch whose roll passes, so exactly one trooper appears every time and
/// the unguarded branch is what guarantees it. Reading those as ten independent rolls would have put
/// anywhere from zero to ten troopers on the floor.
/// </para>
/// <para>
/// The other four are a single unguarded branch that spawns three at once — a squad rather than a
/// pick — and one of the three stands five metres from the other two.
/// </para>
/// <para>
/// <b>Not translated: the walk.</b> Retail hands each trooper a <c>pathname</c>
/// (<c>NPCPathVriAss_Path01</c>) so it walks a fixed route from the drop point. Our runtime has no
/// server-path following, so troopers arrive where retail drops them and then behave as their own
/// template says. That is the same gap the audit calls "blocked: waypoint-placed", and it is the
/// only part of this mechanic left out.
/// </para>
/// </remarks>
[AIName("vritra_caller")]
public class VritraCallerAI : PatternAi
{
    /// <summary>Retail's <c>SPAWN_ID_NONE</c>: nothing tracks these, they outlive the caller.</summary>
    private const int Untracked = 0;

    /// <summary>The caller stands two seconds after its call and then removes itself.</summary>
    private const int RetireMillis = 2000;

    private static readonly ConcurrentDictionary<int, AiPattern> ByNpcId = new ConcurrentDictionary<int, AiPattern>();
    private static readonly AiPattern Nothing = new AiPattern();

    private static AiPattern Build(int npcId)
    {
        if (!VritraCallers.ByCaller.TryGetValue(npcId, out VritraCallers.Option[]? options)
            || options.Length == 0)
        {
            return Nothing;
        }

        var branches = new List<PatternBranch>();
        int priority = options.Length;

        foreach (VritraCallers.Option option in options)
        {
            var actions = new List<PatternAction>();
            foreach (VritraCallers.Placement spawn in option.Spawns)
            {
                var spot = new SpawnSpot(spawn.X, spawn.Y, spawn.Z);
                var spots = new SpawnSpot[spawn.Count];
                for (int i = 0; i < spawn.Count; i++)
                    spots[i] = spot;
                actions.Add(Do.SpawnAt(spawn.NpcId, Untracked, 0, spots));
            }

            actions.Add(Do.SetIdleTimer(RetireMillis));

            branches.Add(Branch(priority--, "",
                option.Chance >= 100 ? When.Always : [When.Chance(option.Chance)],
                actions.ToArray()));
        }

        return new AiPattern
        {
            OnWakeUp = Of(branches.ToArray()),

            OnIdleTimer = Of(
                Branch(1, "", When.Always,
                    Do.DespawnSelf())),
        };
    }

    public VritraCallerAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => ByNpcId.GetOrAdd(GetOwner().GetNpcId(), static id => Build(id));
}
