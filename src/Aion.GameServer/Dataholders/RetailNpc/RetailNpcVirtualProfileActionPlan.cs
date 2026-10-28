using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Numerics;
using static Aion.GameServer.Dataholders.RetailNpc.RetailNpcVirtualGuard;

namespace Aion.GameServer.Dataholders.RetailNpc;

public sealed record RetailNpcVirtualMainActionReceipt(RetailNpcTemplateSourceReceipt Source,
    string PackageContextSha256, string ListSha256, string RecordSha256, int RecordBytes, int SelectedOrdinal,
    string SelectionIdentity, string Epoch, RetailNpcModifierInputOrigin Origin, string? ActorObjectIdentity, string Evidence);
public sealed record RetailNpcVirtualActionContext(RetailNpcVirtualContext ExpectedConstruction,
    RetailNpcVirtualMainActionReceipt ExpectedAction, uint? CurrentPhysicalFlag8e4,
    bool? Binary32NearestEven, bool? MaskedFloatingExceptions, bool? ConsumedInstructionsAvailable, bool? NormalReturn);
public sealed record RetailNpcVirtualFloatStep(string Operation, uint Bits);

/// <summary>Complete selected ChangeAttackParamater value/write plan; no selection inference or mutation.</summary>
public sealed class RetailNpcVirtualProfileActionPlan
{
    private RetailNpcVirtualProfileActionPlan(RetailNpcVirtualConstructionPlan construction, RetailNpcVirtualMainActionReceipt receipt,
        RetailNpcVirtualActionContext context, byte[] image, IDictionary<string, uint> writes, IEnumerable<RetailNpcVirtualFloatStep> steps)
    { Construction = construction; Receipt = receipt; Context = context; RecordBytes = image.ToImmutableArray();
        Writes = writes.ToFrozenDictionary(StringComparer.Ordinal); FloatSteps = steps.ToImmutableArray();
        WriteWidths = writes.Keys.ToFrozenDictionary(x => x, x => x is "0x14b8" or "0x14ba" ? 16 : 32, StringComparer.Ordinal); }
    public RetailNpcVirtualConstructionPlan Construction { get; }
    public RetailNpcVirtualMainActionReceipt Receipt { get; }
    public RetailNpcVirtualActionContext Context { get; }
    public ImmutableArray<byte> RecordBytes { get; }
    public FrozenDictionary<string, uint> Writes { get; }
    public FrozenDictionary<string, int> WriteWidths { get; }
    public ImmutableArray<RetailNpcVirtualFloatStep> FloatSteps { get; }
    public uint RangeBits => Writes["0x910"];
    public bool RuntimeObservationVerified => false;
    public bool ActivationReady => false;

