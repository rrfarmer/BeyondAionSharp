using System.Buffers.Binary;
using static Aion.GameServer.GeoEngine.RetailPath.RetailNpcApproachExecution;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>
/// Inactive admitted ordinary-ground replay of NPC::MoveToTarget0x1401211b0.
/// Consumes recorded helper data, never implements/executes collision, movement, search or actor mutation.
/// </summary>
public static class RetailNpcGroundApproachPlan
{
    public const string TableYSha256 = "ee3f8c41695a4c1c9ec1fe9c52d82facad293638e01317f881c765789e6ea247";
    public const string TableXSha256 = "5a95593d012096b1451a7b27d69e1b2e84eef60a7df51170d3e141334fcef1fb";
    internal static ReadOnlySpan<byte> TableYBytes => tableY;
    internal static ReadOnlySpan<byte> TableXBytes => tableX;
    private static readonly byte[] tableY = Convert.FromHexString("00000000465e563dc712d63d3c30203ee2e6543eed83843e7a379e3e037cb73ed53fd03e6171e83e0000003f766d0b3f1479163f1e1b213f2b4c2b3ff704353fc03e3e3f0af3463fbd1b4f3f25b3563fd0b35d3f0919643f16de693f19ff6e3f7978733fed46773fe8677a3f1fd97c3ffe987e3f37a67f3f0000803f37a67f3ffe987e3f1fd97c3fe8677a3fed46773f7978733f19ff6e3f16de693f0919643fd0b35d3f25b3563fbd1b4f3f0af3463fc03e3e3ff704353f2b4c2b3f1e1b213f1479163f766d0b3f0000003f6171e83ed53fd03e037cb73e7a379e3eed83843ee2e6543e3c30203ec712d63d465e563d00000000465e56bdc712d6bd3c3020bee2e654beed8384be7a379ebe037cb7bed53fd0be6171e8be000000bf766d0bbf147916bf1e1b21bf2b4c2bbff70435bfc03e3ebf0af346bfbd1b4fbf25b356bfd0b35dbf091964bf16de69bf19ff6ebf797873bfed4677bfe8677abf1fd97cbffe987ebf37a67fbf000080bf37a67fbffe987ebf1fd97cbfe8677abfed4677bf797873bf19ff6ebf16de69bf091964bfd0b35dbf25b356bfbd1b4fbf0af346bfc03e3ebff70435bf2b4c2bbf1e1b21bf147916bf766d0bbf000000bf6171e8bed53fd0be037cb7be7a379ebeed8384bee2e654be3c3020bec712d6bd465e56bd");
    private static readonly byte[] tableX = Convert.FromHexString("0000803f37a67f3ffe987e3f1fd97c3fe8677a3fed46773f7978733f19ff6e3f16de693f0919643fd0b35d3f25b3563fbd1b4f3f0af3463fc03e3e3ff704353f2b4c2b3f1e1b213f1479163f766d0b3f0000003f6171e83ed53fd03e037cb73e7a379e3eed83843ee2e6543e3c30203ec712d63d465e563d00000000465e56bdc712d6bd3c3020bee2e654beed8384be7a379ebe037cb7bed53fd0be6171e8be000000bf766d0bbf147916bf1e1b21bf2b4c2bbff70435bfc03e3ebf0af346bfbd1b4fbf25b356bfd0b35dbf091964bf16de69bf19ff6ebf797873bfed4677bfe8677abf1fd97cbffe987ebf37a67fbf000080bf37a67fbffe987ebf1fd97cbfe8677abfed4677bf797873bf19ff6ebf16de69bf091964bfd0b35dbf25b356bfbd1b4fbf0af346bfc03e3ebff70435bf2b4c2bbf1e1b21bf147916bf766d0bbf000000bf6171e8bed53fd0be037cb7be7a379ebeed8384bee2e654be3c3020bec712d6bd465e56bd00000080465e563dc712d63d3c30203ee2e6543eed83843e7a379e3e037cb73ed53fd03e6171e83e0000003f766d0b3f1479163f1e1b213f2b4c2b3ff704353fc03e3e3f0af3463fbd1b4f3f25b3563fd0b35d3f0919643f16de693f19ff6e3f7978733fed46773fe8677a3f1fd97c3ffe987e3f37a67f3f");

