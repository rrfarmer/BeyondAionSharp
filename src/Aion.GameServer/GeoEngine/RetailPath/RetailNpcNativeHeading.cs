using System.Runtime.CompilerServices;

namespace Aion.GameServer.GeoEngine.RetailPath;

public enum RetailNpcHeadingStatus { ComputedConditionalReturn, UnsupportedFloatingEnvironment, UnsupportedNonFiniteInput, SourceIdentityMismatch }
/// <summary>Explicit declaration, not a measurement of the host MXCSR or a native return oracle.</summary>
public sealed record RetailNpcMovementFpEnvironment(string NativeNpcSha256, string ReconstructionProofSha256,
    bool NearestEven, bool ExceptionsMasked, bool FlushToZero, bool DenormalsAreZero, bool Sse2,
    string Evidence, bool SyntheticFixture);
public sealed record RetailNpcAtan2Result(RetailNpcHeadingStatus Status, uint? ReturnWord,
    int? TableIndex, IReadOnlyList<uint> ExplicitStatusMasks, string? Detail)
{
    public bool Success => Status == RetailNpcHeadingStatus.ComputedConditionalReturn;
    public bool NativeTargetExecuted => false;
    public bool ArithmeticStickyFlagsVerified => false;
}

/// <summary>
/// Inactive finite-binary32 reconstruction of pinned NPC14039d7e0 plus its two distinct callers.
/// Exact source table/coefficients and ordered scalar binary64 operations; no MathF.Atan2 fallback.
/// Conditional RNE/masked/FTZ-DAZ-off low return only; explicit helper masks do not claim full FP status.
/// Java controller heading was read first; native caller quantizers are deliberately separate.
/// </summary>
public static class RetailNpcNativeHeading
{
    public const string ProofSha256 = "16d0990721898c6550f95856abc6e8d1819418bde4cdc192f80177b7f4babea0";
    public const ulong NativeAtan2Entry = 0x14039d7e0;
    private const ulong PiWord = 0x400921fb54442d18UL, HalfPiWord = 0x3ff921fb54442d18UL;
    private static readonly ulong[] table = [
        0x3faff55bb72cfde9UL, 0x3fb0f99ea71d52a6UL, 0x3fb1f86dbf082d58UL, 0x3fb2f719318a4a9aUL, 0x3fb3f59f0e7c559dUL, 0x3fb4f3fd677292fbUL,
        0x3fb5f2324fd2d7b2UL, 0x3fb6f03bdcea4b0cUL, 0x3fb7ee182602f10eUL, 0x3fb8ebc54478fb28UL, 0x3fb9e94153cfdcf1UL, 0x3fbae68a71c722b8UL,
        0x3fbbe39ebe6f07c3UL, 0x3fbce07c5c3cca32UL, 0x3fbddd21701eba6eUL, 0x3fbed98c2190043aUL, 0x3fbfd5ba9aac2f6dUL, 0x3fc068d584212b3dUL,
        0x3fc0e6adccf40881UL, 0x3fc1646541060850UL, 0x3fc1e1fafb043726UL, 0x3fc25f6e171a535cUL, 0x3fc2dcbdb2fba1ffUL, 0x3fc359e8edeb99a3UL,
        0x3fc3d6eee8c6626cUL, 0x3fc453cec6092a9eUL, 0x3fc4d087a9da4f17UL, 0x3fc54d18ba11570aUL, 0x3fc5c9811e3ec269UL, 0x3fc645bfffb3aa73UL,
        0x3fc6c1d4898933d8UL, 0x3fc73dbde8a7d201UL, 0x3fc7b97b4bce5b02UL, 0x3fc8350be398ebc7UL, 0x3fc8b06ee2879c28UL, 0x3fc92ba37d050271UL,
        0x3fc9a6a8e96c8626UL, 0x3fca217e601081a5UL, 0x3fca9c231b403279UL, 0x3fcb1696574d780bUL, 0x3fcb90d7529260a2UL, 0x3fcc0ae54d768466UL,
        0x3fcc84bf8a742e6dUL, 0x3fccfe654e1d5395UL, 0x3fcd77d5df205736UL, 0x3fcdf110864c9d9dUL, 0x3fce6a148e96ec4dUL, 0x3fcee2e1451d980cUL,
        0x3fcf5b75f92c80ddUL, 0x3fcfd3d1fc40dbe4UL, 0x3fd025fa510665b5UL, 0x3fd061eea03d6290UL, 0x3fd09dc597d86362UL, 0x3fd0d97ee509acb3UL,
        0x3fd1151a362431c9UL, 0x3fd150973a9ce546UL, 0x3fd18bf5a30bf178UL, 0x3fd1c735212dd883UL, 0x3fd2025567e47c95UL, 0x3fd23d562b381041UL,
        0x3fd278372057ef45UL, 0x3fd2b2f7fd9b5fe2UL, 0x3fd2ed987a823cfeUL, 0x3fd328184fb58951UL, 0x3fd362773707ebcbUL, 0x3fd39cb4eb76157bUL,
        0x3fd3d6d129271134UL, 0x3fd410cbad6c7d32UL, 0x3fd44aa436c2af09UL, 0x3fd4845a84d0c21bUL, 0x3fd4bdee586890e6UL, 0x3fd4f75f73869978UL,
        0x3fd530ad9951cd49UL, 0x3fd569d88e1b4cd7UL, 0x3fd5a2e0175e0f4eUL, 0x3fd5dbc3fbbe768dUL, 0x3fd614840309cfe1UL, 0x3fd64d1ff635c1c5UL,
        0x3fd685979f5fa6fdUL, 0x3fd6bdeac9cbd76cUL, 0x3fd6f61941e4def0UL, 0x3fd72e22d53aa2a9UL, 0x3fd7660752817501UL, 0x3fd79dc6899118d1UL,
        0x3fd7d5604b63b3f7UL, 0x3fd80cd46a14b1d0UL, 0x3fd84422b8df95d7UL, 0x3fd87b4b0c1ebedbUL, 0x3fd8b24d394a1b25UL, 0x3fd8e92916f5cde8UL,
        0x3fd91fde7cd0c662UL, 0x3fd9566d43a34907UL, 0x3fd98cd5454d6b18UL, 0x3fd9c3165cc58107UL, 0x3fd9f93066168001UL, 0x3fda2f233e5e530bUL,
        0x3fda64eec3cc23fcUL, 0x3fda9a92d59e98cfUL, 0x3fdad00f5422058bUL, 0x3fdb056420ae9343UL, 0x3fdb3a911da65c6cUL, 0x3fdb6f962e737efbUL,
        0x3fdba473378624a5UL, 0x3fdbd9281e528191UL, 0x3fdc0db4c94ec9efUL, 0x3fdc42191ff11eb6UL, 0x3fdc76550aad71f8UL, 0x3fdcaa6872f3631bUL,
        0x3fdcde53432c1350UL, 0x3fdd121566b7f2adUL, 0x3fdd45aec9ec862bUL, 0x3fdd791f5a1226f4UL, 0x3fddac670561bb4fUL, 0x3fdddf85bb026974UL,
        0x3fde127b6b0744afUL, 0x3fde4548066cf51aUL, 0x3fde77eb7f175a34UL, 0x3fdeaa65c7cf28c4UL, 0x3fdedcb6d43f8434UL, 0x3fdf0ede98f393cfUL,
        0x3fdf40dd0b541417UL, 0x3fdf72b221a4e495UL, 0x3fdfa45dd3029258UL, 0x3fdfd5e0175fdf83UL, 0x3fe0039c73c1a40bUL, 0x3fe01c341e82422dUL,
        0x3fe034b709250488UL, 0x3fe04d25314342e5UL, 0x3fe0657e94db30cfUL, 0x3fe07dc3324e9b38UL, 0x3fe095f30861a58fUL, 0x3fe0ae0e1639866cUL,
        0x3fe0c6145b5b43daUL, 0x3fe0de05d7aa6f7cUL, 0x3fe0f5e28b67e295UL, 0x3fe10daa77307a0dUL, 0x3fe1255d9bfbd2a8UL, 0x3fe13cfbfb1b056eUL,
        0x3fe1548596376469UL, 0x3fe16bfa6f5137e1UL, 0x3fe1835a88be7c13UL, 0x3fe19aa5e5299f99UL, 0x3fe1b1dc87904284UL, 0x3fe1c8fe7341f64fUL,
        0x3fe1e00babdefeb3UL, 0x3fe1f7043557138aUL, 0x3fe20de813e823b1UL, 0x3fe224b74c1d192aUL, 0x3fe23b71e2cc9e6aUL, 0x3fe25217dd17e501UL,
        0x3fe268a940696da6UL, 0x3fe27f261273d1b3UL, 0x3fe2958e59308e30UL, 0x3fe2abe21aded073UL, 0x3fe2c2215e024465UL, 0x3fe2d84c2961e48bUL,
        0x3fe2ee628406cbcaUL, 0x3fe30464753b090aUL, 0x3fe31a52048874beUL, 0x3fe3302b39b78856UL, 0x3fe345f01cce37bbUL, 0x3fe35ba0b60eccceUL,
        0x3fe3713d0df6c503UL, 0x3fe386c52d3db11eUL, 0x3fe39c391cd41719UL, 0x3fe3b198e5e2564aUL, 0x3fe3c6e491c78dc4UL, 0x3fe3dc1c2a188504UL,
        0x3fe3f13fb89e96f4UL, 0x3fe4064f47569f48UL, 0x3fe41b4ae06fea41UL, 0x3fe430328e4b26d5UL, 0x3fe445065b795b55UL, 0x3fe459c652badc7fUL,
        0x3fe46e727efe4715UL, 0x3fe4830aeb5f7bfdUL, 0x3fe4978fa3269ee1UL, 0x3fe4ac00b1c71762UL, 0x3fe4c05e22de94e4UL, 0x3fe4d4a8023414e8UL,
        0x3fe4e8de5bb6ec04UL, 0x3fe4fd013b7dd17eUL, 0x3fe51110adc5ed81UL, 0x3fe5250cbef1e9faUL, 0x3fe538f57b89061eUL, 0x3fe54ccaf0362c8fUL,
        0x3fe5608d29c70c34UL, 0x3fe5743c352b33b9UL, 0x3fe587d81f732fbaUL, 0x3fe59b60f5cfab9dUL, 0x3fe5aed6c5909517UL, 0x3fe5c2399c244260UL,
        0x3fe5d58987169b18UL, 0x3fe5e8c6941043cfUL, 0x3fe5fbf0d0d5cc49UL, 0x3fe60f084b46e05eUL, 0x3fe6220d115d7b8dUL, 0x3fe634ff312d1f3bUL,
        0x3fe647deb8e20b8fUL, 0x3fe65aabb6c07b02UL, 0x3fe66d663923e086UL, 0x3fe6800e4e7e2857UL, 0x3fe692a40556fb6aUL, 0x3fe6a5276c4b0575UL,
        0x3fe6b798920b3d98UL, 0x3fe6c9f7855c3198UL, 0x3fe6dc44551553aeUL, 0x3fe6ee7f10204aefUL, 0x3fe700a7c5784633UL, 0x3fe712be84295198UL,
        0x3fe724c35b4fae7bUL, 0x3fe736b65a172dffUL, 0x3fe748978fba8e0fUL, 0x3fe75a670b82d8d8UL, 0x3fe76c24dcc6c6c0UL, 0x3fe77dd112ea22c7UL,
        0x3fe78f6bbd5d315eUL, 0x3fe7a0f4eb9c19a2UL, 0x3fe7b26cad2e50fdUL, 0x3fe7c3d311a6092bUL, 0x3fe7d528289fa093UL, 0x3fe7e66c01c114fdUL,
        0x3fe7f79eacb97898UL, 0x3fe808c03940694aUL, 0x3fe819d0b7158a4cUL, 0x3fe82ad036000005UL, 0x3fe83bbec5cdee22UL, 0x3fe84c9c7653f7eaUL,
        0x3fe85d69576cc2c5UL, 0x3fe86e2578f87ae5UL, 0x3fe87ed0eadc5a2aUL, 0x3fe88f6bbd023118UL, 0x3fe89ff5ff57f1f7UL, 0x3fe8b06fc1cf3dfeUL,
        0x3fe8c0d9145cf49dUL, 0x3fe8d13206f8c4caUL, 0x3fe8e17aa99cc05dUL, 0x3fe8f1b30c44f167UL, 0x3fe901db3eeef187UL, 0x3fe911f35199833bUL,
        0x3fe921fb54442d18UL];
    private static double D(ulong bits) => BitConverter.UInt64BitsToDouble(bits);
    // Separate calls prevent implicit reassociation/contraction of the native scalar operations.
    [MethodImpl(MethodImplOptions.NoInlining)] private static double Add(double a, double b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static double Sub(double a, double b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static double Mul(double a, double b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static double Div(double a, double b) => a / b;
    [MethodImpl(MethodImplOptions.NoInlining)] internal static float Add32(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] internal static float Sub32(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] internal static float Mul32(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] internal static float Div32(float a, float b) => a / b;

    public static RetailNpcAtan2Result Atan2(uint yWord, uint xWord, RetailNpcMovementFpEnvironment? environment)
    {
        if (environment is null || !environment.NearestEven || !environment.ExceptionsMasked || environment.FlushToZero ||
            environment.DenormalsAreZero || !environment.Sse2 || string.IsNullOrWhiteSpace(environment.Evidence))
            return Reject(RetailNpcHeadingStatus.UnsupportedFloatingEnvironment, "Explicit RNE/masked/SSE2/FTZ-DAZ-off declaration is required.");
        if (!string.Equals(environment.NativeNpcSha256, RetailGroundPathSearch.SupportedNpcBinarySha256, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(environment.ReconstructionProofSha256, ProofSha256, StringComparison.OrdinalIgnoreCase))
            return Reject(RetailNpcHeadingStatus.SourceIdentityMismatch, "Pinned binary and reviewed finite reconstruction proof must match.");
        if ((yWord & 0x7f800000) == 0x7f800000 || (xWord & 0x7f800000) == 0x7f800000)
            return Reject(RetailNpcHeadingStatus.UnsupportedNonFiniteInput, "Native nonfinite/error-handler branches are outside this admitted return unit.");
        double y = BitConverter.UInt32BitsToSingle(yWord), x = BitConverter.UInt32BitsToSingle(xWord);
        ulong yw = BitConverter.DoubleToUInt64Bits(y), xw = BitConverter.DoubleToUInt64Bits(x);
        bool sy = (yw >> 63) != 0, sx = (xw >> 63) != 0;
        ulong ay = yw & 0x7fffffffffffffff, ax = xw & 0x7fffffffffffffff;
        int gap = (int)((yw >> 52) & 0x7ff) - (int)((xw >> 52) & 0x7ff);
        List<uint> masks = []; int? index = null;
        RetailNpcAtan2Result Done(uint word) => new(RetailNpcHeadingStatus.ComputedConditionalReturn, word, index, masks.AsReadOnly(), null);
        uint Cast(double v) => BitConverter.SingleToUInt32Bits((float)v);
        double Signed(ulong value) => D(value ^ (sy ? 1UL << 63 : 0));
        if (ay == 0)
        {
            if (!sx) return Done(yWord);
            masks.Add(0x20); return Done(Cast(Signed(PiWord)));
        }
        if (ax == 0)
        {
            masks.Add(0x20);
            if (sy) return Done(Cast(Signed(HalfPiWord)));
        }
        if (gap > 26) { masks.Add(0x20); return Done(Cast(Signed(HalfPiWord))); }
        if (gap < -13 && !sx)
        {
            if (gap < -150) { masks.Add(0x30); return Done(sy ? 0x80000000u : 0u); }
            double ratio;
            if (gap < -126)
            {
                ulong scaled = BitConverter.DoubleToUInt64Bits(Div(Mul(y, D(0x4630000000000000)), x));
                int exponent = (int)((scaled >> 52) & 0x7ff) - 100;
                // Finite f32 domain + gap>=-150 proves this binary64 exponent positive.
                if (exponent <= 0) throw new InvalidOperationException("Finite-domain scaled exponent proof violated.");
                ratio = D(((ulong)exponent << 52) | (scaled & 0x800fffffffffffff));
            }
            else ratio = Div(y, x);
            return Done(Cast(ratio));
        }
        if (gap < -26 && sx) { masks.Add(0x20); return Done(Cast(Signed(PiWord))); }
        bool swap = D(ay) > D(ax); double low = D(swap ? ax : ay), high = D(swap ? ay : ax);
        double r = Div(low, high);
        if (r > D(0x3fb0000000000000))
        {
            int k = (int)Add(Mul(r, 256d), .5d); index = k;
            double u = Div(Sub(Mul(low, 256d), Mul(k, high)), Add(Mul(k, low), Mul(high, 256d)));
            r = Sub(Add(u, D(table[k - 16])), Mul(Mul(Mul(u, u), u), D(0x3fd5555555550877)));
        }
        else if (r >= D(0x3f1a36e2eb1c432d))
        {
            double t = Mul(r, r);
            double v = Sub(D(0x3fc99999999643a3), Mul(t, D(0x3fc2492482bd6be1)));
            v = Mul(v, t); v = Sub(D(0x3fd5555555555538), v);
            v = Mul(v, Mul(t, r)); r = Sub(r, v);
        }
        if (swap) r = Sub(D(HalfPiWord), r);
        if (sx) r = Sub(D(PiWord), r);
        if (sy) r = D(BitConverter.DoubleToUInt64Bits(r) ^ (1UL << 63));
        return Done(Cast(r));
    }
    public static int QuantizeMoveToGoal(uint angleWord)
    {
        float angle = BitConverter.UInt32BitsToSingle(angleWord);
        if (!float.IsFinite(angle) || System.MathF.Abs(angle) > 3.141593f) throw new ArgumentOutOfRangeException(nameof(angleWord));
        int h = (int)Div32(Add32(Div32(Mul32(angle, 180f), BitConverter.UInt32BitsToSingle(0x40490fdb)), 1.5f), 3f);
        return h < 0 ? h + 120 : h;
    }
    public static int QuantizeLook(uint angleWord)
    {
        float angle = BitConverter.UInt32BitsToSingle(angleWord);
        if (!float.IsFinite(angle) || System.MathF.Abs(angle) > 3.141593f) throw new ArgumentOutOfRangeException(nameof(angleWord));
        int degree = (360 - (int)Div32(Mul32(angle, -180f), BitConverter.UInt32BitsToSingle(0x40490fdb))) % 360;
        return degree / 3;
    }
    public static int? Look(RetailSearchPoint from, RetailSearchPoint to, int oldHeading,
        RetailNpcMovementFpEnvironment environment, out RetailNpcAtan2Result? evaluation)
    {
        evaluation = null;
        if (!from.IsFinite || !to.IsFinite) return null;
        float x = Sub32(to.X, from.X), y = Sub32(to.Y, from.Y);
        if (System.MathF.Abs(x) < .05f && System.MathF.Abs(y) < .05f) return oldHeading;
        evaluation = Atan2(BitConverter.SingleToUInt32Bits(y), BitConverter.SingleToUInt32Bits(x), environment);
        return evaluation.Success ? QuantizeLook(evaluation.ReturnWord!.Value) : null;
    }
    private static RetailNpcAtan2Result Reject(RetailNpcHeadingStatus status, string detail) => new(status, null, null, Array.Empty<uint>(), detail);
}