    public static RetailNpcVirtualResult<RetailNpcVirtualProfileActionPlan> PlanSelectedAction(
        RetailNpcVirtualConstructionPlan construction, ReadOnlySpan<byte> actualMainDescriptor,
        RetailNpcVirtualMainActionReceipt receipt, RetailNpcVirtualActionContext context)
    {
        ArgumentNullException.ThrowIfNull(construction); ArgumentNullException.ThrowIfNull(receipt); ArgumentNullException.ThrowIfNull(context);
        try
        {
            RetailNpcVirtualProfileConstruction.Validate(construction.Source, construction.RatioTable, construction.Selection, construction.Effects, context.ExpectedConstruction);
            Need(construction.Context == context.ExpectedConstruction && context.ExpectedAction == receipt, "virtual_action_context", "Complete construction/current action pin differs");
            Hash(receipt.ListSha256); Hash(receipt.RecordSha256);
            var expected = construction.Context;
            Need(receipt.Source == construction.Source.Receipt && receipt.PackageContextSha256 == expected.ExpectedPackageContextSha256
                && receipt.Epoch == expected.ExpectedEpoch && receipt.Origin == expected.ExpectedOrigin && receipt.ActorObjectIdentity == expected.ExpectedActorObjectIdentity,
                "virtual_action_epoch", "Selected Main descriptor/source/epoch/origin/actor context differs");
            Need(Text(receipt.SelectionIdentity, 4096) && Text(receipt.Evidence, 4096) && receipt.SelectedOrdinal is >= 0 and < 4096,
                "virtual_action_selection", "Independent Main list/index/selection evidence required; NPC XML is insufficient");
            Need(actualMainDescriptor.Length is >= 24 and <= 65536 && receipt.RecordBytes == actualMainDescriptor.Length,
                "virtual_action_image", "Whole Main record extent differs");
            byte[] image = actualMainDescriptor.ToArray();
            Need(RetailNpcVirtualProfileTable.Digest(image) == receipt.RecordSha256, "virtual_action_image", "Owned whole Main record hash differs");
            Need(context.CurrentPhysicalFlag8e4.HasValue, "virtual_action_flag", "Current weapon flag must be explicitly known");
            Need(context is { Binary32NearestEven: true, MaskedFloatingExceptions: true, ConsumedInstructionsAvailable: true, NormalReturn: true },
                "virtual_action_arithmetic", "Explicit RNE/masked/ISA/normal-return mathematical conditions required");
            uint sourceRange = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(4, 4));
            F32.Check(sourceRange); Need(F32.Compare(sourceRange, F32.Integer(-65535)) >= 0 && F32.Compare(sourceRange, F32.Integer(65535)) <= 0,
                "virtual_action_range", "Source range outside bounded finite projection domain");
            int control = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(0x14, 4));
            var steps = new List<RetailNpcVirtualFloatStep>();
            uint Step(string op, uint bits) { F32.Check(bits); steps.Add(new(op, bits)); return bits; }
            uint multiplier = Step("signed32-control-to-f32", F32.Integer(control));
            multiplier = Step("control-times-original-0.01", F32.Multiply(multiplier, 0x3c23d70a));
            bool physical = context.CurrentPhysicalFlag8e4.GetValueOrDefault() != 0;
            var crit = construction.Effects.Pairs[physical ? 25 : 27];
            uint critConverted = Step("crit-percent-to-f32", F32.Integer(crit.Percent));
            uint critCoefficient = Step("crit-percent-divide100", F32.Divide(critConverted, 0x42c80000));
            uint result = ComputeRangeStage(construction.Selection.Descriptor.Pairs[0x70], sourceRange, construction.Effects.Pairs[38], steps);
            var writes = new Dictionary<string, uint>(StringComparer.Ordinal)
            {
                ["0x14d0"]=0,["0x8d4"]=multiplier,["0x8d8"]=multiplier,
                ["0x14b8"]=checked((uint)construction.Scalars[physical ? "critical" : "magical_critical"].Value),["0x14ba"]=0,
                ["0x8e0"]=critCoefficient,["0x8dc"]=critCoefficient,["0x14d8"]=unchecked((uint)crit.Flat),["0x14d4"]=unchecked((uint)crit.Flat),["0x910"]=result
            };
            return new(new(construction, receipt, context, image, writes, steps), []);
        }
        catch (Exception e) when (e is RetailNpcVirtualError or InvalidOperationException or ArgumentException or OverflowException)
        { return Failure<RetailNpcVirtualProfileActionPlan>(e); }
    }

    // Internal arithmetic stage also admits explicit mathematical pair oracles whose
    // two nonzero components cannot be produced by the strict XML replacement parser.
    internal static uint ComputeRangeStage(RetailNpcVirtualPair range, uint sourceRange,
        RetailNpcModifierPairInput effects, List<RetailNpcVirtualFloatStep> steps)
    {
            F32.Check(sourceRange);
            Need(F32.Compare(sourceRange, F32.Integer(-65535)) >= 0 && F32.Compare(sourceRange, F32.Integer(65535)) <= 0,
                "virtual_action_range", "Source range outside bounded finite projection domain");
            uint Step(string op, uint bits) { F32.Check(bits); steps.Add(new(op, bits)); return bits; }
            uint pct = Step("profile-percent-to-f32", F32.Integer(range.Percent));
            uint flat = Step("profile-flat-to-f32", F32.Integer(range.Flat));
            uint rangeFactor = Step("profile-percent-times-original-0.01", F32.Multiply(pct, 0x3c23d70a));
            uint rangeProduct = Step("profile-range-product", F32.Multiply(rangeFactor, sourceRange));
            uint baseline = Step("profile-range-flat-add", F32.Add(rangeProduct, flat));
            uint ep = Step("range-effect-percent-to-f32", F32.Integer(effects.Percent));
            uint ef = Step("range-effect-flat-to-f32", F32.Integer(effects.Flat));
            uint product = Step("range-effect-product", F32.Multiply(ep, baseline));
            uint contribution = Step("range-effect-times-original-0.01", F32.Multiply(product, 0x3c23d70a));
            uint added = Step("range-effect-flat-add", F32.Add(contribution, ef));
            uint total = Step("range-baseline-final-add", F32.Add(added, baseline));
            uint result = F32.Compare(total, 0) < 0 ? 0 : F32.Compare(total, 0x437f0000) > 0 ? 0x437f0000 : total;
            Step("range-clamp0..255", result);
            return result;
    }

    // Small exact rational RNE machinery, solely for bounded int conversion/add/mul/div100
    // in the fully captured action body. Nonfinite and consumed subnormal states reject.
    // No host cosine/FP mode/MXCSR or general-purpose floating-point API is used.
    private static class F32
    {
        private readonly record struct Q(BigInteger N, BigInteger D);
        internal static void Check(uint bits) => Need((bits & 0x7fffffffu) < 0x7f800000u
            && ((bits & 0x7fffffffu) == 0 || (bits & 0x7fffffffu) >= 0x00800000u),
            "virtual_action_arithmetic", "Consumed nonfinite/subnormal state is outside the admitted arithmetic context");
        private static Q Value(uint bits)
        {
            Check(bits); int e = (int)(bits >> 23 & 255), sig = (int)(bits & 0x7fffff);
            if (e != 0) sig |= 1 << 23;
            int power = e == 0 ? -149 : e - 127 - 23; BigInteger n = sig, d = BigInteger.One;
            if (power >= 0) n <<= power; else d <<= -power;
            if ((bits & 0x80000000u) != 0) n = -n; return new(n, d);
        }
        internal static uint Integer(int n) => Round(new(n, BigInteger.One));
        internal static int Compare(uint a, uint b) { var x = Value(a); var y = Value(b); return (x.N * y.D).CompareTo(y.N * x.D); }
        internal static uint Multiply(uint a, uint b)
        {
            var x = Value(a); var y = Value(b); if (x.N.IsZero || y.N.IsZero) return (a ^ b) & 0x80000000u;
            return Round(new(x.N * y.N, x.D * y.D));
        }
        internal static uint Add(uint a, uint b)
        {
            var x = Value(a); var y = Value(b); BigInteger n = x.N * y.D + y.N * x.D;
            if (n.IsZero) return (a & b) & 0x80000000u;
            return Round(new(n, x.D * y.D));
        }
        internal static uint Divide(uint a, uint b)
        {
            var x = Value(a); var y = Value(b); Need(!y.N.IsZero, "virtual_action_arithmetic", "Zero divisor outside action contract");
            if (x.N.IsZero) return (a ^ b) & 0x80000000u;
            return Round(new(x.N * y.D * y.N.Sign, x.D * BigInteger.Abs(y.N)));
        }
        private static BigInteger Even(BigInteger n, BigInteger d)
        { var q = BigInteger.DivRem(n, d, out var r); return r * 2 > d || r * 2 == d && !q.IsEven ? q + 1 : q; }
        private static uint Round(Q q)
        {
            if (q.N.IsZero) return 0; uint sign = q.N.Sign < 0 ? 0x80000000u : 0;
            BigInteger n = BigInteger.Abs(q.N), d = q.D; int e = checked((int)(n.GetBitLength() - d.GetBitLength()));
            if (e >= 0 ? n < (d << e) : (n << -e) < d) e--;
            BigInteger significand;
            if (e < -126)
            { significand = Even(n << 149, d); uint bits = sign | checked((uint)significand); Check(bits); return bits; }
            Need(e <= 127, "virtual_action_arithmetic", "Finite bounded-stage overflow"); int shift = 23 - e;
            significand = shift >= 0 ? Even(n << shift, d) : Even(n, d << -shift);
            if (significand == (BigInteger.One << 24)) { e++; significand >>= 1; }
            Need(e <= 127, "virtual_action_arithmetic", "Finite rounded-stage overflow");
            uint result = sign | (uint)(e + 127) << 23 | (checked((uint)significand) - 0x800000u); Check(result); return result;
        }
    }
}
