namespace Aion.GameServer.GeoEngine.RetailPath;

public sealed record RetailCollisionIntersection(RetailCollisionStatus Status, bool? Hit, float? Parameter, string? Detail)
{
    public bool RuntimeVerified => false;
}
public sealed record RetailCollisionEligibility(RetailCollisionStatus Status, bool? Eligible, bool CosineEvidenceConsumed, string? Detail);

/// <summary>Entire pinned triangle/normal/preprocess primitives. No geometry callback, actor or native executable is invoked.</summary>
public static class RetailCollisionMath
{
    public const uint NativeCosineArgumentBits = 0x3f490fdb;
    public const string NativeCosineFunction = "0x14039df80";

    public static RetailCollisionIntersection IntersectTriangle(RetailCollisionTriangle triangle,
        RetailSearchPoint origin, RetailSearchPoint delta, bool ContiguousNativeVariant)
    {
        try
        {
            bool hit = Intersect(triangle.A, triangle.B, triangle.C, origin, delta, ContiguousNativeVariant, out float parameter);
            return new(RetailCollisionStatus.Computed, hit, hit ? parameter : null, null);
        }
        catch (RetailCollisionStop stop) { return new(stop.Status, null, null, stop.Message); }
    }

    public static RetailCollisionEligibility GroundEligibility(RetailCollisionTriangle triangle, RetailCollisionCosineReceipt? cosine)
    {
        bool consumed = false;
        try { bool value = Eligible(triangle, cosine, ref consumed); return new(RetailCollisionStatus.Computed, value, consumed, null); }
        catch (RetailCollisionStop stop) { return new(stop.Status, null, consumed, stop.Message); }
    }

    public static RetailCollisionPreparationResult Prepare(RetailCollisionData raw, RetailCollisionCosineReceipt? cosine,
        RetailCollisionPrepareLimits limits)
    {
        int processed = 0; bool consumed = false;
        try
        {
            if (raw is null || limits is null || limits.MaxReferences < 1 || limits.MaxBands < 1 || limits.MaxMetadataBytes < 1)
                Stop(RetailCollisionStatus.InvalidInput, "Explicit raw data and positive managed preparation limits are required.");
            long estimate = (long)raw.ReferenceCount * 12 + (long)raw.Bands.Count * 48 + (long)raw.Cells.Count * 48;
            if (raw.ReferenceCount > limits.MaxReferences || raw.Bands.Count > limits.MaxBands || estimate > limits.MaxMetadataBytes)
                Stop(RetailCollisionStatus.DefensiveLimit, "Preparation count/conservative metadata estimate limit, not a native no-surface result.");
            var cells = new RetailCollisionPreparedCell[raw.Cells.Count];
            var bands = new RetailCollisionPreparedBand[raw.Bands.Count];
            var references = new uint[raw.ReferenceCount];
            for (int c = 0; c < cells.Length; c++)
            {
                var cell = raw.Cells[c]; bool special = false;
                for (int b = 0; b < cell.BandCount; b++)
                {
                    var source = raw.Bands[cell.FirstBand + b];
                    int start = processed;
                    for (int r = 0; r < source.ReferenceCount; r++)
                    {
                        uint tagged = raw.GetRawReference(source, r), id = tagged & 0x7fffffffu;
                        var face = raw.GetTriangle(id);
                        if (SpecialMaterial(face.Material)) { special = true; tagged |= 0x80000000u; }
                        else if (ClassifiedMaterial(face.Material) || !Eligible(face, cosine, ref consumed)) tagged |= 0x80000000u;
                        references[processed++] = tagged;
                    }
                    // Native comparator places the untagged partition first, then sorts each partition by uint32 value.
                    Array.Sort(references, start, processed - start, NativeReferenceComparer.Instance);
                    bands[cell.FirstBand + b] = new(source.Height, start, source.ReferenceCount, source.HeaderFileOffset);
                }
                cells[c] = new(cell.FirstBand, cell.BandCount, cell.SharedEmptyStorage, special);
            }
            return new(RetailCollisionStatus.Computed, new(raw, cells, bands, references, cosine, consumed), null, processed, consumed);
        }
        catch (RetailCollisionStop stop) { return new(stop.Status, null, stop.Message, processed, consumed); }
    }

    internal static bool SpecialMaterial(byte material) => material == 11 || material is >= 13 and <= 16 or 135 or 136;
    internal static bool ClassifiedMaterial(byte material) => material is >= 1 and <= 9 or 11 or >= 14 and <= 16 or 135 or 136;

    internal static bool Intersect(RetailSearchPoint a, RetailSearchPoint b, RetailSearchPoint c,
        RetailSearchPoint origin, RetailSearchPoint delta, bool contiguous, out float parameter)
    {
        Point(a); Point(b); Point(c); Point(origin); Point(delta); parameter = 0;
        var e1 = Subtract(b, a); var e2 = Subtract(c, a); var p = Cross(delta, e2);
        float determinant = contiguous
            ? Add(Add(Multiply(p.Y, e1.Y), Multiply(p.X, e1.X)), Multiply(p.Z, e1.Z))
            : Dot(p, e1);
        if (determinant > -.001f && determinant < .001f) return false;
        float reciprocal = Divide(1f, determinant);
        var s = Subtract(origin, a);
        float u = Multiply(Dot(s, p), reciprocal);
        if (u < 0f || u > 1f) return false;
        var q = Cross(s, e1);
        float v = Multiply(Dot(delta, q), reciprocal);
        if (v < 0f || Add(v, u) > 1f) return false;
        parameter = Multiply(Dot(q, e2), reciprocal);
        return true; // Segment [0,1] admission belongs to its consumer; projection intentionally accepts a ray parameter.
    }

