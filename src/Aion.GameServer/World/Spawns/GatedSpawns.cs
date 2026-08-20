using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns;

namespace Aion.GameServer.World.Spawns;

/// <summary>One placement retail keeps behind a condition.</summary>
/// <param name="NpcId">What to place.</param>
/// <param name="X">Retail's absolute position; these are world coordinates, not offsets.</param>
/// <param name="RespawnSeconds">Retail's <c>spawn_time</c>, zero for a one-shot.</param>
/// <param name="DespawnAtOther">
/// Retail's <c>despawnAtOther</c>. <b>False does not mean "stays forever" by accident</b> — it is the
/// difference between a group that appears once a condition is met and one that tracks the condition
/// both ways, and roughly a third of the gated groups in the dump are the first kind.
/// </param>
/// <param name="Gate">The condition, already parsed.</param>
public sealed record GatedSpawn(
    int NpcId, float X, float Y, float Z, byte Heading, int RespawnSeconds,
    bool DespawnAtOther, SpawnCondition Gate);

/// <summary>
/// Puts a map's conditional spawn groups in and takes them out again as their gates move.
/// </summary>
/// <remarks>
/// The last structural piece of the conditional spawn engine. <see cref="SpawnCondition"/> reads the
/// gates, <see cref="SpawnVariables"/> holds the counters, <c>PatternAi.SetSpawnVariable</c> lets a
/// pattern move one — and this is what any of that is <i>for</i>: retail hides 78,865 npc placements
/// behind those gates, 25,012 of which this port has templates for and never places.
/// <para>
/// <b>Only the groups that could have changed are re-checked.</b> A gate names the variables it reads,
/// so a write to <c>N_WAVE_01</c> re-evaluates the gates that mention it and leaves the other thousands
/// alone. Without that, every counter tick in a fortress would walk every gate in the world.
/// </para>
/// </remarks>
public sealed class GatedSpawnController : IDisposable
{
    private readonly int mapId;
    private readonly int instanceId;
    private readonly SpawnVariables variables;
    private readonly List<GatedSpawn> gated;
    private readonly Dictionary<GatedSpawn, VisibleObject> placed = new();
    private readonly Lock gate = new();

    public GatedSpawnController(int mapId, int instanceId, SpawnVariables variables,
        IEnumerable<GatedSpawn> groups)
    {
        ArgumentNullException.ThrowIfNull(variables);
        ArgumentNullException.ThrowIfNull(groups);

        this.mapId = mapId;
        this.instanceId = instanceId;
        this.variables = variables;
        gated = groups.ToList();
        variables.Changed += OnVariableChanged;
    }

    /// <summary>
    /// How many gates have been evaluated since this controller was made.
    /// </summary>
    /// <remarks>
    /// Exposed because the selectivity is a real property and not an incidental one: a fortress counter
    /// ticking must not walk every gate in the world, and nothing else about the controller's behaviour
    /// distinguishes "re-checked the one gate that reads this" from "re-checked all of them".
    /// </remarks>
    public long Evaluations { get; private set; }

    /// <summary>What is in the world right now because its gate holds.</summary>
    public int Placed
    {
        get
        {
            lock (gate)
                return placed.Count;
        }
    }

    /// <summary>Evaluates every gate and makes the world match.</summary>
    public void Refresh() => Apply(gated);

    private void OnVariableChanged(string name, int value)
        => Apply(gated.Where(g => g.Gate.Variables.Contains(name)).ToList());

    private void Apply(IReadOnlyCollection<GatedSpawn> subset)
    {
        if (subset.Count == 0)
            return;

        IReadOnlyDictionary<string, int> values = variables.Snapshot();
        lock (gate)
        {
            foreach (GatedSpawn group in subset)
            {
                Evaluations++;
                bool holds = group.Gate.Holds(values);
                bool here = placed.ContainsKey(group);

                if (holds && !here)
                {
                    SpawnTemplate template = SpawnEngine.SpawnEngine.NewSpawn(
                        mapId, group.NpcId, group.X, group.Y, group.Z, group.Heading,
                        group.RespawnSeconds);
                    if (SpawnEngine.SpawnEngine.SpawnObject(template, instanceId) is VisibleObject spawned)
                        placed[group] = spawned;
                }
                else if (!holds && here && group.DespawnAtOther)
                {
                    // Only when retail says so. A group without the flag is placed once its condition
                    // is met and stays; taking it away anyway would be a mechanic retail does not have.
                    placed[group].GetController().Delete();
                    placed.Remove(group);
                }
            }
        }
    }

    public void Dispose() => variables.Changed -= OnVariableChanged;
}
