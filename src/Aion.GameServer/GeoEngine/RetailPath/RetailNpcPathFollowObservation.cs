using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Aion.GameServer.GeoEngine.RetailPath;

public enum RetailNpcPathFollowStatus
{
    Planned, InvalidInput, SourceIdentityMismatch, StaleSource, UnsupportedActor,
    UnsupportedProgress, UnsupportedArithmetic, UnsupportedGeometry, DefensiveLimit
}
public sealed record RetailNpcPathFollowEpoch(string ActorIdentity, string ActorRevision, string OwnershipEpoch,
    string PathRevision, string WorldIdentity, string InstanceIdentity, string SceneRevision, string AssetRevision);
public sealed record RetailNpcPathFollowPathReceipt(string SnapshotIdentity, string SnapshotSha256,
    string ProducerEvidenceSha256, string ProducerEvidence);
/// <summary>Current raw actor fields and method identity, not defaults from visual template bounds.</summary>
public sealed record RetailNpcPathFollowActor(ulong? Virtual18Target, uint? RawPose5a0, int? RawKind32c,
    float? UpperC8, int? Progress82c, bool? SerializedProgressOwnership,
    bool? NearestEvenNormalArithmetic, string EvidenceSha256);
public sealed record RetailNpcPathFollowLimits(int MaxPathPoints, int MaxWaypointVisits, int MaxQueries,
    int MaxTraceEntries, RetailCollisionQueryLimits QueryLimits);
public sealed record RetailNpcPathFollowOperation(int Sequence, string Operation, int? OldValue = null,
    int? NewValue = null, string? NativeAddress = null);
public sealed record RetailNpcPathGeometryCall(int Sequence, RetailCollisionQueryKind Kind,
    RetailSearchPoint Input, RetailSearchPoint? End, uint? IgnoredEditorId, float? ShortSegmentTolerance,
    RetailCollisionQueryResult Result);
public sealed record RetailNpcPathFollowResult(RetailNpcPathFollowStatus Status, uint? NativeValue,
    RetailSearchPoint? DesiredWaypoint, RetailSearchPoint? NextPosition, bool OutputsWritten,
    int? FinalPathIndex, int? FinalProgress, IReadOnlyList<RetailNpcPathFollowOperation> Operations,
    IReadOnlyList<RetailCollisionQueryResult> Queries, string? Detail,
    RetailNpcPathFollowEpoch? Epoch, RetailNpcPathFollowPathReceipt? PathReceipt)
{
    public bool HasNativeOutcome => Status == RetailNpcPathFollowStatus.Planned && NativeValue is not null;
    public bool RequiresAdapterRejectionInvalidation => !HasNativeOutcome;
    public bool ExecutesMovement => false;
    public bool RuntimeVerified => false;
    public bool ActivationReady => false;
    public bool AppliesCommands => false;
    public RetailCollisionStatus? GeometryFailure { get; init; }
    public RetailCollisionSources? GeometrySources { get; init; }
    public RetailCollisionSceneDeclaration? SceneDeclaration { get; init; }
    public RetailNpcPathFollowActor? ActorDeclaration { get; init; }
    public bool PathSnapshotBytesVerified { get; init; }
    public bool SyntheticFixture { get; init; }
    public IReadOnlyList<RetailNpcPathGeometryCall> GeometryCalls { get; init; } = Array.Empty<RetailNpcPathGeometryCall>();
}

