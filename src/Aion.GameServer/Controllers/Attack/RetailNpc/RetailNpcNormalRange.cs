namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>
/// Complete range-request contract for the unsigned-word ordinary NPC normal base.
/// FUN_140df1660 orders endpoints, returns a singleton without RNG and otherwise requests
/// an inclusive range. This is not the RNG kernel, a sampler, or an attack selector.
/// Wider/signed ranges and weapon-booster endpoint adjustments are outside this type.
/// </summary>
internal sealed class RetailNpcNormalRange
{
    internal ushort Minimum { get; }
    internal ushort Maximum { get; }
    internal uint InclusiveCount => (uint)Maximum - Minimum + 1u;
    internal bool RequiresRandomDraw => Minimum != Maximum;

    private RetailNpcNormalRange(ushort first, ushort second)
    {
        Minimum = Math.Min(first, second);
        Maximum = Math.Max(first, second);
    }

    internal static RetailNpcNormalRange Create(RetailNpcNormalScope scope, ushort first, ushort second)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.RequireNpcAttacker();
        return new RetailNpcNormalRange(first, second);
    }

    /// <summary>
    /// Accept the original range result, without drawing again. Null is required for a
    /// singleton and forbidden otherwise. Validation does not prove distribution/state parity.
    /// </summary>
    internal int ResolveOriginalSample(int? originalSample)
    {
        if (!RequiresRandomDraw)
        {
            if (originalSample.HasValue)
                throw new ArgumentException("A singleton range must not consume a random draw.", nameof(originalSample));
            return Minimum;
        }
        if (!originalSample.HasValue)
            throw new ArgumentException("The original inclusive range result is required.", nameof(originalSample));
        if (originalSample.Value < Minimum || originalSample.Value > Maximum)
            throw new ArgumentOutOfRangeException(nameof(originalSample), originalSample, "Result is outside the requested range.");
        return originalSample.Value;
    }
}
