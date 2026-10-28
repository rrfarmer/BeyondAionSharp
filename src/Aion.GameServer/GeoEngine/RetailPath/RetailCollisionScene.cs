using System.Buffers.Binary;
using System.Security.Cryptography;
using static Aion.GameServer.GeoEngine.RetailPath.RetailCollisionMath;

namespace Aion.GameServer.GeoEngine.RetailPath;

public enum RetailCollisionPolicySource { PinnedFileImageDeclaration, RecordedRuntimeDeclaration }

/// <summary>Six int32 material columns, explicitly supplied. Matching file-image bytes does not establish initialized runtime policy.</summary>
public sealed class RetailCollisionMaterialPolicy
{
    public const string PinnedSha256 = "bc5f0dbdc9a4221913b3dbc5dbe8568f33d0d6ebef594fa94dbb9d084a81db76";
    public const int PinnedByteLength = 6144;
    private static readonly byte[] image = MakeImage();
    private readonly byte[] bytes;
    private readonly string byteSha256;
    public RetailCollisionMaterialPolicy(ReadOnlySpan<byte> bytes, string nativeBinarySha256, string revision,
        string evidenceSha256, RetailCollisionPolicySource source, bool known)
    {
        DeclaredByteLength = bytes.Length;
        this.bytes = bytes[..System.Math.Min(bytes.Length, PinnedByteLength + 1)].ToArray();
        byteSha256 = Convert.ToHexString(SHA256.HashData(this.bytes));
        NativeBinarySha256 = nativeBinarySha256; Revision = revision; EvidenceSha256 = evidenceSha256; Source = source; Known = known;
    }
    public int DeclaredByteLength { get; }
    public ReadOnlyMemory<byte> Bytes => bytes;
    public string NativeBinarySha256 { get; }
    public string Revision { get; }
    public string EvidenceSha256 { get; }
    public RetailCollisionPolicySource Source { get; }
    public bool Known { get; }
    public bool RuntimeVerified => false;
    public static ReadOnlySpan<byte> PinnedFileImageBytes => image;
    public static RetailCollisionMaterialPolicy FromPinnedFileImage(string revision) => new(image,
        RetailGroundPathSearch.SupportedNpcBinarySha256, revision, PinnedSha256, RetailCollisionPolicySource.PinnedFileImageDeclaration, true);
    internal bool Excludes(byte material, int view)
    {
        if (!Known || DeclaredByteLength != PinnedByteLength || !Text(Revision) || !Digest(EvidenceSha256) ||
            !string.Equals(NativeBinarySha256, RetailGroundPathSearch.SupportedNpcBinarySha256, StringComparison.OrdinalIgnoreCase) ||
            Source is not RetailCollisionPolicySource.PinnedFileImageDeclaration and not RetailCollisionPolicySource.RecordedRuntimeDeclaration ||
            !string.Equals(byteSha256, PinnedSha256, StringComparison.OrdinalIgnoreCase))
            Stop(RetailCollisionStatus.MissingMaterialPolicy, "Complete source-bound six-column policy bytes must match the supported pin; no clear-geometry fallback.");
        if ((uint)view >= 6) Stop(RetailCollisionStatus.UnsupportedFormat, "Consumed material view is outside the checked six-column table.");
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan((material * 6 + view) * 4)) != 0;
    }
    private static byte[] MakeImage()
    {
        byte[] output = new byte[PinnedByteLength];
        // Exact first17 PE rows; every remaining byte in the checked6144-byte image is zero.
        int[][] rows = [
            [0,0,0,0,0,1],[0,1,1,1,1,1],[0,0,1,1,1,1],[0,0,0,1,1,1],
            [0,0,0,0,1,1],[0,0,0,0,0,1],[1,0,1,1,1,1],[1,0,0,1,1,1],
            [1,0,0,0,1,1],[1,0,0,0,0,1],[1,0,0,0,0,0],[1,1,1,1,1,1],
            [1,0,0,0,0,0],[1,1,1,1,1,1],[1,1,1,1,1,1],[1,1,1,1,1,1],[1,1,1,1,1,1]
        ];
        for (int row = 0; row < rows.Length; row++) for (int col = 0; col < 6; col++)
            BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan((row * 6 + col) * 4), rows[row][col]);
        return output;
    }
}

