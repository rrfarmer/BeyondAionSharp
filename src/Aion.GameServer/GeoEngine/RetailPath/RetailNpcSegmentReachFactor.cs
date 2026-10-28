namespace Aion.GameServer.GeoEngine.RetailPath;

public enum RetailNpcSegmentReachStatus { Calculated, UnsupportedSource, UnsupportedArithmetic }

public sealed record RetailNpcSegmentReachResult(RetailNpcSegmentReachStatus Status, float? Factor,
    float? LengthSquared, float? Discriminant, float? AnalyticCandidate, int FineSamples, string? Detail = null)
{
    public bool RuntimeVerified => false;
    public bool ExecutesMovement => false;
}

/// <summary>
/// Complete inactive calculation of NPCServer0x140225af0, used by World::MoveByPath.
/// Java NpcMoveController was read first. Evidence: phase4/path-following, checked native
/// scalar instruction order. This computes a segment factor, not collision, clearance or arrival.
/// The argument is an already selected squared radius; the caller's0.9 multiplier is separate.
/// </summary>
public static class RetailNpcSegmentReachFactor
{
    public static RetailNpcSegmentReachResult Calculate(RetailSearchPoint origin, RetailSearchPoint delta,
        RetailSearchPoint target, float squaredRadius, string nativeNpcSha256)
    {
        if (!string.Equals(nativeNpcSha256, RetailGroundPathSearch.SupportedNpcBinarySha256, StringComparison.Ordinal))
            return new(RetailNpcSegmentReachStatus.UnsupportedSource, null, null, null, null, 0, "Pinned NPCServer identity required.");
        try
        {
            float x = Scalar(delta.X), y = Scalar(delta.Y), z = Scalar(delta.Z);
            Scalar(origin.X); Scalar(origin.Y); Scalar(origin.Z);
            Scalar(target.X); Scalar(target.Y); Scalar(target.Z); Scalar(squaredRadius);
            if (squaredRadius < 0) throw new ArithmeticException("Negative squared radius is outside the admitted caller domain.");
            // Every operation remains binary32 in the recovered order. In particular, do not
            // reassociate c*(a*4) into (c*a)*4 or the final y-square+x-square into x+y.
            float a = Add(Add(Mul(x, x), Mul(y, y)), Mul(z, z));
            float factor = 0;
            float? discriminant = null, candidate = null;
            int samples = 0;
            if (a >= .0001f || a <= -.0001f)
            {
                float dx = Sub(target.X, origin.X), dy = Sub(target.Y, origin.Y), dz = Sub(target.Z, origin.Z);
                float b = Sub(Mul(Mul(z, dz), -2f), Add(Mul(Mul(x, dx), 2f), Mul(Mul(y, dy), 2f)));
                float c = Sub(Add(Add(Mul(dx, dx), Mul(dy, dy)), Mul(dz, dz)), squaredRadius);
                float d = Sub(Mul(b, b), Mul(c, Mul(a, 4f)));
                discriminant = d;
                if (d < 0) return new(RetailNpcSegmentReachStatus.Calculated, 1f, a, d, null, 0);
                candidate = d > 0 ? Div(Sub(Scalar(-b), Scalar(MathF.Sqrt(d))), Mul(a, 2f)) : Scalar(-Div(b, Mul(a, 2f)));
                if (candidate >= 0 && candidate <= 1) factor = candidate.Value;
            }
            do
            {
                samples++;
                float dx = Sub(target.X, Add(origin.X, Mul(x, factor)));
                float dy = Sub(target.Y, Add(origin.Y, Mul(y, factor)));
                float dz = Sub(target.Z, Add(origin.Z, Mul(z, factor)));
                if (Add(Add(Mul(dy, dy), Mul(dx, dx)), Mul(dz, dz)) <= squaredRadius) break;
                factor = Add(factor, .005f);
            } while (factor <= 1f);
            // Native fallback can return >1. A clamp would change its subsequent consumer.
            return new(RetailNpcSegmentReachStatus.Calculated, factor, a, discriminant, candidate, samples);
        }
        catch (ArithmeticException e)
        {
            return new(RetailNpcSegmentReachStatus.UnsupportedArithmetic, null, null, null, null, 0, e.Message);
        }
    }

    // Nonfinite/denormal inputs or intermediates are visible support boundaries until captured
    // native MXCSR behavior is available; these are not invented native world-movement rules.
    private static float Scalar(float value) => float.IsFinite(value) && !float.IsSubnormal(value)
        ? value : throw new ArithmeticException("Finite normal binary32 values or signed zero are required.");
    private static float Add(float a, float b) => Scalar(a + b);
    private static float Sub(float a, float b) => Scalar(a - b);
    private static float Mul(float a, float b) => Scalar(a * b);
    private static float Div(float a, float b) => Scalar(a / b);
}
