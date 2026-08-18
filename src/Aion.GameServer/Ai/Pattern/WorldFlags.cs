using System.Runtime.CompilerServices;
using Aion.GameServer.World;

namespace Aion.GameServer.Ai.Pattern;

/// <summary>
/// Retail's <c>set_world_flag_var</c> family: flags shared by every npc in one map instance, rather than
/// held by a single npc the way <see cref="PatternAi.TestAndSetFlag"/> holds them.
/// </summary>
/// <remarks>
/// <b>The difference is the whole point of them.</b> A per-npc flag lets a branch run once for that npc;
/// a world flag lets <em>one npc arm a step that a different npc later takes</em>. The Theobomos Lab hall
/// is the clearest case in the data: the sealed akaimum sets a world flag when it stands a fallen guard
/// back up, and the silikor consumes that same flag when a neutral caster spells it, which is what makes
/// dismissing the akaimum a reward for having fought the hall instead of something available at the door.
/// <para>
/// <b>Scope is the map instance, not the server.</b> Retail calls them "world" flags, but the content
/// that uses them is instanced — the Lab, Tiamat's lair, the Rune hall, Idgel Dome — and one group's
/// progress must not arm another group's mechanic. For non-instanced maps this is the shared instance, so
/// the flag is genuinely map-wide there, which is the same thing retail means.
/// </para>
/// <para>
/// <b>Slots are the same 32 as the per-npc flags</b>, and deliberately a separate space: retail's
/// <c>FLAGVARI_ALPHA_1</c> as a world flag and as a per-npc flag are different variables, and several
/// patterns use both names in one handler.
/// </para>
/// </remarks>
public static class WorldFlags
{
    /// <summary>Matches <see cref="PatternAi"/>'s per-npc flag count.</summary>
    public const int Slots = 32;

    /// <summary>
    /// Per map instance, one flag word. Keyed by the instance object itself so that an instance which is
    /// destroyed and rebuilt under the same id does not inherit the old group's progress.
    /// </summary>
    /// <remarks>
    /// A <see cref="ConditionalWeakTable{TKey,TValue}"/> rather than a dictionary keyed by id: instances
    /// are created and torn down constantly, and nothing here should keep one alive or need a hook to
    /// clean up after it.
    /// </remarks>
    private static readonly ConditionalWeakTable<WorldMapInstance, bool[]> Store = new();

    private static bool[] For(WorldMapInstance instance) => Store.GetValue(instance, _ => new bool[Slots]);

    /// <summary>Test-and-set, shared across the instance. True only for the first npc to reach it.</summary>
    public static bool TestAndSet(WorldMapInstance instance, int flag)
    {
        bool[] flags = For(instance);
        lock (flags)
        {
            if (flags[flag])
                return false;
            flags[flag] = true;
            return true;
        }
    }

    /// <summary>Test-and-unset: true only while the flag is set, clearing it for everyone.</summary>
    public static bool TestAndUnset(WorldMapInstance instance, int flag)
    {
        bool[] flags = For(instance);
        lock (flags)
        {
            if (!flags[flag])
                return false;
            flags[flag] = false;
            return true;
        }
    }

    /// <summary>Retail's <c>is_world_flag_var</c>: reads without touching.</summary>
    public static bool IsSet(WorldMapInstance instance, int flag)
    {
        bool[] flags = For(instance);
        lock (flags)
            return flags[flag];
    }

    /// <summary>
    /// Drops every world flag in one instance. For tests, and for any instance reset that reuses the same
    /// instance object rather than building a new one.
    /// </summary>
    public static void Clear(WorldMapInstance instance)
    {
        bool[] flags = For(instance);
        lock (flags)
            Array.Clear(flags);
    }
}