public sealed record RetailCollisionSources(RetailSearchMapAssetIdentity Mesh, RetailSearchMapAssetIdentity Collision,
    RetailSearchMapAssetIdentity HeightMap40, RetailSearchAssetIdentity NativeNpcBinary);
public sealed record RetailCollisionSceneDeclaration(string WorldIdentity, string InstanceIdentity,
    string SceneRevision, string AssetRevision, string EvidenceSha256, string Evidence, bool SyntheticFixture);
public sealed record RetailCollisionSceneObject(uint ObjectId, string ClassIdentity, string ObjectRevision);
public sealed record RetailCollisionRegionObservation(int RegionX, int RegionY, bool CoverageKnown,
    string SceneRevision, string CaptureSha256, IReadOnlyList<RetailCollisionSceneObject> OrderedObjects);
public sealed record RetailCollisionSceneLimits(int MaxRegionRecords, int MaxObjectRecords);
public sealed record RetailCollisionSceneResult(RetailCollisionStatus Status, RetailCollisionScene? Scene, string? Detail)
{
    public bool Success => Status == RetailCollisionStatus.Computed && Scene is not null;
    public bool RuntimeVerified => false;
}
public sealed record RetailCollisionQueryStamp(string InstanceIdentity, string SceneRevision, string AssetRevision);
public sealed record RetailCollisionQueryLimits(int MaxVisitedCells, int MaxBands, int MaxTriangles,
    int MaxSceneObjects, int MaxTraceEntries);
public enum RetailCollisionTraceKind { Cell, Band, TerrainTriangle, MeshTriangle, SceneObject, NativeGuard }
public sealed record RetailCollisionTrace(RetailCollisionTraceKind Kind, int CellX, int CellY,
    byte? Selector = null, string? NativeFunction = null, uint? TriangleId = null, int? FileOffset = null, uint? ObjectId = null);
public sealed record RetailCollisionQueryResult(RetailCollisionStatus Status, uint? NativeResult,
    RetailSearchPoint? Point, IReadOnlyList<RetailCollisionTrace> Trace, int VisitedCells, int Bands, int Triangles,
    int SceneObjects, RetailCollisionSceneDeclaration? Declaration, string? Detail = null,
    RetailCollisionSources? Sources = null, RetailCollisionCosineReceipt? CosineReceipt = null,
    RetailCollisionMaterialPolicy? MaterialPolicy = null)
{
    public IReadOnlyList<RetailFieldObjectObservation> ConsumedFieldObjects { get; init; } = Array.Empty<RetailFieldObjectObservation>();
    public bool RuntimeVerified => false;
    public bool ExecutesCommands => false;
    public bool AppliesActorClearance => false;
    public bool NativeTargetExecuted => false;
}

/// <summary>Owned immutable scene records. Catalog entries and missing region keys never imply absent live field objects.</summary>
public sealed class RetailCollisionScene
{
    private readonly Dictionary<(int X, int Y), RetailCollisionRegionObservation> regions;
    private readonly Dictionary<uint, RetailFieldObjectGeometry> fieldObjects;
    private RetailCollisionScene(RetailCollisionPreparedData prepared, RetailHeightMap40Data terrain, RetailCollisionSources sources,
        RetailSearchWorldBounds bounds, RetailCollisionSceneDeclaration declaration,
        Dictionary<(int X, int Y), RetailCollisionRegionObservation> regions, RetailCollisionMaterialPolicy? policy,
        Dictionary<uint, RetailFieldObjectGeometry> fieldObjects)
    { Prepared = prepared; Terrain = terrain; Sources = sources; Bounds = bounds; Declaration = declaration; this.regions = regions; MaterialPolicy = policy; this.fieldObjects = fieldObjects; }
    public RetailCollisionPreparedData Prepared { get; }
    public RetailHeightMap40Data Terrain { get; }
    public RetailCollisionSources Sources { get; }
    public RetailSearchWorldBounds Bounds { get; }
    public RetailCollisionSceneDeclaration Declaration { get; }
    public RetailCollisionMaterialPolicy? MaterialPolicy { get; }
    public bool RuntimeVerified => false;
    public RetailCollisionQueryStamp Stamp => new(Declaration.InstanceIdentity, Declaration.SceneRevision, Declaration.AssetRevision);

