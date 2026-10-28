namespace Aion.GameServer.GeoEngine.RetailPath;

public readonly record struct RetailSearchPoint(float X, float Y, float Z)
{
    public bool IsFinite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);
}
public readonly record struct RetailSearchWorldBounds(RetailSearchPoint Minimum, RetailSearchPoint Maximum)
{
    public bool Contains(RetailSearchPoint p) => p.X >= Minimum.X && p.X < Maximum.X &&
        p.Y >= Minimum.Y && p.Y < Maximum.Y && p.Z >= Minimum.Z && p.Z < Maximum.Z;
}
public enum RetailGroundQueueKind { LinkedList, FibonacciHeap }
public enum RetailSearchSurfaceState { Resolved, Missing, Ambiguous, Unsupported }
public sealed record RetailSearchSurfaceObservation(RetailSearchSurfaceState State, RetailPathSurfaceRef? Surface);
public readonly record struct RetailGroundActorObservation(bool NpcPresent, bool NativeVirtual14Rejects,
    bool NativeVirtual19Rejects, bool NativeVirtual18AlternateMotion, float UpperBoundMetres);

/// <summary>A caller-declared file identity. Search checks path/terrain bytes; it does not open or verify external files.</summary>
public sealed record RetailSearchAssetIdentity(string Identity, string Sha256, long ByteLength);
public sealed record RetailSearchMapAssetIdentity(RetailSearchAssetIdentity Asset, Guid? BuildGuid,
    int WorldWidthMetres, int WorldHeightMetres);
/// <summary>Opaque, explicit actor/dynamic/collision context receipts; none implies live verification or clear geometry.</summary>
public sealed record RetailSearchPolicyContext(string ActorIdentity, string DynamicContextIdentity,
    string CollisionContextIdentity, string Evidence);
public sealed record RetailSearchSourceReceipts(RetailPathData Path, RetailSearchMapAssetIdentity PathIdentity,
    RetailHeightMap40Data? Terrain, RetailSearchMapAssetIdentity? TerrainIdentity,
    RetailSearchAssetIdentity NativeNpcBinary, RetailSearchAssetIdentity? CollisionAsset,
    RetailSearchPolicyContext PolicyContext);
/// <summary>Only path/terrain byte and metadata checks are performed here. External identity declarations remain declarations.</summary>
public sealed record RetailSearchSourceValidation(bool PathBytesVerified, bool TerrainBytesVerified,
    bool NativeBinaryDeclarationMatchesPin)
{
    public bool RuntimeVerified => false;
    public bool CollisionBytesVerified => false;
}

/// <summary>Explicit, immutable replay inputs. No clock, actor, configured default, surface lookup or dynamic-state adapter is inferred.</summary>
public sealed record RetailGroundSearchObservation(
    RetailSearchPoint Start, RetailSearchPoint Goal, RetailSearchWorldBounds Bounds,
    RetailGroundActorObservation Actor, int NativeSelector, RetailGroundQueueKind Queue,
    int? ObservedHeapRankCapacity, bool NativePathTablesAvailable,
    RetailSearchSurfaceObservation StartSurface, RetailSearchSurfaceObservation GoalSurface,
    string PolicyRevision, RetailSearchSourceReceipts Sources, RetailPathSurfaceQueryOptions SurfaceQueryOptions,
    bool SyntheticFixture);

/// <summary>Additional support limits; native limits remain separately enforced. These are not allocation/latency guarantees.</summary>
public sealed record RetailSearchDefensiveLimits
{
    public int MaxNodes { get; init; } = 20_480;
    public int MaxPops { get; init; } = 49_999;
    public int MaxOutputPoints { get; init; } = 20_000;
    public int MaxPolicyCalls { get; init; } = 1_000_000;
    public int MaxShortcutSteps { get; init; } = 65_536;
    public int MaxHeapRankCapacity { get; init; } = 256;
}

