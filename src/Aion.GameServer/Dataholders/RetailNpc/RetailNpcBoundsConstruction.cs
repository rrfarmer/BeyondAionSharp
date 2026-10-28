using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Aion.GameServer.Dataholders.RetailNpc;

public sealed record RetailNpcBoundsContext(RetailNpcTemplateSourceReceipt ExpectedSource,
    bool? FreshObjectClassConstructor, bool? NearestEvenNormalArithmetic, string Evidence);
public sealed record RetailNpcBoundsValues(float Front, float Side, float Upper);
public sealed record RetailNpcBoundsStep(string Operation, string NativeSource, string Detail);
public sealed record RetailNpcBoundsCopy(int ActorOffset, int Width, ulong Bits, int Order);
public sealed record RetailNpcBoundsIssue(string Code, string Detail);
public sealed record RetailNpcBoundsPlan(RetailNpcTemplateSourceReceipt Source,
    RetailNpcBoundsValues Constructor, RetailNpcBoundsValues AfterParser,
    RetailNpcBoundsValues FinalAxes, float TemplateScale, float HorizontalRadius,
    bool? OriginalParserReturn, bool ScaleApplied, bool HasParserDiagnostics,
    ImmutableArray<RetailNpcBoundsStep> Steps, ImmutableArray<RetailNpcBoundsCopy> CommonTailCopies)
{
    // Native parser diagnostics remain visible even when its upper failure returns one.
    public bool CanSupplySourceDerivedBounds => !HasParserDiagnostics;
    public bool CurrentActorBoundsObserved => false;
    public bool ActorClearanceVerified => false;
    public bool RuntimeVerified => false;
    public bool ActivationReady => false;
}
public sealed record RetailNpcBoundsConstructionResult(RetailNpcBoundsPlan? Plan,
    ImmutableArray<RetailNpcBoundsIssue> Issues)
{
    public bool Success => Plan is not null && Issues.IsEmpty;
}

/// <summary>
/// Inactive complete ObjectClass bounds-construction and NPC::SetClass copy plan.
/// NPC 1b4630 constructor, 1b5490 bound_radius case, 319160 parser, 104cc0 common tail.
/// Exact full bodies/name-table bytes and independent vectors live in external
/// phase4/actor-bounds-construction. No actor, movement owner, effect or world is touched.
/// Axis names do not assert a capsule, diameter, clearance policy or current actor state.
/// </summary>
public static class RetailNpcBoundsConstruction
{
    public const string NpcBinarySha256 = "c24e600107352268d377c9a7361e52d848e96af8deff61259e031b98b7130350";

