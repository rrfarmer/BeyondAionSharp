using System.Buffers.Binary;
using static Aion.GameServer.GeoEngine.RetailPath.RetailCollisionMath;

namespace Aion.GameServer.GeoEngine.RetailPath;

public enum RetailFieldObjectEvidenceKind { SyntheticFixture, SourceInitializationDeclaration, RuntimeObservationDeclaration }
public sealed record RetailFieldObjectClassReceipt(string ClassName, ulong Constructor, ulong Vtable,
    string NativeNpcSha256, string EvidenceSha256);
/// <summary>Explicit declarations of an instance object and its backing owner. None is a live-state observation performed by this unit.</summary>
public sealed record RetailFieldObjectObservation(uint EditorId, string ObjectRevision, int? RawState,
    string WorldIdentity, string InstanceIdentity, string SceneRevision, string AssetRevision,
    string StorageOwnerIdentity, string StorageOwnerRevision, string EvidenceSha256,
    RetailFieldObjectEvidenceKind EvidenceKind, RetailFieldObjectClassReceipt Class);
public sealed record RetailFieldObjectPrepareLimits(int MaxFaces, long MaxMetadataBytes);
public sealed record RetailFieldObjectGeometryResult(RetailCollisionStatus Status,
    RetailFieldObjectGeometry? Geometry, string? Detail, int ProcessedFaces, bool CosineEvidenceConsumed)
{
    public bool Success => Status == RetailCollisionStatus.Computed && Geometry is not null;
    public bool RuntimeVerified => false;
}

/// <summary>
/// Complete admitted DespawnableFieldObj/DoorBase range preparation and slots56..58.
/// Java GeoMap was read first. NPC factory22d160, vtable492f38, loader328790,
/// preprocess327590, bounds327a50/327a00, queries328640/3286e0/328860.
/// Catalog strings never select a class. Vertices already use world coordinates.
/// </summary>
public sealed class RetailFieldObjectGeometry
{
    public const ulong SupportedConstructor = 0x140228510;
    public const ulong SupportedVtable = 0x140492f38;
    public const string SupportedClass = "DespawnableFieldObj";
    private readonly byte[] eligible;
    private RetailFieldObjectGeometry(RetailCollisionPreparedData prepared, RetailFieldObjectObservation observation,
        RetailCollisionFieldRecord catalog, uint first, uint last, byte[] eligible,
        RetailSearchPoint minimum, RetailSearchPoint maximum, bool cosineConsumed, bool specialMaterial)
    {
        Prepared = prepared; Observation = observation; Catalog = catalog; FirstTriangle = first;
        LastTriangle = last; this.eligible = eligible; Minimum = minimum; Maximum = maximum;
        MinimumPruneZ = Subtract(minimum.Z, .001f); MaximumPruneZ = Add(maximum.Z, .001f);
        CosineEvidenceConsumed = cosineConsumed;
        SetsWorldSpecialMaterialFlag = specialMaterial;
    }
    public RetailCollisionPreparedData Prepared { get; }
    public RetailFieldObjectObservation Observation { get; }
    public RetailCollisionFieldRecord Catalog { get; }
    public uint FirstTriangle { get; }
    public uint LastTriangle { get; }
    public int FaceCount => eligible.Length;
    public ReadOnlyMemory<byte> EligibilityBytes => eligible;
    public RetailSearchPoint Minimum { get; }
    public RetailSearchPoint Maximum { get; }
    public float MinimumPruneZ { get; }
    public float MaximumPruneZ { get; }
    public bool CosineEvidenceConsumed { get; }
    public bool SetsWorldSpecialMaterialFlag { get; }
    public bool RuntimeVerified => false;
    public bool CurrentStateObservedByThisUnit => false;

