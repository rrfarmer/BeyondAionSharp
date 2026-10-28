using System.Security.Cryptography;

namespace Aion.GameServer.GeoEngine.RetailPath;

public sealed record RetailNpcApproachWorld(string Identity, uint WorldId, RetailSearchWorldBounds Bounds);
public sealed record RetailNpcApproachPath(string Identity, int Count, int Index, RetailSearchPoint Destination, bool Present);
public sealed record RetailNpcApproachCache(uint TargetId, RetailSearchPoint TargetPosition, RetailSearchPoint ApproachPosition, int Marker);
public sealed record RetailNpcApproachActor(uint Id, RetailSearchPoint Position, float HalfBound, float UpperBound,
    int DirectionIndex, int Field32c, int FieldB8, uint MasterId, int Field568, uint MovementEndTick, int GroundMotionFamily);
public sealed record RetailNpcApproachTarget(uint Id, RetailSearchPoint Position, RetailSearchPoint RawVector,
    float HalfBound, float UpperBound, int ObjectKind, int GroundMotionFamily, int FieldB8, string? OwnerObjectIdentity, int Heading);
public sealed record RetailNpcApproachHistoryPolicy(bool Present, int Enabled, int SquaredRangeFloor);

/// <summary>Raw immutable fields, including values reloaded after an observed native operation. Revisions are declarations, not live verification.</summary>
public sealed record RetailNpcApproachFrame(string Identity, string ActorRevision, string TargetRevision,
    string SceneRevision, string PathRevision, string Evidence, RetailNpcApproachActor Actor,
    RetailNpcApproachTarget? Target, RetailNpcApproachWorld? ActorWorld, RetailNpcApproachWorld? TargetWorld,
    RetailNpcApproachPath Path, RetailNpcApproachCache Cache, uint FailureTick, RetailSearchPoint FailedGoal,
    RetailNpcApproachHistoryPolicy History, int BudgetEnabled, uint BudgetMilliseconds);

public enum RetailNpcApproachOperation
{
    TargetZone10, PredictionProjection2480, Segment2257e0, StateCode7da70, TargetVirtual198,
    TargetSpecialFf950, HasAnyPath, ActorVirtual14, ActorVirtual18, GroundProjection2600,
    HasGoalPath, Shortcut225430, Visibility23b2f0, TargetOwner68, MoveToGoal,
    Clock, FallbackHeadingIndex, SurfaceLookup, TargetHistory, ActorVirtual22,
    TargetVirtualB0, TargetVectorZero, ChangeState, ChangeDirection, Dismiss, CompareExchangeFailureTick
}