    public static RetailNpcApproachResult Replay(RetailNpcGroundApproachObservation observation, RetailNpcApproachLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var run = new RetailNpcApproachExecution(observation.Initial, observation.Sources, observation.RecordedSteps, observation.SyntheticFixture, limits);
        try
        {
            run.Validate();
            if (observation.CallerChooserReason == -3 && observation.ForceFlag != 1)
                Stop(RetailNpcApproachStatus.InvalidObservation, "The native reason-3 handoff requires force flag1.");
            return run.Result(Execute(run, observation.SquaredReach, observation.ForceFlag));
        }
        catch (RetailNpcApproachStop stop) { return run.Result(null, stop); }
    }

    internal static uint Execute(RetailNpcApproachExecution e, float reach, int force)
    {
        if (!float.IsFinite(reach) || reach < 0) Stop(RetailNpcApproachStatus.UnsupportedArithmetic, "Squared reach must be finite and nonnegative in this admitted domain.");
        var actor = e.Frame.Actor;
        var target = e.Frame.Target;
        if (target is null) Stop(RetailNpcApproachStatus.InvalidObservation, "Ground approach requires a target.");
        if (actor.GroundMotionFamily != 0 || target.GroundMotionFamily != 0)
            Stop(RetailNpcApproachStatus.UnsupportedMotion, "Only explicitly declared ordinary-ground source/target families are admitted.");
        RetailSearchPoint source = actor.Position, actual = target.Position;
        var cache = e.Frame.Cache;
        bool cached = cache.TargetId == target.Id && Near(actual, cache.TargetPosition);
        var cachedGoal = cache.ApproachPosition;
        var lead = new RetailSearchPoint(Finite(actual.X + target.RawVector.X * .667f), Finite(actual.Y + target.RawVector.Y * .667f),
            Finite(MathF.Max(actual.Z + target.RawVector.Z * .667f, actual.Z) + 1.5f));
        if (e.Frame.ActorWorld is null || e.Frame.TargetWorld is null || e.Frame.ActorWorld.WorldId != e.Frame.TargetWorld.WorldId) return 1;
        if (target.ObjectKind == 1 && e.Test(RetailNpcApproachOperation.TargetZone10, "TargetZone10"))
            return Complete(e, 1, lead, source, actual, reach, cached);
        float xy = Finite((lead.Y - actual.Y) * (lead.Y - actual.Y) + (lead.X - actual.X) * (lead.X - actual.X));
        if (xy <= .001f) lead = actual;
        else
        {
            lead = e.Project(lead, RetailNpcApproachOperation.PredictionProjection2480, "PredictionProjection", out bool found);
            if (!found || e.Geometric(RetailNpcApproachOperation.Segment2257e0, "PredictionSegmentBlocked", source, lead)) lead = actual;
        }
        if (cached && reach <= Squared(actual, cachedGoal)) cached = false;
        bool orientation = false;
        float rootReach = 0;
        bool zeroTolerance = force != 0;
        if (!cached)
        {
            bool pathException = false;
            if (reach > 0 && force == 0)
            {
                float body = Finite(target.HalfBound + actor.HalfBound + 3f);
                if (Finite(body * body) > reach && e.Raw(new(RetailNpcApproachOperation.StateCode7da70, "ApproachStateCode")) == 1 &&
                    unchecked((int)e.Raw(new(RetailNpcApproachOperation.TargetVirtual198, "TargetVirtual198"))) >= 2)
                {
                    bool special = target.ObjectKind == 2 && e.Test(RetailNpcApproachOperation.TargetSpecialFf950, "OrientationTargetSpecial");
                    if (!special)
                    {
                        orientation = true;
                        pathException = e.Test(RetailNpcApproachOperation.HasAnyPath, "OrientationHasAnyPath");
                    }
                }
            }
            if (force == 0 && Squared(actual, source) <= reach && !pathException) return 0;
            if (e.Test(RetailNpcApproachOperation.ActorVirtual14, "ApproachActorGuard")) return 1;
            if (e.Test(RetailNpcApproachOperation.ActorVirtual18, "ApproachActorMotion")) Stop(RetailNpcApproachStatus.UnsupportedMotion, "Native alternate-motion branch is outside this ground unit.");
            float oldZ = lead.Z;
            var projected = e.Project(lead with { Z = Finite(lead.Z + 2f) }, RetailNpcApproachOperation.GroundProjection2600, "GroundProjection", out bool projectedGround);
            if (projectedGround)
            {
                lead = projected;
                reach = AdjustVerticalBudget(reach, e.Frame.Actor.Position.Z, oldZ, projected.Z);
            }
            rootReach = MathF.Sqrt(reach);
            if (orientation && reach > 0 && !e.HasGoal(ref lead, "OrientationHasGoal"))
            {
                int direction = e.Frame.Actor.DirectionIndex;
                ValidateDirection(direction);
                float fraction = 1f;
                do
                {
                    var candidate = Offset(lead, fraction * rootReach, direction, Finite(lead.Z + 2f));
                    candidate = e.Project(candidate, RetailNpcApproachOperation.GroundProjection2600, "OrientationProjection", out bool found);
                    if (found && Squared(actual, candidate) <= reach &&
                        e.Geometric(RetailNpcApproachOperation.Shortcut225430, "OrientationShortcut", e.Frame.Actor.Position, candidate) &&
                        e.Geometric(RetailNpcApproachOperation.Visibility23b2f0, "OrientationVisibility", candidate, lead, 1))
                    { lead = candidate; zeroTolerance = true; break; }
                    fraction -= .1f;
                } while (fraction > .5f);
            }
        }
        uint reference = target.Id;
        if (target.ObjectKind == 2 && e.Test(RetailNpcApproachOperation.TargetSpecialFf950, "ReferenceTargetSpecial") && target.OwnerObjectIdentity is not null)
        {
            uint owner = e.Raw(new(RetailNpcApproachOperation.TargetOwner68, "ReferenceOwner"));
            if (owner != 0) reference = owner;
        }
        uint result = e.Goal(cached ? cachedGoal : lead, reference, cached || zeroTolerance ? 0 : reach, "PrimaryGoal");
        if ((result & 0xfffffffdu) == 0) e.FailureTick(0, "PrimaryGoalClearFailure");
        else if (unchecked(result - 4) > 1 && !cached)
        {
            if (reach < 1f) result = 1;
            else
            {
                float selectedReach = zeroTolerance ? reach : 4f;
                float margin = Finite(selectedReach - MathF.Max(selectedReach * .02f, .21f));
                float radius = MathF.Min(rootReach, 2f);
                float z = Finite(actual.Z + MathF.Max(MathF.Abs(actual.Z - source.Z) + 1f, 2f));
                uint start = e.Tick("AngularBudgetStart");
                var late = e.Frame.Actor.Position;
                int heading = MathF.Abs(actual.X - late.X) >= .05f || MathF.Abs(actual.Y - late.Y) >= .05f ? e.Heading("AngularHeading") : 0;
                for (int i = 0; i < 12; i++)
                {
                    if (e.Frame.BudgetEnabled != 0 && e.Frame.BudgetMilliseconds < unchecked(e.Tick("AngularBudgetCheck") - start))
                    { result = 5; e.Add(new(RetailNpcApproachCommandKind.Diagnostic, "AngularProcessingBudget")); break; }
                    int direction = (heading + i * 10) % 120;
                    var candidate = Offset(actual, radius * .75f, direction, z);
                    candidate = e.Project(candidate, RetailNpcApproachOperation.GroundProjection2600, "AngularOuterProjection", out bool found);
                    if (!found)
                    {
                        candidate = Offset(actual, 2f, direction, z);
                        candidate = e.Project(candidate, RetailNpcApproachOperation.GroundProjection2600, "AngularWideProjection", out found);
                        if (!found || Squared(actual, candidate) >= margin) continue;
                    }
                    else if (Squared(actual, candidate) >= margin)
                    {
                        candidate = Offset(actual, radius * .3751f, direction, z);
                        candidate = e.Project(candidate, RetailNpcApproachOperation.GroundProjection2600, "AngularInnerProjection", out found);
                        if (!found || Squared(actual, candidate) >= margin) continue;
                    }
                    if (!e.Geometric(RetailNpcApproachOperation.SurfaceLookup, "AngularSurface", candidate, candidate) ||
                        !e.Geometric(RetailNpcApproachOperation.Visibility23b2f0, "AngularVisibility", candidate, actual, 1)) continue;
                    result = e.Goal(candidate, reference, 0, "AngularGoal");
                    if ((result & 0xfffffffdu) == 0)
                    {
                        lead = candidate;
                        bool special = target.ObjectKind == 2 && e.Test(RetailNpcApproachOperation.TargetSpecialFf950, "AngularCacheTargetSpecial");
                        if (!special) e.Cache(new(target.Id, actual, candidate, 0), "AngularCache");
                        e.FailureTick(0, "AngularGoalClearFailure");
                        goto Finish;
                    }
                    if (result is 4 or 5) break;
                }
                if (e.Frame.History.Present && e.Frame.History.Enabled != 0 && target.ObjectKind == 1 &&
                    !e.Geometric(RetailNpcApproachOperation.SurfaceLookup, "ActualTargetSurface", actual, actual))
                {
                    float historyReach = MathF.Max((float)e.Frame.History.SquaredRangeFloor, reach);
                    var historical = e.History(actual, historyReach, target.Id);
                    if (historical is { } point)
                    {
                        result = e.Goal(point, target.Id, 0, "HistoryGoal");
                        if ((result & 0xfffffffdu) == 0)
                        {
                            lead = point; cached = true;
                            e.Cache(new(target.Id, actual, point, 1), "HistoryCache");
                            e.FailureTick(0, "HistoryGoalClearFailure");
                        }
                    }
                    e.Add(new(RetailNpcApproachCommandKind.Diagnostic, "TargetHistoryResult"));
                }
            }
        }
    Finish:
        return Complete(e, result, lead, source, actual, reach, cached);
    }

