namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Source-width dual normal endpoints before per-hand modifiers.
/// Independent of hit status, target state and native memory reconstruction.</summary>
internal static partial class RetailNpcDualNormalPlan
{
    internal readonly record struct Range(ushort Minimum, ushort Maximum);
    internal sealed record Ranges(Range Main, Range Offhand, byte MainHitCount, byte OffhandHitCount)
    {
        internal int PrimaryRecordCount => 2;
        internal int AdditionalRecordCount => 0;
    }

    /// <summary>Receive the final native-width endpoint words before the dual tail.
    /// Each word is converted to binary32, multiplied by0.5, truncated, then copied
    /// to both hands. Attack-profile and attack-effect rates/flats are later per-hand
    /// operands; they must not be applied here and then applied again to each sample.</summary>
    internal static Ranges Split(int minimumBeforeDualSplit, int maximumBeforeDualSplit, int effectiveHitCount)
    {
        if (minimumBeforeDualSplit is < 0 or > ushort.MaxValue
            || maximumBeforeDualSplit is < 0 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(minimumBeforeDualSplit), "Native unsigned-word endpoints required");
        if (effectiveHitCount != 1)
            throw new ArgumentOutOfRangeException(nameof(effectiveHitCount), "Additional-hit selection is outside this complete two-record domain");
        ushort minimum = (ushort)(int)((float)minimumBeforeDualSplit * 0.5f);
        ushort maximum = (ushort)(int)((float)maximumBeforeDualSplit * 0.5f);
        var range = new Range(minimum, maximum);
        return new(range, range, 1, 1);
    }

}