    public static RetailCollisionSceneResult Create(RetailCollisionPreparedData prepared, RetailHeightMap40Data terrain,
        RetailCollisionSources sources, RetailSearchWorldBounds bounds, RetailCollisionSceneDeclaration declaration,
        IReadOnlyList<RetailCollisionRegionObservation> regions, RetailCollisionMaterialPolicy? policy, RetailCollisionSceneLimits limits,
        IReadOnlyList<RetailFieldObjectGeometry>? fieldObjects = null)
    {
        try
        {
            if (prepared is null || terrain is null || sources is null || declaration is null || regions is null || limits is null ||
                limits.MaxRegionRecords < 1 || limits.MaxObjectRecords < 0 || !Text(declaration.WorldIdentity) || !Text(declaration.InstanceIdentity) ||
                !Text(declaration.SceneRevision) || !Text(declaration.AssetRevision) || !Digest(declaration.EvidenceSha256) || !Text(declaration.Evidence))
                Stop(RetailCollisionStatus.InvalidInput, "Explicit prepared assets, bounds, scene declarations and managed record limits are required.");
            if (regions.Count > limits.MaxRegionRecords) Stop(RetailCollisionStatus.DefensiveLimit, "Region-record budget; no scene records are silently dropped.");
            var raw = prepared.Raw;
            if (sources.Mesh is null || sources.Collision is null || sources.HeightMap40 is null || sources.NativeNpcBinary is null ||
                !MapMatches(sources.Mesh, raw.MeshBytes, raw.MeshGuid, raw.Dimensions.WidthMetres, raw.Dimensions.HeightMetres) ||
                !MapMatches(sources.Collision, raw.CollisionBytes, raw.CollisionGuid, raw.Dimensions.WidthMetres, raw.Dimensions.HeightMetres) ||
                !MapMatches(sources.HeightMap40, terrain.RawFile, terrain.BuildGuid, terrain.WorldWidthMetres, terrain.WorldHeightMetres) ||
                !Text(sources.NativeNpcBinary.Identity) || sources.NativeNpcBinary.ByteLength != RetailGroundPathSearch.SupportedNpcBinaryBytes ||
                !string.Equals(sources.NativeNpcBinary.Sha256, RetailGroundPathSearch.SupportedNpcBinarySha256, StringComparison.OrdinalIgnoreCase))
                Stop(RetailCollisionStatus.SourceIdentityMismatch, "An individual loaded mesh/collision/40 identity or pinned binary declaration mismatches.");
            if (raw.MeshGuid is null || raw.CollisionGuid != raw.MeshGuid || terrain.BuildGuid != raw.MeshGuid ||
                terrain.WorldWidthMetres != raw.Dimensions.WidthMetres || terrain.WorldHeightMetres != raw.Dimensions.HeightMetres)
                Stop(RetailCollisionStatus.SourceIdentityMismatch, "Unverified headerless/GUID/dimension mixtures cannot publish computed geometry.");
            Point(bounds.Minimum); Point(bounds.Maximum);
            if (bounds.Minimum.X != 0 || bounds.Minimum.Y != 0 || bounds.Maximum.X != raw.Dimensions.WidthMetres ||
                bounds.Maximum.Y != raw.Dimensions.HeightMetres || bounds.Minimum.Z >= bounds.Maximum.Z)
                Stop(RetailCollisionStatus.InvalidInput, "Explicit native XY bounds must match loaded dimensions; finite Z bounds are required.");
            var owned = new Dictionary<(int, int), RetailCollisionRegionObservation>();
            int objects = 0;
            foreach (var region in regions)
            {
                if (region is null || region.OrderedObjects is null || region.RegionX < 0 || region.RegionY < 0 ||
                    region.RegionX >= raw.Dimensions.WidthMetres / 16 || region.RegionY >= raw.Dimensions.HeightMetres / 16 ||
                    !Text(region.SceneRevision) || !Digest(region.CaptureSha256))
                    Stop(RetailCollisionStatus.InvalidInput, "Invalid explicit region identity/coverage carrier.");
                if (region.OrderedObjects.Count > limits.MaxObjectRecords - objects)
                    Stop(RetailCollisionStatus.DefensiveLimit, "Object-record budget; no tail is silently dropped.");
                var copy = region.OrderedObjects.ToArray(); objects += copy.Length;
                if (copy.Any(o => o is null || !Text(o.ClassIdentity) || !Text(o.ObjectRevision)))
                    Stop(RetailCollisionStatus.InvalidInput, "Unknown object identity/class/revision must be explicit.");
                if (!owned.TryAdd((region.RegionX, region.RegionY), region with { OrderedObjects = Array.AsReadOnly(copy) }))
                    Stop(RetailCollisionStatus.InvalidInput, "Duplicate region records cannot select an arbitrary scene snapshot.");
            }
            var fields = new Dictionary<uint, RetailFieldObjectGeometry>();
            if (fieldObjects is not null)
            {
                if (fieldObjects.Count > limits.MaxObjectRecords)
                    Stop(RetailCollisionStatus.DefensiveLimit, "Owned field-geometry record budget.");
                int fieldCount = fieldObjects.Count;
                for (int fieldIndex = 0; fieldIndex < fieldCount; fieldIndex++)
                {
                    var field = fieldObjects[fieldIndex];
                    if (field is null || !ReferenceEquals(field.Prepared, prepared))
                        Stop(RetailCollisionStatus.SourceIdentityMismatch, "Field geometry must use this exact owned prepared mesh and eligibility source.");
                    if (!fields.TryAdd(field.Observation.EditorId, field))
                        Stop(RetailCollisionStatus.InvalidInput, "Duplicate owned editor IDs cannot choose an arbitrary geometry binding.");
                }
            }
            return new(RetailCollisionStatus.Computed, new(prepared, terrain, sources, bounds, declaration, owned, policy, fields), null);
        }
        catch (RetailCollisionStop stop) { return new(stop.Status, null, stop.Message); }
    }
    internal RetailCollisionRegionObservation Region(int worldX, int worldY)
    {
        if (!regions.TryGetValue((worldX >> 4, worldY >> 4), out var region) || !region.CoverageKnown)
            Stop(RetailCollisionStatus.UnsupportedDynamicScene, "Consumed field-object region has no known complete ordered snapshot; absence is not empty.");
        if (region.SceneRevision != Declaration.SceneRevision)
            Stop(RetailCollisionStatus.StaleSource, "Consumed field-object list has a stale scene epoch.");
        return region;
    }
    internal RetailFieldObjectGeometry Field(RetailCollisionSceneObject obj)
    {
        if (!fieldObjects.TryGetValue(obj.ObjectId, out var field))
            Stop(RetailCollisionStatus.UnsupportedDynamicScene, $"Consumed editor{obj.ObjectId}/{obj.ClassIdentity} has no computed actual-class geometry.");
        var observation = field.Observation;
        if (obj.ClassIdentity != observation.Class.ClassName)
            Stop(RetailCollisionStatus.UnsupportedDynamicScene, "Region object class differs from its actual constructor/vtable binding.");
        if (obj.ObjectRevision != observation.ObjectRevision || observation.WorldIdentity != Declaration.WorldIdentity ||
            observation.InstanceIdentity != Declaration.InstanceIdentity || observation.SceneRevision != Declaration.SceneRevision ||
            observation.AssetRevision != Declaration.AssetRevision)
            Stop(RetailCollisionStatus.StaleSource, "Consumed field state/backing-owner receipt does not match this instance/object/scene/asset epoch.");
        return field;
    }
    private static bool MapMatches(RetailSearchMapAssetIdentity identity, ReadOnlyMemory<byte> bytes, Guid? guid, int width, int height) =>
        identity.Asset is not null && Text(identity.Asset.Identity) && identity.Asset.ByteLength == bytes.Length && Digest(identity.Asset.Sha256) &&
        string.Equals(Convert.ToHexString(SHA256.HashData(bytes.Span)), identity.Asset.Sha256, StringComparison.OrdinalIgnoreCase) &&
        identity.BuildGuid == guid && identity.WorldWidthMetres == width && identity.WorldHeightMetres == height;
}