    public static RetailFieldObjectGeometryResult Prepare(RetailCollisionPreparedData prepared,
        RetailFieldObjectObservation observation, RetailFieldObjectPrepareLimits limits)
    {
        int processed = 0; bool consumed = false, special = false;
        try
        {
            if (prepared is null || observation is null || limits is null || limits.MaxFaces < 1 || limits.MaxMetadataBytes < 1 ||
                !Text(observation.ObjectRevision) || !Text(observation.WorldIdentity) || !Text(observation.InstanceIdentity) ||
                !Text(observation.SceneRevision) || !Text(observation.AssetRevision) || !Text(observation.StorageOwnerIdentity) ||
                !Text(observation.StorageOwnerRevision) || !Digest(observation.EvidenceSha256) ||
                !Enum.IsDefined(observation.EvidenceKind))
                Stop(RetailCollisionStatus.InvalidInput, "An explicit class/state/instance/backing-owner declaration and positive managed budgets are required.");
            var cls = observation.Class;
            if (cls is null || cls.ClassName != SupportedClass || cls.Constructor != SupportedConstructor || cls.Vtable != SupportedVtable ||
                !Digest(cls.EvidenceSha256) || !string.Equals(cls.NativeNpcSha256, RetailGroundPathSearch.SupportedNpcBinarySha256, StringComparison.OrdinalIgnoreCase))
                Stop(RetailCollisionStatus.UnsupportedDynamicScene, "The actual constructor/vtable must bind the supported class; catalog kind is insufficient.");
            // Native lazy lookup stops at the first matching editor ID. Preserve duplicate file order.
            var catalog = prepared.Raw.Fields.FirstOrDefault(f => f.Id == observation.EditorId);
            if (catalog is null || catalog.Payload.Length < 16)
                Stop(RetailCollisionStatus.UnsupportedDynamicScene, "The selected class requires its first matching complete16-byte catalog payload.");
            uint first = BinaryPrimitives.ReadUInt32LittleEndian(catalog.Payload.Span);
            uint last = BinaryPrimitives.ReadUInt32LittleEndian(catalog.Payload.Span[4..]);
            long count = (long)last - first + 1;
            if (first > last || last >= prepared.Raw.TriangleCount)
                Stop(RetailCollisionStatus.UnsupportedDynamicScene, "The native inclusive class range is not backed by this mesh.");
            // Byte array plus conservative object metadata estimate; not a total allocation cap.
            if (count > int.MaxValue || count > limits.MaxFaces || count + 512 > limits.MaxMetadataBytes)
                Stop(RetailCollisionStatus.DefensiveLimit, "Field eligibility count/conservative metadata estimate exceeded.");
            byte[] eligible = new byte[(int)count];
            RetailSearchPoint minimum = default, maximum = default;
            for (int i = 0; i < eligible.Length; i++)
            {
                var face = prepared.Raw.GetTriangle(first + (uint)i);
                Point(face.A); Point(face.B); Point(face.C);
                if (i == 0) minimum = maximum = face.A;
                Include(face.A, ref minimum, ref maximum); Include(face.B, ref minimum, ref maximum); Include(face.C, ref minimum, ref maximum);
                special |= SpecialMaterial(face.Material);
                if (!SpecialMaterial(face.Material) && !ClassifiedMaterial(face.Material))
                {
                    var result = GroundEligibility(face, prepared.Cosine);
                    consumed |= result.CosineEvidenceConsumed;
                    if (result.Status != RetailCollisionStatus.Computed) Stop(result.Status, result.Detail ?? "Eligibility arithmetic unavailable.");
                    eligible[i] = result.Eligible == true ? (byte)1 : (byte)0;
                }
                processed++;
            }
            return new(RetailCollisionStatus.Computed,
                new(prepared, observation, catalog, first, last, eligible, minimum, maximum, consumed, special), null, processed, consumed);
        }
        catch (RetailCollisionStop stop) { return new(stop.Status, null, stop.Message, processed, consumed); }
    }

    private static void Include(RetailSearchPoint p, ref RetailSearchPoint low, ref RetailSearchPoint high)
    {
        low = new(p.X < low.X ? p.X : low.X, p.Y < low.Y ? p.Y : low.Y, p.Z < low.Z ? p.Z : low.Z);
        high = new(p.X > high.X ? p.X : high.X, p.Y > high.Y ? p.Y : high.Y, p.Z > high.Z ? p.Z : high.Z);
    }
    private bool Active()
    {
        if (Observation.RawState is null)
            Stop(RetailCollisionStatus.UnsupportedDynamicScene, "Consumed native state+0x200 is unknown; initialization is not current state.");
        return Observation.RawState.Value != 0;
    }
    internal bool Segment(RetailSearchPoint from, RetailSearchPoint delta, int view, bool contact,
        RetailCollisionMaterialPolicy? policy, Action<RetailCollisionTriangle> consume, out RetailSearchPoint? point)
    {
        point = null;
        if (!Active()) return false; // DoorBase tests actual state before its Z prune.
        float endZ = Add(from.Z, delta.Z);
        if (!((MinimumPruneZ <= from.Z || MinimumPruneZ <= endZ) && (from.Z <= MaximumPruneZ || endZ <= MaximumPruneZ))) return false;
        bool found = false; float distance = 0;
        for (int i = 0; i < eligible.Length; i++)
        {
            var face = Prepared.Raw.GetTriangle(FirstTriangle + (uint)i);
            if (policy is null) Stop(RetailCollisionStatus.MissingMaterialPolicy, "Active field segment consumes the explicit material column.");
            if (policy.Excludes(face.Material, view)) continue;
            consume(face);
            if (!Intersect(face.A, face.B, face.C, from, delta, false, out float t) || t < 0 || t > 1) continue;
            if (!contact) return true;
            var candidate = Along(from, delta, t); float candidateDistance = Squared(candidate, from);
            if (!found || candidateDistance < distance) { found = true; point = candidate; distance = candidateDistance; }
        }
        return found;
    }
    internal void Project(RetailSearchPoint input, bool onlyEligible, Action<RetailCollisionTriangle> consume,
        ref bool found, ref float score, ref RetailSearchPoint? best)
    {
        if (found && score <= Score(input.Z, MinimumPruneZ) && score <= Subtract(input.Z, MaximumPruneZ)) return;
        if (!Active()) return;
        // Eligibility storage is owned and always present even for flag0, as required by327fc0.
        var origin = new RetailSearchPoint(input.X, input.Y, 0); var delta = new RetailSearchPoint(0, 0, 1);
        for (int i = 0; i < eligible.Length; i++)
        {
            if (onlyEligible && eligible[i] == 0) continue;
            var face = Prepared.Raw.GetTriangle(FirstTriangle + (uint)i); consume(face);
            if (!Intersect(face.A, face.B, face.C, origin, delta, false, out float z)) continue;
            float candidate = Score(input.Z, z);
            if (!found || candidate < score) { found = true; score = candidate; best = new(input.X, input.Y, z); }
        }
    }
    private static float Score(float query, float z)
    {
        float d = Subtract(query, z);
        if (d < 0) { d = F(-d); if (.1f < d) d = Add(d, 10000f); }
        return d;
    }
}
