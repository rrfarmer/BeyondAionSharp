using System.Runtime.CompilerServices;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>
/// Arithmetic for the ordinary NPCServer minimum-reach weighted pool.
/// Inputs have already passed the original pending-skill/eligibility checks and belong to
/// the automatic branch (raw param6 == 0). This type neither proves those facts nor calls
/// skills, geometry, clocks or RNG. It does not select a final action or emit an attack.
/// </summary>
/// <remarks>
/// Pinned NPC::ChooseAttackType 0x14011c680: reach operations at 0x14011cd20,
/// tie constants at 0x14011cdb4/0x14011cdbc, int32 ADD at 0x14011cf1b,
/// range request and post-draw threshold walk at 0x14011cf32 onward.
/// Negative weights, signed cumulative overflow and nonfinite geometry are unsupported,
/// rather than approximated as native fallback results. The normal adapter uses
/// this pool for source attack-entry selection; this type performs no routing.
/// </remarks>
internal sealed class RetailNpcMinimumReachPool
{
    internal const string NpcServerBinarySha256 =
        "c24e600107352268d377c9a7361e52d848e96af8deff61259e031b98b7130350";

    // Disk-verified binary32 constants, not double literals or inferred tolerances.
    private static readonly float RangeTolerance = BitConverter.Int32BitsToSingle(0x3c23d70a);
    private static readonly float TieTolerance = BitConverter.Int32BitsToSingle(0x38d1b717);
    private static readonly float NegativeTieTolerance = BitConverter.Int32BitsToSingle(unchecked((int)0xb8d1b717));

    /// <summary>
    /// Actual effective radii and the original binary32 center-distance-squared value.
    /// OriginalRangeBypass is the supplied original branch result, not a geometry query.
    /// The caller owns the distance calculation and the evidence for this snapshot.
    /// </summary>
    internal readonly record struct EffectiveGeometry(float TargetBodyRadius, float NpcBodyRadius,
        float CenterDistanceSquared, bool OriginalRangeBypass);

    /// <summary>Original combined-list index, loaded entry range/weight and its source identity.</summary>
    internal readonly record struct EligibleAttack(int CombinedIndex, float EntryRange, int Weight,
        string SourceEvidence);

    internal readonly record struct PoolEntry(int CombinedIndex, float EntryRange, int Weight,
        string SourceEvidence, float EffectiveReachSquared, int CumulativeThreshold);

    /// <summary>
    /// Receipt of one original FUN_140336f10 wrapper invocation, including equal endpoints.
    /// AdapterInvoked means the unequal-endpoint call to FUN_1401441c0, not a count of
    /// underlying RNG words. These declarations are validated, not runtime-attested here.
    /// </summary>
    internal readonly record struct OriginalDrawReceipt(int Minimum, int Maximum, int Result,
        bool AdapterInvoked, string SourceEvidence);

    /// <summary>
    /// All ordered thresholds satisfied by the same original draw. The original adapter
    /// must still perform facing/geometry checks in order and can return a negative result.
    /// In particular, these entries are not a list of successful attacks.
    /// </summary>
    internal sealed class ThresholdCandidates
    {
        internal OriginalDrawReceipt Receipt { get; }
        internal IReadOnlyList<PoolEntry> Entries { get; }

        internal ThresholdCandidates(OriginalDrawReceipt receipt, PoolEntry[] entries)
        {
            Receipt = receipt;
            Entries = Array.AsReadOnly(entries);
        }
    }

    internal string SourceEvidence { get; }
    internal EffectiveGeometry Geometry { get; }
    internal IReadOnlyList<PoolEntry> Entries { get; }
    internal float? MinimumReachSquared { get; }
    internal float? MaximumEligibleReachSquared { get; }
    internal int TotalWeight { get; }
    internal bool RequiresOriginalRangeWrapper => Entries.Count != 0;
    internal bool RequiresInclusiveAdapter => TotalWeight > 1;

    private RetailNpcMinimumReachPool(string sourceEvidence, EffectiveGeometry geometry,
        PoolEntry[] entries, float? minimumReachSquared, float? maximumEligibleReachSquared, int totalWeight)
    {
        SourceEvidence = sourceEvidence;
        Geometry = geometry;
        Entries = Array.AsReadOnly(entries);
        MinimumReachSquared = minimumReachSquared;
        MaximumEligibleReachSquared = maximumEligibleReachSquared;
        TotalWeight = totalWeight;
    }