    public static RetailNpcBoundsConstructionResult Construct(RetailNpcTemplateInputs source,
        RetailNpcBoundsContext context)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(context);
        if (context.ExpectedSource != source.Receipt || source.Receipt.NpcSha256 != NpcBinarySha256)
            return Fail("source_identity", "Expected complete source receipt and pinned NPC binary must match.");
        if (context.FreshObjectClassConstructor != true || context.NearestEvenNormalArithmetic != true
            || string.IsNullOrWhiteSpace(context.Evidence) || context.Evidence.Length > 4096)
            return Fail("construction_context", "Fresh constructor and explicit nearest-even normal arithmetic evidence required.");
        var steps = ImmutableArray.CreateBuilder<RetailNpcBoundsStep>();
        var ctor = new RetailNpcBoundsValues(.5f, .5f, 2f);
        var parsed = ctor;
        float scale = 1f, radius = .5f;
        bool? parserReturn = null;
        bool diagnostics = false, scaled = false;
        steps.Add(new("FreshObjectClassBounds", "1401b4644;1401b4680;1401b468a;1401b4969;1401b4973",
            "axes=.5,.5,2; scale=1; radius=.5 independently, without max/2 recomputation"));
        var bounds = source.Bounds;
        if (bounds.Presence != RetailNpcSourcePresence.Absent)
        {
            if (bounds.Presence is not (RetailNpcSourcePresence.ExplicitValue or RetailNpcSourcePresence.ExplicitZero))
                return Fail("bounds_presence", "Unrecognized bound_radius source presence.");
            steps.Add(new("ResetParserAxes", "140319160", "front=.5; side=.5; upper=2 before original front read"));
            var front = bounds.Axes["front"]; var side = bounds.Axes["side"]; var upper = bounds.Axes["upper"];
            if (!Valid(front))
                return Fail("numeric_domain", "Consumed front must be a finite normal/zero binary32 value or absent.");
            if (front.Presence == RetailNpcSourcePresence.Absent)
            {
                diagnostics = true; parserReturn = false;
                steps.Add(new("FrontAbsentParserReturnsZero", "140319160", "constructor axes retained; original front diagnostic"));
            }
            else
            {
                parsed = parsed with { Front = front.ParsedValue!.Value };
                if (!Valid(side)) return Fail("numeric_domain", "Consumed side leaves the finite normal/zero domain.");
                if (side.Presence == RetailNpcSourcePresence.Absent)
                {
                    diagnostics = true; parserReturn = false;
                    steps.Add(new("SideAbsentParserReturnsZero", "140319160", "parsed front retained; side/upper defaults; original side diagnostic"));
                }
                else
                {
                    parsed = parsed with { Side = side.ParsedValue!.Value };
                    if (!Valid(upper)) return Fail("numeric_domain", "Consumed upper leaves the finite normal/zero domain.");
                    parserReturn = true;
                    if (upper.Presence == RetailNpcSourcePresence.Absent)
                    {
                        diagnostics = true;
                        steps.Add(new("UpperAbsentParserStillReturnsOne", "140319160", "upper=2; original upper diagnostic does not change successful return"));
                    }
                    else parsed = parsed with { Upper = upper.ParsedValue!.Value };
                }
            }
        }
        else steps.Add(new("BoundGroupAbsent", "1401b5490", "no bound parser, scale lookup or radius recomputation"));
        var axes = parsed;
        if (parserReturn == true)
        {
            var rawScale = source.VisualScale;
            if (rawScale.Presence != RetailNpcSourcePresence.Absent && rawScale.ParsedValue is null)
                return Fail("scale_presence", "Explicit scale needs its original signed32 parse.");
            if (rawScale.ParsedValue is { } integerScale && integerScale != 100)
            {
                // CVTSI2SS signed32, DIVSS100, then three independent MULSS.
                scale = Divide(Convert(integerScale), 100f);
                axes = new(Multiply(scale, parsed.Front), Multiply(scale, parsed.Side), Multiply(scale, parsed.Upper));
                scaled = true;
                if (!NormalOrZero(axes.Front) || !NormalOrZero(axes.Side) || !NormalOrZero(axes.Upper))
                    return Fail("numeric_domain", "Scaled axes leave the supported finite normal/zero domain.");
                steps.Add(new("ApplySigned32Scale", "1401b586d..1401b58c3", "signed32 to binary32, /100, independent axis multiplications"));
            }
            else steps.Add(new("ScaleAbsentOrHundred", "1401b5840..1401b587d", "constructor scale remains1; no axis multiply"));
            float maximum = axes.Front;
            if (axes.Front <= axes.Side) maximum = axes.Side; // Preserve equal/signed-zero selection.
            radius = Multiply(maximum, .5f);
            if (!NormalOrZero(radius)) return Fail("numeric_domain", "Radius leaves the supported finite normal/zero domain.");
            steps.Add(new("HalfSelectedHorizontalAxis", "1401b58cb..1401b58f0", "front<=side selects side, then separate MULSS.5"));
        }
        else if (parserReturn == false)
            steps.Add(new("ParserFailureKeepsRadiusAndScale", "1401b582e..1401b5835", "partial parsed axes remain; radius=.5 and scale=1"));

        // Symbolic common-tail writes. The caller must separately reach SetClass's tail
        // and own a current actor/instance; no constructor defaults for a live actor.
        var copies = ImmutableArray.Create(
            new RetailNpcBoundsCopy(0xc0, 8, Bits(axes.Front) | ((ulong)Bits(axes.Side) << 32), 1),
            new RetailNpcBoundsCopy(0xc8, 4, Bits(axes.Upper), 2),
            new RetailNpcBoundsCopy(0xbc, 4, Bits(radius), 3));
        steps.Add(new("NpcSetClassCommonTailCopies", "140105043..140105068", "template13c/140->actorc0/c4 qword;144->c8;148->bc"));
        return new(new(source.Receipt, ctor, parsed, axes, scale, radius, parserReturn, scaled, diagnostics,
            steps.ToImmutable(), copies), ImmutableArray<RetailNpcBoundsIssue>.Empty);
    }

    private static bool Valid(RetailNpcSourceScalar<float> x) => x.Presence == RetailNpcSourcePresence.Absent
        ? x.ParsedValue is null : x.Presence is RetailNpcSourcePresence.ExplicitValue or RetailNpcSourcePresence.ExplicitZero
            && x.ParsedValue is { } value && NormalOrZero(value);
    private static bool NormalOrZero(float value) => float.IsFinite(value) && (value == 0f || float.IsNormal(value));
    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);
    private static RetailNpcBoundsConstructionResult Fail(string code, string detail) => new(null, [new(code, detail)]);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Convert(int value) => value;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Divide(float a, float b) => a / b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Multiply(float a, float b) => a * b;
}