    private static bool Eligible(RetailCollisionTriangle triangle, RetailCollisionCosineReceipt? cosine, ref bool consumed)
    {
        Point(triangle.A); Point(triangle.B); Point(triangle.C);
        var e1 = Normalize(Subtract(triangle.B, triangle.A));
        var e2 = Normalize(Subtract(triangle.C, triangle.A));
        // Native computes the same oriented cross product through these component expressions.
        float z = Subtract(Multiply(e2.Y, e1.X), Multiply(e2.X, e1.Y));
        float x = Subtract(Multiply(e2.Z, e1.Y), Multiply(e2.Y, e1.Z));
        float y = Subtract(Multiply(e2.X, e1.Z), Multiply(e2.Z, e1.X));
        float norm = F(MathF.Sqrt(Add(Add(Multiply(x, x), Multiply(y, y)), Multiply(z, z))));
        if (norm > 0f) z = Multiply(z, Divide(1f, norm));
        consumed = true;
        if (cosine is null || !cosine.Known || cosine.NativeFunction != NativeCosineFunction ||
            !string.Equals(cosine.NativeBinarySha256, RetailGroundPathSearch.SupportedNpcBinarySha256, StringComparison.OrdinalIgnoreCase) ||
            cosine.ArgumentBits != NativeCosineArgumentBits || !Text(cosine.Revision) || !Digest(cosine.CaptureSha256))
            Stop(RetailCollisionStatus.MissingCosineEvidence, "The cosine return requires a pinned, source-bound return receipt; no host cosine default.");
        if (!RetailCollisionCosineSourceProof.CanConsume(cosine))
            Stop(RetailCollisionStatus.MissingCosineEvidence, "Conditional cosine proof has missing assumptions, conflicting bits or different proof/review identities.");
        float threshold = F(BitConverter.Int32BitsToSingle(unchecked((int)cosine.ResultBits)));
        if (threshold <= 0f || threshold >= 1f) Stop(RetailCollisionStatus.MissingCosineEvidence, "Cosine declaration is outside the admitted positive pi/4-result domain.");
        return z >= threshold; // Winding is retained; no absolute Z or Java angle folding.
    }
    private static RetailSearchPoint Normalize(RetailSearchPoint value)
    {
        float length = F(MathF.Sqrt(Dot(value, value)));
        return length > 0 ? Scale(value, Divide(1f, length)) : value;
    }
    internal static RetailSearchPoint Subtract(RetailSearchPoint a, RetailSearchPoint b) => new(Subtract(a.X, b.X), Subtract(a.Y, b.Y), Subtract(a.Z, b.Z));
    internal static RetailSearchPoint Scale(RetailSearchPoint a, float scale) => new(Multiply(a.X, scale), Multiply(a.Y, scale), Multiply(a.Z, scale));
    internal static RetailSearchPoint Along(RetailSearchPoint origin, RetailSearchPoint delta, float factor) =>
        new(Add(origin.X, Multiply(delta.X, factor)), Add(origin.Y, Multiply(delta.Y, factor)), Add(origin.Z, Multiply(delta.Z, factor)));
    internal static RetailSearchPoint Cross(RetailSearchPoint a, RetailSearchPoint b) =>
        new(Subtract(Multiply(a.Y, b.Z), Multiply(a.Z, b.Y)), Subtract(Multiply(a.Z, b.X), Multiply(a.X, b.Z)), Subtract(Multiply(a.X, b.Y), Multiply(a.Y, b.X)));
    internal static float Dot(RetailSearchPoint a, RetailSearchPoint b) => Add(Add(Multiply(a.X, b.X), Multiply(a.Y, b.Y)), Multiply(a.Z, b.Z));
    internal static float Squared(RetailSearchPoint a, RetailSearchPoint b)
    { var d = Subtract(a, b); return Add(Add(Multiply(d.Y, d.Y), Multiply(d.X, d.X)), Multiply(d.Z, d.Z)); }
    internal static float Add(float a, float b) => F(a + b);
    internal static float Subtract(float a, float b) => F(a - b);
    internal static float Multiply(float a, float b) => F(a * b);
    internal static float Divide(float a, float b) => F(a / b);
    internal static void Point(RetailSearchPoint point) { F(point.X); F(point.Y); F(point.Z); }
    internal static float F(float value)
    {
        uint bits = unchecked((uint)BitConverter.SingleToInt32Bits(value)) & 0x7fffffffu;
        if (bits >= 0x7f800000u || (bits != 0 && bits < 0x00800000u))
            Stop(RetailCollisionStatus.UnsupportedArithmetic, "Consumed nonfinite/subnormal binary32 state is outside the proven floating-point environment.");
        return value;
    }
    internal static bool Text(string? value) => !string.IsNullOrWhiteSpace(value);
    internal static bool Digest(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    internal static void Stop(RetailCollisionStatus status, string detail) => throw new RetailCollisionStop(status, detail);
    private sealed class NativeReferenceComparer : IComparer<uint>
    {
        internal static readonly NativeReferenceComparer Instance = new();
        public int Compare(uint a, uint b)
        {
            if ((int)a < 0 && (int)b >= 0) return 1;
            if ((int)a >= 0 && (int)b < 0) return -1;
            return a < b ? -1 : b < a ? 1 : 0;
        }
    }
}
