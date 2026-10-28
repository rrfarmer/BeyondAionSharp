using System.Text.Json;

namespace Aion.GameServer.Retail;

// Ordinary combat inputs share the profile publication. Optional skill readers
// extend the same package without owning this field or a second active state.
internal static partial class RetailNpcStatCatalog
{
    private sealed partial record Package
    {
        internal RetailNpcStatPveInputs? CombatPve { get; init; }
    }

    internal static RetailNpcStatPveInputs? CombatPve => Volatile.Read(ref active)?.CombatPve;

    static partial void ReadNormalPackage(JsonElement root, string directory, ref Package package)
    {
        var inputs = root.TryGetProperty("combat_stats", out var context)
            ? RetailNpcStatPveInputs.Parse(ReadAsset(context, directory), context.GetProperty("sha256").GetString()!)
            : null;
        package = package with { CombatPve = inputs };
    }
}