/// <summary>
/// Owned immutable World::MoveByPath inputs. A point-snapshot SHA is checked against
/// actual header/metadata/point bytes; producer evidence remains an explicit declaration.
/// This is not proof that a route was searched, remains passable, or belongs to a live actor.
/// </summary>
public sealed class RetailNpcPathFollowObservation
{
    private readonly RetailSearchPoint[] points;
    private RetailNpcPathFollowObservation(RetailSearchPoint[] points, int count, int index,
        RetailSearchPoint metadata, RetailSearchPoint source, RetailSearchPoint target,
        float budget, float tolerance, uint ignoredEditorId, RetailNpcPathFollowActor actor,
        RetailNpcPathFollowEpoch epoch, RetailNpcPathFollowPathReceipt receipt, bool synthetic)
    {
        this.points = points; HeaderCount = count; HeaderIndex = index; DestinationMetadata = metadata;
        Source = source; Target = target; DistanceBudget = budget; SquaredStopTolerance = tolerance;
        IgnoredEditorId = ignoredEditorId; Actor = actor; Epoch = epoch; PathReceipt = receipt; SyntheticFixture = synthetic;
        Points = Array.AsReadOnly(points);
    }
    public IReadOnlyList<RetailSearchPoint> Points { get; }
    public int HeaderCount { get; }
    public int HeaderIndex { get; }
    public RetailSearchPoint DestinationMetadata { get; }
    public RetailSearchPoint Source { get; }
    public RetailSearchPoint Target { get; }
    public float DistanceBudget { get; }
    public float SquaredStopTolerance { get; }
    public uint IgnoredEditorId { get; }
    public RetailNpcPathFollowActor Actor { get; }
    public RetailNpcPathFollowEpoch Epoch { get; }
    public RetailNpcPathFollowPathReceipt PathReceipt { get; }
    public bool SyntheticFixture { get; }
    public bool RuntimeVerified => false;

    public static RetailNpcPathFollowObservation Create(IReadOnlyList<RetailSearchPoint> points,
        int headerCount, int headerIndex, RetailSearchPoint destinationMetadata,
        RetailSearchPoint source, RetailSearchPoint target, float distanceBudget, float squaredStopTolerance,
        uint ignoredEditorId, RetailNpcPathFollowActor actor, RetailNpcPathFollowEpoch epoch,
        RetailNpcPathFollowPathReceipt receipt, bool syntheticFixture, int maxOwnedPoints)
    {
        ArgumentNullException.ThrowIfNull(points); ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(epoch); ArgumentNullException.ThrowIfNull(receipt);
        int length = points.Count;
        if (maxOwnedPoints < 0 || length < 0 || length > maxOwnedPoints)
            throw new ArgumentOutOfRangeException(nameof(maxOwnedPoints), "Owned snapshot allocation limit; not a native path limit.");
        var owned = new RetailSearchPoint[length];
        for (int i = 0; i < length; i++) owned[i] = points[i];
        if (points.Count != length) throw new ArgumentException("Point source changed during its owned snapshot.", nameof(points));
        return new(owned, headerCount, headerIndex, destinationMetadata, source, target,
            distanceBudget, squaredStopTolerance, ignoredEditorId, actor, epoch, receipt, syntheticFixture);
    }

    /// <summary>
    /// Managed snapshot format: LE signed count/index/owned-length, metadata XYZ, then all point XYZ words.
    /// Native pointer bytes are excluded; this is not a native packet/header serialization.
    /// Unconsumed tails and signed zeros are retained without lexical/float normalization.
    /// </summary>
    public static string ComputeSnapshotSha256(IReadOnlyList<RetailSearchPoint> points, int count, int index,
        RetailSearchPoint metadata, int maxPoints)
    {
        ArgumentNullException.ThrowIfNull(points);
        int length = points.Count;
        if (maxPoints < 0 || length < 0 || length > maxPoints) throw new ArgumentOutOfRangeException(nameof(maxPoints));
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> header = stackalloc byte[24];
        BinaryPrimitives.WriteInt32LittleEndian(header, count); BinaryPrimitives.WriteInt32LittleEndian(header[4..], index);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], length); WritePoint(header[12..], metadata);
        hash.AppendData(header);
        Span<byte> bytes = stackalloc byte[12];
        for (int i = 0; i < length; i++) { WritePoint(bytes, points[i]); hash.AppendData(bytes); }
        if (points.Count != length) throw new ArgumentException("Point source changed during its hash.", nameof(points));
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
    internal bool SnapshotMatches() => string.Equals(PathReceipt.SnapshotSha256,
        ComputeSnapshotSha256(points, HeaderCount, HeaderIndex, DestinationMetadata, points.Length), StringComparison.OrdinalIgnoreCase);
    private static void WritePoint(Span<byte> output, RetailSearchPoint p)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(output, BitConverter.SingleToUInt32Bits(p.X));
        BinaryPrimitives.WriteUInt32LittleEndian(output[4..], BitConverter.SingleToUInt32Bits(p.Y));
        BinaryPrimitives.WriteUInt32LittleEndian(output[8..], BitConverter.SingleToUInt32Bits(p.Z));
    }
}