/// <summary>Exact native-operation input. Float equality includes its bit pattern, including signed zero.</summary>
public sealed record RetailNpcApproachRequest(RetailNpcApproachOperation Operation, string Purpose,
    RetailSearchPoint? From = null, RetailSearchPoint? To = null, float? SquaredReach = null,
    int? Flag = null, uint? ReferenceId = null, uint? MovementFlags = null, int? ReuseFlag = null, uint? TickValue = null,
    int? Code = null, int? Reason = null)
{
    internal bool Matches(RetailNpcApproachRequest other) => Operation == other.Operation && Purpose == other.Purpose &&
        PointEqual(From, other.From) && PointEqual(To, other.To) && FloatEqual(SquaredReach, other.SquaredReach) &&
        Flag == other.Flag && ReferenceId == other.ReferenceId && MovementFlags == other.MovementFlags && ReuseFlag == other.ReuseFlag && TickValue == other.TickValue &&
        Code == other.Code && Reason == other.Reason;
    internal static bool PointEqual(RetailSearchPoint? a, RetailSearchPoint? b) => a.HasValue == b.HasValue &&
        (!a.HasValue || (Bits(a.Value.X, b!.Value.X) && Bits(a.Value.Y, b.Value.Y) && Bits(a.Value.Z, b.Value.Z)));
    private static bool FloatEqual(float? a, float? b) => a.HasValue == b.HasValue && (!a.HasValue || Bits(a.Value, b!.Value));
    private static bool Bits(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
}

public sealed record RetailNpcApproachReceipt(bool Known, string NativeBinarySha256, string FrameIdentity,
    string ActorRevision, string TargetRevision, string SceneRevision, string PathRevision, string Evidence);

/// <summary>
/// Declaration of a recorded complete native helper result, not an implementation of collision.
/// Geometry consumers require exact request binding, matched asset identities and a capture digest even in synthetic fixtures.
/// </summary>
public sealed record RetailNpcApproachGeometryReceipt(string NativeFunction, string PathSha256,
    string? TerrainSha256, string CollisionSha256, string CaptureSha256, bool CompleteHelperObserved);

public sealed class RetailNpcApproachHistorySnapshot
{
    public RetailNpcApproachHistorySnapshot(uint targetId, int cursor, uint count, IEnumerable<RetailSearchPoint> slots, string revision, string captureSha256)
    { TargetId = targetId; Cursor = cursor; Count = count; Slots = Array.AsReadOnly(slots.Take(21).ToArray()); Revision = revision; CaptureSha256 = captureSha256; }
    public uint TargetId { get; }
    public int Cursor { get; }
    public uint Count { get; }
    public IReadOnlyList<RetailSearchPoint> Slots { get; }
    public string Revision { get; }
    public string CaptureSha256 { get; }
}

public sealed record RetailNpcApproachRecordedStep(RetailNpcApproachRequest Request, RetailNpcApproachReceipt Receipt,
    uint? RawResult = null, RetailSearchPoint? OutputPoint = null, uint? Tick = null, int? HeadingIndex = null,
    RetailNpcApproachFrame? AfterFrame = null, RetailNpcApproachGeometryReceipt? Geometry = null,
    RetailNpcApproachHistorySnapshot? History = null);

public sealed record RetailNpcApproachLimits
{
    public int MaxRecordedOperations { get; init; } = 512;
    public int MaxCommands { get; init; } = 512;
}
/// <summary>Exactly one complete ordered branch transcript is required; extra operations are rejected, including after a native early return.</summary>
public sealed record RetailNpcGroundApproachObservation(RetailNpcApproachFrame Initial, float SquaredReach,
    int ForceFlag, int? CallerChooserReason, RetailSearchSourceReceipts Sources,
    IReadOnlyList<RetailNpcApproachRecordedStep> RecordedSteps, bool SyntheticFixture);
public sealed record RetailNpcFollowStateObservation(RetailNpcApproachFrame Initial, int RawEvent,
    bool StatePresent, uint StateEndTick, RetailSearchSourceReceipts Sources,
    IReadOnlyList<RetailNpcApproachRecordedStep> RecordedSteps, bool SyntheticFixture);

public enum RetailNpcApproachStatus { Replayed, InvalidObservation, UnsupportedMotion, UnsupportedEvidence, UnsupportedGeometry, UnsupportedHeading, UnsupportedArithmetic, DefensiveLimit }
public enum RetailNpcApproachCommandKind
{
    GoalRequest, CacheWrite, FailureTickWrite, FailedGoalWrite, ChangeState, ChangeDirection,
    ScheduleEvent, Dismiss, Remove, PopState, Diagnostic, MovementEndTickWrite
}
public sealed record RetailNpcApproachCommand(RetailNpcApproachCommandKind Kind, string Purpose,
    RetailNpcApproachRequest? Request = null, RetailSearchPoint? Point = null, uint? Tick = null,
    RetailNpcApproachCache? Cache = null, int? Code = null, int? DelayMilliseconds = null, string? LockScope = null);
public sealed record RetailNpcApproachConsumption(int Ordinal, RetailNpcApproachRequest Request,
    RetailNpcApproachReceipt Receipt, uint? RawResult, string AfterFrameIdentity);
/// <summary>FinalFrame is a diagnostic replay snapshot. Rejected replay returns no applicable commands or native result.</summary>
public sealed record RetailNpcApproachResult(RetailNpcApproachStatus Status, uint? NativeResult,
    IReadOnlyList<RetailNpcApproachCommand> Commands, IReadOnlyList<RetailNpcApproachConsumption> Consumed,
    RetailNpcApproachFrame? FinalFrame, bool SyntheticFixture, string? Detail = null)
{
    public bool RuntimeVerified => false;
    public bool ImplementsCollision => false;
    public bool RequiresAdapterRejectionInvalidation => Status != RetailNpcApproachStatus.Replayed;
    public bool ExecutesCommands => false;
}

internal sealed class RetailNpcApproachStop(RetailNpcApproachStatus status, string detail) : Exception(detail)
{ internal RetailNpcApproachStatus Status { get; } = status; }

/// <summary>Shared private replay cursor; native operations are data records and no callback, clock, scheduler or actor is invoked.</summary>
internal sealed class RetailNpcApproachExecution
{
    private readonly RetailNpcApproachRecordedStep[] steps;
    private readonly int providedStepCount;
    private readonly RetailNpcApproachLimits limits;
    private readonly List<RetailNpcApproachCommand> commands = [];
    private readonly List<RetailNpcApproachConsumption> consumed = [];
    internal RetailNpcApproachFrame Frame { get; private set; }
    internal RetailSearchSourceReceipts Sources { get; }
    private readonly bool synthetic;
    internal int Cursor { get; private set; }

    internal RetailNpcApproachExecution(RetailNpcApproachFrame initial, RetailSearchSourceReceipts sources,
        IReadOnlyList<RetailNpcApproachRecordedStep> recorded, bool synthetic, RetailNpcApproachLimits? limits)
    {
        Frame = initial; Sources = sources; this.synthetic = synthetic; this.limits = limits ?? new();
        providedStepCount = recorded?.Count ?? -1;
        steps = recorded is null || this.limits.MaxRecordedOperations < 1 ? [] : recorded.Take(System.Math.Min(recorded.Count, this.limits.MaxRecordedOperations)).ToArray();
    }

    internal void Validate()
    {
        if (limits.MaxRecordedOperations < 1 || limits.MaxCommands < 1 || Sources is null || Frame is null || providedStepCount < 0)
            Stop(RetailNpcApproachStatus.InvalidObservation, "Missing observation or invalid managed count limits.");
        if (providedStepCount > limits.MaxRecordedOperations)
            Stop(RetailNpcApproachStatus.DefensiveLimit, "The complete provided transcript exceeds the managed recorded-operation limit; no tail is silently truncated.");
        ValidateFrame(Frame);
        if (Sources.Path is null || Sources.PathIdentity is null || Sources.NativeNpcBinary is null || Sources.PolicyContext is null ||
            !Asset(Sources.PathIdentity.Asset) || !Asset(Sources.NativeNpcBinary) || !Asset(Sources.CollisionAsset) ||
            !Text(Sources.PolicyContext.ActorIdentity) || !Text(Sources.PolicyContext.DynamicContextIdentity) ||
            !Text(Sources.PolicyContext.CollisionContextIdentity) || !Text(Sources.PolicyContext.Evidence))
            Stop(RetailNpcApproachStatus.InvalidObservation, "Individual path, collision, pinned binary and context declarations are required.");
        if (!Equal(Sources.NativeNpcBinary.Sha256, RetailGroundPathSearch.SupportedNpcBinarySha256) ||
            Sources.NativeNpcBinary.ByteLength != RetailGroundPathSearch.SupportedNpcBinaryBytes ||
            !MapMatches(Sources.PathIdentity, Sources.Path.RawFile, Sources.Path.BuildGuid, Sources.Path.WorldWidthMetres, Sources.Path.WorldHeightMetres))
            Stop(RetailNpcApproachStatus.UnsupportedEvidence, "Pinned binary declaration or loaded path identity mismatch.");
        if ((Sources.Terrain is null) != (Sources.TerrainIdentity is null)) Stop(RetailNpcApproachStatus.UnsupportedEvidence, "Terrain and its identity must be supplied together.");
        if (Sources.Terrain is { } terrain && (Sources.TerrainIdentity is null || !Asset(Sources.TerrainIdentity.Asset) ||
            !MapMatches(Sources.TerrainIdentity, terrain.RawFile, terrain.BuildGuid, terrain.WorldWidthMetres, terrain.WorldHeightMetres)))
            Stop(RetailNpcApproachStatus.UnsupportedEvidence, "Loaded terrain identity mismatch.");
    }

    internal RetailNpcApproachRecordedStep Read(RetailNpcApproachRequest request, bool advancesFrame = false)
    {
        if (Cursor >= limits.MaxRecordedOperations) Stop(RetailNpcApproachStatus.DefensiveLimit, "Managed recorded-operation limit.");
        if (Cursor >= steps.Length) Stop(RetailNpcApproachStatus.UnsupportedEvidence, $"Missing recorded {request.Operation}/{request.Purpose}.");
        var step = steps[Cursor++];
        if (step is null || step.Request is null || !step.Request.Matches(request) || !ReceiptMatches(step.Receipt))
            Stop(RetailNpcApproachStatus.UnsupportedEvidence, $"Mismatched request, source or revision for {request.Operation}/{request.Purpose}.");
        if (GeometryFunction(request.Operation) is { } function)
        {
            var proof = step.Geometry;
            if (proof is null || !proof.CompleteHelperObserved || !Equal(proof.NativeFunction, function) ||
                !Equal(proof.PathSha256, Sources.PathIdentity.Asset.Sha256) ||
                !Equal(proof.TerrainSha256, Sources.TerrainIdentity?.Asset.Sha256) ||
                !Equal(proof.CollisionSha256, Sources.CollisionAsset!.Sha256) || !Digest(proof.CaptureSha256))
                Stop(RetailNpcApproachStatus.UnsupportedGeometry, "Complete source-bound recorded helper evidence is required; no clear-geometry default.");
        }
        if (advancesFrame)
        {
            if (step.AfterFrame is null) Stop(RetailNpcApproachStatus.UnsupportedEvidence, "Missing explicit post-operation frame.");
            var after = step.AfterFrame!;
            ValidateFrame(after);
            if (after.Actor.Id != Frame.Actor.Id || after.Target?.Id != Frame.Target?.Id ||
                after.SceneRevision != Frame.SceneRevision || after.TargetRevision != Frame.TargetRevision || after.Target != Frame.Target ||
                after.ActorWorld != Frame.ActorWorld || after.TargetWorld != Frame.TargetWorld)
                Stop(RetailNpcApproachStatus.UnsupportedEvidence, "Actor/target/scene changed outside the admitted serialized request domain.");
            Frame = after;
        }
        else if (step.AfterFrame is not null) Stop(RetailNpcApproachStatus.UnsupportedEvidence, "Unexpected mutation frame for an observed pure query.");
        consumed.Add(new(Cursor, request, step.Receipt, step.RawResult, Frame.Identity));
        return step;
    }

    internal uint Raw(RetailNpcApproachRequest request)
    { var result = Read(request).RawResult; return result ?? throw new RetailNpcApproachStop(RetailNpcApproachStatus.UnsupportedEvidence, "Missing raw native result."); }
    internal bool Test(RetailNpcApproachOperation operation, string purpose)
    {
        bool value = Raw(new(operation, purpose)) != 0;
        if (operation == RetailNpcApproachOperation.HasAnyPath && value != (Frame.Path.Present && Frame.Path.Index < Frame.Path.Count))
            Stop(RetailNpcApproachStatus.UnsupportedEvidence, "HasPath() contradicts its bound path snapshot.");
        if (operation == RetailNpcApproachOperation.TargetVectorZero)
        {
            var vector = Frame.Target?.RawVector;
            if (vector is null || value != (Near(vector.Value.X) && Near(vector.Value.Y) && Near(vector.Value.Z)))
                Stop(RetailNpcApproachStatus.UnsupportedEvidence, "Vector-zero observation contradicts its bound raw components.");
        }
        return value;
    }
    internal uint Tick(string purpose)
    { var result = Read(new(RetailNpcApproachOperation.Clock, purpose), true).Tick; return result ?? throw new RetailNpcApproachStop(RetailNpcApproachStatus.UnsupportedEvidence, "Missing uint32 clock observation."); }
    internal RetailSearchPoint Project(RetailSearchPoint point, RetailNpcApproachOperation operation, string purpose, out bool found)
    {
        var result = Read(new(operation, purpose, point, point));
        found = RequireRaw(result) != 0;
        var output = RequirePoint(result);
        return found ? output : point;
    }
    internal bool HasGoal(ref RetailSearchPoint goal, string purpose)
    {
        var result = Read(new(RetailNpcApproachOperation.HasGoalPath, purpose, To: goal));
        bool found = RequireRaw(result) != 0;
        var output = RequirePoint(result);
        bool expected = Frame.Path.Present && Frame.Path.Index < Frame.Path.Count && Near(goal, Frame.Path.Destination) && !Near(Frame.Actor.Position, goal);
        if (found != expected || (found && !RetailNpcApproachRequest.PointEqual(output, Frame.Path.Destination)))
            Stop(RetailNpcApproachStatus.UnsupportedEvidence, "HasPath(goal) contradicts the bound path identity/destination/index.");
        if (!found && !RetailNpcApproachRequest.PointEqual(output, goal)) Stop(RetailNpcApproachStatus.UnsupportedEvidence, "Failed HasPath(goal) cannot canonicalize the point.");
        if (found) goal = output;
        return found;
    }
    internal bool Geometric(RetailNpcApproachOperation operation, string purpose, RetailSearchPoint from, RetailSearchPoint to, int? flag = null) =>
        Raw(new(operation, purpose, from, to, Flag: flag)) != 0;
    internal uint Goal(RetailSearchPoint point, uint reference, float reach, string purpose)
    {
        var request = new RetailNpcApproachRequest(RetailNpcApproachOperation.MoveToGoal, purpose,
            To: point, SquaredReach: reach, ReferenceId: reference, MovementFlags: 2, ReuseFlag: 1);
        Add(new(RetailNpcApproachCommandKind.GoalRequest, purpose, Request: request));
        return RequireRaw(Read(request, true));
    }
    internal int Heading(string purpose)
    {
        var result = Read(new(RetailNpcApproachOperation.FallbackHeadingIndex, purpose)).HeadingIndex;
        if (result is not >= 0 or >= 120) Stop(RetailNpcApproachStatus.UnsupportedHeading, "A recorded CRT/narrowing-derived heading index0..119 is required.");
        return result.Value;
    }
    internal void Add(RetailNpcApproachCommand command)
    {
        if (commands.Count >= limits.MaxCommands) Stop(RetailNpcApproachStatus.DefensiveLimit, "Managed command count limit.");
        commands.Add(command);
    }
    internal void FailureTick(uint value, string purpose)
    { Add(new(RetailNpcApproachCommandKind.FailureTickWrite, purpose, Tick: value)); Frame = Frame with { FailureTick = value }; }
    internal void FailedGoal(RetailSearchPoint point, string purpose)
    { Add(new(RetailNpcApproachCommandKind.FailedGoalWrite, purpose, Point: point)); Frame = Frame with { FailedGoal = point }; }
    internal void MovementEnd(uint tick)
    {
        Add(new(RetailNpcApproachCommandKind.MovementEndTickWrite, "FollowRetryMovementEnd", Tick: tick));
        Frame = Frame with { Actor = Frame.Actor with { MovementEndTick = tick } };
    }
    internal void Cache(RetailNpcApproachCache value, string purpose)
    { Add(new(RetailNpcApproachCommandKind.CacheWrite, purpose, Cache: value, LockScope: "NPC+0x120")); Frame = Frame with { Cache = value }; }
    internal uint TryFailureTick(uint value, string purpose)
    {
        // Late callback writes belong in an explicit preceding stage. Unobserved concurrent writers are outside this replay domain.
        uint before = Frame.FailureTick;
        var step = Read(new(RetailNpcApproachOperation.CompareExchangeFailureTick, purpose, TickValue: value), true);
        uint old = RequireRaw(step);
        if (old != before) Stop(RetailNpcApproachStatus.UnsupportedEvidence, "CMPXCHG returned-old differs from the fresh pre-operation failure tick in this serialized replay domain.");
        if (Frame.FailureTick != (old == 0 ? value : old)) Stop(RetailNpcApproachStatus.UnsupportedEvidence, "Incoherent post-CMPXCHG failure tick.");
        if (old == 0) Add(new(RetailNpcApproachCommandKind.FailureTickWrite, purpose, Tick: value, LockScope: "CMPXCHG NPC+0xa18"));
        return old;
    }
    internal RetailSearchPoint? History(RetailSearchPoint target, float radius, uint targetId)
    {
        var step = Read(new(RetailNpcApproachOperation.TargetHistory, "TargetHistory", To: target, SquaredReach: radius, ReferenceId: targetId));
        var history = step.History;
        if (history is null || history.TargetId != targetId || history.Slots.Count != 20 || history.Count > 20 ||
            history.Cursor < -1 || history.Cursor >= 20 || !Text(history.Revision) || !Digest(history.CaptureSha256))
            Stop(RetailNpcApproachStatus.UnsupportedEvidence, "Bounded20-slot target-history snapshot and identity are required.");
        int index = history.Cursor;
        for (uint i = 0; i < history.Count; i++, index--)
        {
            int actual = index < 0 ? index + 20 : index;
            if ((uint)actual >= 20) Stop(RetailNpcApproachStatus.UnsupportedEvidence, "Unsupported history cursor/count arithmetic.");
            var point = history.Slots[actual];
            if (!point.IsFinite) Stop(RetailNpcApproachStatus.UnsupportedArithmetic, "Nonfinite consumed history point.");
            if (Squared(point, target) < radius) return point;
        }
        return null;
    }
    internal RetailNpcApproachResult Result(uint? native, RetailNpcApproachStop? error = null)
    {
        if (error is null && Cursor != providedStepCount)
            error = new(RetailNpcApproachStatus.UnsupportedEvidence, "Unused recorded operations remain after the native branch ended; the entire transcript must be consumed.");
        return new(error?.Status ?? RetailNpcApproachStatus.Replayed, error is null ? native : null,
            Array.AsReadOnly(error is null ? commands.ToArray() : []), Array.AsReadOnly(consumed.ToArray()), Frame, synthetic, error?.Message);
    }

    internal static uint RequireRaw(RetailNpcApproachRecordedStep step) => step.RawResult ?? throw new RetailNpcApproachStop(RetailNpcApproachStatus.UnsupportedEvidence, "Missing raw result.");
    internal static RetailSearchPoint RequirePoint(RetailNpcApproachRecordedStep step)
    {
        if (step.OutputPoint is null || !step.OutputPoint.Value.IsFinite) Stop(RetailNpcApproachStatus.UnsupportedEvidence, "Missing/nonfinite recorded in/out point.");
        var point = step.OutputPoint!.Value;
        return point;
    }
    internal static bool Near(RetailSearchPoint a, RetailSearchPoint b) => Near(a.X - b.X) && Near(a.Y - b.Y) && Near(a.Z - b.Z);
    internal static bool Near(float difference) => difference < .0001f && difference > -.0001f;
    internal static float Squared(RetailSearchPoint a, RetailSearchPoint b)
    { float x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z; return Finite((y * y + x * x) + z * z); }
    internal static float Finite(float value)
    { if (!float.IsFinite(value)) Stop(RetailNpcApproachStatus.UnsupportedArithmetic, "Nonfinite binary32 arithmetic."); return value; }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    internal static void Stop(RetailNpcApproachStatus status, string detail) => throw new RetailNpcApproachStop(status, detail);

    private bool ReceiptMatches(RetailNpcApproachReceipt? r) => r is not null && r.Known &&
        Equal(r.NativeBinarySha256, RetailGroundPathSearch.SupportedNpcBinarySha256) && r.FrameIdentity == Frame.Identity &&
        r.ActorRevision == Frame.ActorRevision && r.TargetRevision == Frame.TargetRevision &&
        r.SceneRevision == Frame.SceneRevision && r.PathRevision == Frame.PathRevision && Text(r.Evidence);
    private static string? GeometryFunction(RetailNpcApproachOperation operation) => operation switch
    {
        RetailNpcApproachOperation.PredictionProjection2480 => "0x1402f2480", RetailNpcApproachOperation.GroundProjection2600 => "0x1402f2600",
        RetailNpcApproachOperation.Segment2257e0 => "0x1402257e0", RetailNpcApproachOperation.Shortcut225430 => "0x140225430",
        RetailNpcApproachOperation.Visibility23b2f0 => "0x14023b2f0", RetailNpcApproachOperation.SurfaceLookup => "0x1400fa8d0", _ => null
    };
    private static void ValidateFrame(RetailNpcApproachFrame f)
    {
        if (!Text(f.Identity) || !Text(f.ActorRevision) || !Text(f.TargetRevision) || !Text(f.SceneRevision) || !Text(f.PathRevision) || !Text(f.Evidence) ||
            f.Actor is null || f.Path is null || f.Cache is null || f.History is null || !f.Actor.Position.IsFinite ||
            !float.IsFinite(f.Actor.HalfBound) || f.Actor.HalfBound < 0 || !float.IsFinite(f.Actor.UpperBound) || f.Actor.UpperBound < 0 ||
            !f.Cache.TargetPosition.IsFinite || !f.Cache.ApproachPosition.IsFinite || !f.FailedGoal.IsFinite || !f.Path.Destination.IsFinite || !Text(f.Path.Identity) ||
            f.Path.Count < 0 || f.Path.Index < 0 || f.Path.Index > f.Path.Count)
            Stop(RetailNpcApproachStatus.InvalidObservation, "Invalid explicit frame/raw field domain.");
        if (f.Target is { } target && (!target.Position.IsFinite || !target.RawVector.IsFinite || !float.IsFinite(target.HalfBound) || target.HalfBound < 0 ||
            !float.IsFinite(target.UpperBound) || target.UpperBound < 0 || (target.OwnerObjectIdentity is not null && !Text(target.OwnerObjectIdentity))))
            Stop(RetailNpcApproachStatus.InvalidObservation, "Invalid target shape/transform/owner identity.");
        foreach (var world in new[] { f.ActorWorld, f.TargetWorld }) if (world is not null && (!Text(world.Identity) || !world.Bounds.Minimum.IsFinite || !world.Bounds.Maximum.IsFinite ||
            world.Bounds.Minimum.X >= world.Bounds.Maximum.X || world.Bounds.Minimum.Y >= world.Bounds.Maximum.Y || world.Bounds.Minimum.Z >= world.Bounds.Maximum.Z))
            Stop(RetailNpcApproachStatus.InvalidObservation, "Invalid explicit world identity/bounds.");
    }
    private static bool Text(string? s) => !string.IsNullOrWhiteSpace(s);
    private static bool Equal(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static bool Digest(string? s) => s is { Length: 64 } && s.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    private static bool Asset(RetailSearchAssetIdentity? a) => a is not null && Text(a.Identity) && a.ByteLength >= 0 && Digest(a.Sha256);
    private static bool MapMatches(RetailSearchMapAssetIdentity identity, ReadOnlyMemory<byte> bytes, Guid? guid, int width, int height) =>
        identity.Asset.ByteLength == bytes.Length && identity.BuildGuid == guid && identity.WorldWidthMetres == width && identity.WorldHeightMetres == height &&
        Equal(Convert.ToHexString(SHA256.HashData(bytes.Span)), identity.Asset.Sha256);
}