    /// <summary>
    /// Preserve source order without sorting, truncating to 23, or reindexing after filters.
    /// Zero weights are omitted as in the original branch. All supplied indices must be
    /// unique, ascending and within the native first 23 combined entries (0..22).
    /// Empty/out-of-range pools have no wrapper request; they do not imply a native error code.
    /// </summary>
    internal static RetailNpcMinimumReachPool Prepare(RetailNpcNormalScope scope,
        string sourceNpcSha256, string sourceEvidence, EffectiveGeometry geometry,
        IReadOnlyList<EligibleAttack> alreadyEligibleEntries)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.RequireNpcAttacker();
        if (!string.Equals(sourceNpcSha256, NpcServerBinarySha256, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Unsupported or unverified NPCServer identity.", nameof(sourceNpcSha256));
        RequireEvidence(sourceEvidence, nameof(sourceEvidence));
        ArgumentNullException.ThrowIfNull(alreadyEligibleEntries);
        if (alreadyEligibleEntries.Count > 23)
            throw new ArgumentOutOfRangeException(nameof(alreadyEligibleEntries), "Supply only eligible entries from the original first 23, without truncation.");
        RequireNonnegativeFinite(geometry.TargetBodyRadius, nameof(geometry.TargetBodyRadius));
        RequireNonnegativeFinite(geometry.NpcBodyRadius, nameof(geometry.NpcBodyRadius));
        RequireNonnegativeFinite(geometry.CenterDistanceSquared, nameof(geometry.CenterDistanceSquared));

        if (alreadyEligibleEntries.Count == 0)
            return new RetailNpcMinimumReachPool(sourceEvidence, geometry, [], null, null, 0);

        // XMM11 holds this separately rounded sum before adding each entry's range.
        float radii = Add(geometry.TargetBodyRadius, geometry.NpcBodyRadius);
        var inRange = new List<(EligibleAttack Attack, float ReachSquared)>();
        float? minimum = null;
        float? maximum = null;
        int previousIndex = -1;
        foreach (EligibleAttack attack in alreadyEligibleEntries)
        {
            if (attack.CombinedIndex < 0 || attack.CombinedIndex >= 23 || attack.CombinedIndex <= previousIndex)
                throw new ArgumentException("Combined indices must be original, strictly ascending values in 0..22.", nameof(alreadyEligibleEntries));
            previousIndex = attack.CombinedIndex;
            RequireEvidence(attack.SourceEvidence, nameof(attack.SourceEvidence));
            RequireNonnegativeFinite(attack.EntryRange, nameof(attack.EntryRange));
            if (attack.Weight < 0)
                throw new ArgumentOutOfRangeException(nameof(attack.Weight), attack.Weight, "Negative native weights are outside this supported domain.");
            if (attack.Weight == 0)
                continue;

            float reach = Add(radii, attack.EntryRange);
            if (reach <= 2f)
                reach = 2f;
            float squared = Multiply(reach, reach);
            if (!maximum.HasValue || maximum.Value < squared)
                maximum = squared;
            // Native order calculates this addition even when the supplied bypass is true.
            float permittedDistance = Add(squared, RangeTolerance);
            if (geometry.CenterDistanceSquared <= permittedDistance || geometry.OriginalRangeBypass)
            {
                inRange.Add((attack, squared));
                if (!minimum.HasValue || squared < minimum.Value)
                    minimum = squared;
            }
        }

        var pool = new List<PoolEntry>();
        int total = 0;
        foreach (var candidate in inRange)
        {
            float difference = Subtract(minimum!.Value, candidate.ReachSquared);
            if (!(difference < TieTolerance && NegativeTieTolerance < difference))
                continue;
            // ADD R10D is wrapping int32 arithmetic. The wrapped fallback/threshold domain
            // is not implemented: reject it visibly instead of returning a guessed action.
            int next = unchecked(total + candidate.Attack.Weight);
            if (next < total)
                throw new ArithmeticException($"Cumulative native weight would wrap int32 to {next}; this domain is unsupported.");
            total = next;
            EligibleAttack attack = candidate.Attack;
            pool.Add(new PoolEntry(attack.CombinedIndex, attack.EntryRange, attack.Weight,
                attack.SourceEvidence, candidate.ReachSquared, total));
        }
        return new RetailNpcMinimumReachPool(sourceEvidence, geometry, pool.ToArray(), minimum, maximum, total);
    }

    /// <summary>
    /// Validate one original inclusive wrapper receipt, then retain every ordered threshold
    /// at least its result. This preserves potential geometry-failure fallthrough without
    /// performing those checks, rerolling, renormalizing, or claiming final action selection.
    /// Even weight 1 requires a wrapper receipt; its inclusive adapter must not be invoked.
    /// </summary>
    internal ThresholdCandidates EnumerateSatisfiedThresholds(OriginalDrawReceipt originalDraw)
    {
        if (!RequiresOriginalRangeWrapper)
            throw new InvalidOperationException("An empty pool has no original weighted range request.");
        RequireEvidence(originalDraw.SourceEvidence, nameof(originalDraw.SourceEvidence));
        if (originalDraw.Minimum != 1 || originalDraw.Maximum != TotalWeight)
            throw new ArgumentException("The receipt must identify this pool's original inclusive request 1..TotalWeight.", nameof(originalDraw));
        if (originalDraw.AdapterInvoked != RequiresInclusiveAdapter)
            throw new ArgumentException("Inclusive adapter invocation must match the original unequal-endpoint branch.", nameof(originalDraw));
        if (originalDraw.Result < 1 || originalDraw.Result > TotalWeight)
            throw new ArgumentOutOfRangeException(nameof(originalDraw), originalDraw.Result, "The original result is outside the requested inclusive range.");

        var candidates = new List<PoolEntry>();
        foreach (PoolEntry entry in Entries)
            if (originalDraw.Result <= entry.CumulativeThreshold)
                candidates.Add(entry);
        return new ThresholdCandidates(originalDraw, candidates.ToArray());
    }

    private static void RequireEvidence(string evidence, string name)
    {
        if (string.IsNullOrWhiteSpace(evidence))
            throw new ArgumentException("Explicit source/receipt evidence is required; no provenance default is supplied.", name);
    }

    private static void RequireNonnegativeFinite(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0f)
            throw new ArgumentOutOfRangeException(name, value, "Only finite nonnegative effective geometry is supported.");
    }

    private static float FiniteResult(float value)
    {
        if (!float.IsFinite(value))
            throw new ArithmeticException("An intermediate exceeds the supported finite binary32 domain.");
        return value;
    }

    // Separate scalar operations preserve SSE rounding points and prevent reassociation/FMA.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Add(float left, float right) => FiniteResult(left + right);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Subtract(float left, float right) => FiniteResult(left - right);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Multiply(float left, float right) => FiniteResult(left * right);
}