    private static uint Complete(RetailNpcApproachExecution e, uint result, RetailSearchPoint lead,
        RetailSearchPoint source, RetailSearchPoint actual, float reach, bool cached)
    {
        if (result is 0 or 4) { e.FailureTick(0, "ApproachClearFailure"); return result; }
        if (result == 5 || (result == 2 && (cached || Squared(actual, source) < reach))) return result;
        if (e.Frame.Actor.Field32c != 0x10)
        {
            uint now = e.Tick("ApproachFailureClock");
            if (e.TryFailureTick(now, "ApproachFailureStart") == 0) e.FailedGoal(lead, "ApproachFailedGoal");
            e.Add(new(RetailNpcApproachCommandKind.Diagnostic, "ApproachPathFailure", Point: lead));
        }
        return result;
    }

    internal static float AdjustVerticalBudget(float reach, float ownerZ, float previousZ, float projectedZ)
    {
        float currentDelta = ownerZ - projectedZ, previousDelta = ownerZ - previousZ, change = previousZ - projectedZ;
        if (!Near(currentDelta))
        {
            if (!Near(previousDelta)) reach = (reach - previousDelta * previousDelta) + currentDelta * currentDelta;
            else if (!Near(change)) reach += change * change;
        }
        else if (!Near(change)) reach -= change * change;
        reach = Finite(reach);
        return reach < 0 ? 0 : reach; // Native clamps only negative values; preserve negative zero.
    }
    private static RetailSearchPoint Offset(RetailSearchPoint origin, float radius, int direction, float z)
    {
        ValidateDirection(direction);
        float x = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(tableX.AsSpan(direction * 4, 4)));
        float y = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(tableY.AsSpan(direction * 4, 4)));
        return new(Finite(radius * x + origin.X), Finite(radius * y + origin.Y), z);
    }
    private static void ValidateDirection(int value)
    { if ((uint)value >= 120) Stop(RetailNpcApproachStatus.UnsupportedHeading, "Native direction table index must be observed in0..119."); }
}