public readonly record struct RetailSearchReplayStamp(bool Known, string Revision, string Evidence);
public readonly record struct RetailSearchCostObservation(RetailSearchReplayStamp Stamp, float Cost);
public readonly record struct RetailSearchBooleanObservation(RetailSearchReplayStamp Stamp, bool Value);
public readonly record struct RetailSearchSegmentObservation(RetailSearchReplayStamp Stamp, bool Blocked, RetailSearchPoint ClippedEndpoint);
public enum RetailSearchPolicyPurpose { DirectDistanceBranch, MissingStartFallback, InitialShortcut, Smoothing, BeforeSmoothing, AfterSmoothing }

/// <summary>
/// Explicit observations of native actor/policy consumers. Unknown/missing/revision-mismatched responses fail visibly.
/// CheckSegment represents the complete 0x1402257e0 result, including topology/actor/zone/master/ground clipping;
/// its blocked=false result requires a full requested endpoint. No implementation may silently assume clear geometry.
/// </summary>
public interface IRetailSearchPolicyReplay
{
    RetailSearchCostObservation TransitionCost(RetailPathSurfaceRef source, RetailPathSurfaceRef target);
    RetailSearchBooleanObservation SameNodeShortcutCapability(RetailPathNode node);
    RetailSearchSegmentObservation CheckSegment(RetailSearchPoint source, RetailSearchPoint target, RetailSearchPolicyPurpose purpose);
    RetailSearchBooleanObservation FieldObjectCollision(RetailSearchPoint source, RetailSearchPoint target, RetailSearchPolicyPurpose purpose);
}

public enum RetailGroundSearchStatus
{
    Completed, DirectOrClippedResult, NoRoute, InvalidObservation, AmbiguousSurface,
    UnsupportedSurface, UnsupportedSelector, UnsupportedActor, UnsupportedPolicyObservation,
    UnsupportedHeapCapacity, UnsupportedArithmetic, DefensiveResourceLimit, UnsupportedSourceIdentity
}
public enum RetailGroundSearchTermination
{
    None, RejectedBounds, EqualEndpoints, ActorGuard, MissingGoal, SegmentBlocked, NoProgress,
    QueueExhausted, NativePopLimit, NativeNodePoolLimit, NativePathPointLimit, DefensiveLimit,
    UnknownPolicy, SurfaceBoundary, UnsupportedMode, InvalidInput
}
public sealed record RetailSearchPolicyConsumption(int Sequence, string Operation, RetailSearchPolicyPurpose? Purpose,
    string Revision, string Evidence);
public sealed record RetailGroundSearchResult(RetailGroundSearchStatus Status, RetailGroundSearchTermination Termination,
    IReadOnlyList<RetailSearchPoint> Points, IReadOnlyList<RetailPathSurfaceRef> NodeChain,
    int AllocatedNodes, int PoppedNodes, int PolicyCalls, int DiagnosticFieldCollisions,
    IReadOnlyList<RetailSearchPolicyConsumption> ConsumedPolicy, string? Detail = null)
{
    public RetailSearchSourceReceipts? Sources { get; init; }
    public RetailSearchSourceValidation SourceValidation { get; init; } = new(false, false, false);
    public IReadOnlyList<string> SurfaceCompatibilityDiagnostics { get; init; } = Array.Empty<string>();
    public string PolicyRevision { get; init; } = "";
    public bool SyntheticFixture { get; init; }
    public bool Success => Status is RetailGroundSearchStatus.Completed or RetailGroundSearchStatus.DirectOrClippedResult;
    /// <summary>Transfer metadata only for replayed native outcomes. Unsupported/defensive outcomes are managed rejections, not native false results.</summary>
    public bool NativeCallerClearsPreviousPath => Success || Status == RetailGroundSearchStatus.NoRoute;
    /// <summary>A future adapter must explicitly invalidate its previous path when rejecting this observation; this unit mutates no actor.</summary>
    public bool RequiresAdapterRejectionInvalidation => !NativeCallerClearsPreviousPath;
}
