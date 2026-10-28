using System.Buffers.Binary;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>Inactive bounded reader for the pinned native MeshMaterial/Collision layouts. Structural success is not native acceptance.</summary>
public static class RetailCollisionDataReader
{
    private const string MeshFile = "MeshMaterial.Dat", CollisionFile = "Collision.Dat";
    public static RetailCollisionReadResult ReadPair(ReadOnlySpan<byte> meshBytes, ReadOnlySpan<byte> collisionBytes,
        RetailCollisionDimensions dimensions, RetailCollisionReadOptions options)
    {
        var diagnostics = new List<RetailCollisionDiagnostic>();
        try
        {
            if (options is null || options.MaxMeshBytes < 4 || options.MaxCollisionBytes < 4 || options.MaxVertices < 1 ||
                options.MaxTriangles < 1 || options.MaxSectors < 1 || options.MaxBands < 1 || options.MaxReferences < 1 ||
                options.MaxMetadataBytes < 1 || options.MaxFieldRecords < 0 || options.MaxNameBytes is < 1 or > 255)
                Fail(RetailCollisionReadErrorCode.InvalidOptions, "options", 0, "Explicit positive budgets and a name-carrier budget1..255 are required.");
            if (dimensions.WidthMetres < 64 || dimensions.HeightMetres < 64 || (dimensions.WidthMetres & 1) != 0 || (dimensions.HeightMetres & 1) != 0)
                Fail(RetailCollisionReadErrorCode.InvalidDimensions, "dimensions", 0, "Explicit even metric dimensions of at least64m are required.");
            if (meshBytes.Length > options.MaxMeshBytes || collisionBytes.Length > options.MaxCollisionBytes)
                Fail(RetailCollisionReadErrorCode.AllocationLimitExceeded,
                    meshBytes.Length > options.MaxMeshBytes ? MeshFile : CollisionFile, 0, "Input exceeds its declared carrier budget.");
            long sectorCount = (long)(dimensions.WidthMetres / 64) * (dimensions.HeightMetres / 64);
            if (sectorCount > options.MaxSectors || sectorCount > int.MaxValue / 1024)
                Fail(RetailCollisionReadErrorCode.AllocationLimitExceeded, CollisionFile, 0, "Sector/cell index count exceeds the managed support budget.");
            // Owned carriers are copied once each. Builders, caller buffers and later preparation are additional memory, not this budget.
            byte[] meshOwned = meshBytes.ToArray(), collisionOwned = collisionBytes.ToArray();
            var mesh = new Cursor(meshOwned, MeshFile);
            var collision = new Cursor(collisionOwned, CollisionFile);
            int meshHeader = mesh.Header(), collisionHeader = collision.Header();
            long metadata = 0;
            void Reserve(long amount, string file, int at)
            {
                if (amount < 0 || amount > options.MaxMetadataBytes - metadata)
                    Fail(RetailCollisionReadErrorCode.AllocationLimitExceeded, file, at, "Conservative metadata estimate exceeds its explicit budget; this is not a measured global allocation cap.");
                metadata += amount;
            }
            int verticesAt = mesh.At, vertices = mesh.Int32();
            if (vertices < 0) Fail(RetailCollisionReadErrorCode.InvalidLength, MeshFile, verticesAt, "Negative vertex count.");
            if (vertices > options.MaxVertices) Fail(RetailCollisionReadErrorCode.AllocationLimitExceeded, MeshFile, verticesAt, "Vertex count budget.");
            int vertexDataAt = mesh.Take((long)vertices * 12);
            int triangles = 0, trianglesAt = mesh.At, materialAt = mesh.At;
            var fields = new List<RetailCollisionFieldRecord>();
            if (vertices != 0)
            {
                int countAt = mesh.At; triangles = mesh.Int32();
                if (triangles <= 0) Fail(RetailCollisionReadErrorCode.InvalidLength, MeshFile, countAt, "The admitted positive-vertex layout requires positive triangle count; native zero-sized bulk-read acceptance is not invented.");
                if (triangles > options.MaxTriangles) Fail(RetailCollisionReadErrorCode.AllocationLimitExceeded, MeshFile, countAt, "Triangle count budget.");
                trianglesAt = mesh.Take((long)triangles * 8);
                for (int i = 0; i < triangles; i++)
                {
                    int at = trianglesAt + i * 8;
                    long origin = BinaryPrimitives.ReadUInt32LittleEndian(meshOwned.AsSpan(at));
                    short d1 = BinaryPrimitives.ReadInt16LittleEndian(meshOwned.AsSpan(at + 4));
                    short d2 = BinaryPrimitives.ReadInt16LittleEndian(meshOwned.AsSpan(at + 6));
                    long length = (long)vertices * 3;
                    if (origin + 2 >= length) Fail(RetailCollisionReadErrorCode.VertexReference, MeshFile, at, "Base float-element offset is outside the vertex carrier.");
                    if (origin + d1 < 0 || origin + d1 + 2 >= length) Fail(RetailCollisionReadErrorCode.VertexReference, MeshFile, at + 4, "First signed relative float-element offset is outside the vertex carrier.");
                    if (origin + d2 < 0 || origin + d2 + 2 >= length) Fail(RetailCollisionReadErrorCode.VertexReference, MeshFile, at + 6, "Second signed relative float-element offset is outside the vertex carrier.");
                }
                materialAt = mesh.Take(triangles);
                int fieldsAt = mesh.At, fieldCount = mesh.Int32();
                if (fieldCount < 0) Fail(RetailCollisionReadErrorCode.InvalidLength, MeshFile, fieldsAt, "Negative field catalog count.");
                if (fieldCount > options.MaxFieldRecords) Fail(RetailCollisionReadErrorCode.AllocationLimitExceeded, MeshFile, fieldsAt, "Field catalog count budget.");
                Reserve((long)fieldCount * 192, MeshFile, fieldsAt);
                var ids = new HashSet<uint>();
                for (int i = 0; i < fieldCount; i++)
                {
                    int at = mesh.At;
                    uint id = unchecked((uint)mesh.Int32());
                    var kind = mesh.Name(options.MaxNameBytes);
                    var name = mesh.Name(options.MaxNameBytes);
                    int lengthAt = mesh.At, length = mesh.Int32();
                    if (length < 0) Fail(RetailCollisionReadErrorCode.InvalidLength, MeshFile, lengthAt, "Negative field payload length.");
                    int payloadAt = mesh.Take(length);
                    fields.Add(new(id, kind, name, meshOwned.AsMemory(payloadAt, length), at, payloadAt));
                    if (!ids.Add(id)) diagnostics.Add(new("DuplicateFieldId", MeshFile, at, "Preserved ordered duplicate; native lazy lookup selects in file order."));
                }
            }
            else diagnostics.Add(new("NativeEmptyVertexBranch", MeshFile, verticesAt, "Native skips remaining mesh/material/catalog reads when vertex count is zero; remaining bytes are retained."));

            Reserve(sectorCount * 48 + sectorCount * 1024 * 48, CollisionFile, collision.At);
            var sectors = new RetailCollisionSector[(int)sectorCount];
            var cells = new RetailCollisionCell[(int)sectorCount * 1024];
            var bands = new List<RetailCollisionBand>();
            int references = 0, sectorsX = dimensions.WidthMetres / 64;
            for (int s = 0; s < sectors.Length; s++)
            {
                int lengthAt = collision.At, length = collision.Int32();
                int countAt = collision.At, blobAt = collision.At;
                if (length < 1)
                {
                    if (length < 0) diagnostics.Add(new("NativeNegativeEmptySector", CollisionFile, lengthAt, "The signed less-than-one native branch is preserved as shared empty storage, not a positive allocation."));
                    sectors[s] = new(s % sectorsX, s / sectorsX, lengthAt, length, -1, -1);
                    for (int c = 0; c < 1024; c++) cells[s * 1024 + c] = new(bands.Count, 0, -1, true);
                    continue;
                }
                countAt = collision.Take(2048);
                blobAt = collision.Take(length);
                var blob = new Cursor(collisionOwned, CollisionFile, blobAt, blobAt + length);
                sectors[s] = new(s % sectorsX, s / sectorsX, lengthAt, length, countAt, blobAt);
                for (int c = 0; c < 1024; c++)
                {
                    int at = countAt + c * 2;
                    short count = BinaryPrimitives.ReadInt16LittleEndian(collisionOwned.AsSpan(at));
                    if (count < 0) Fail(RetailCollisionReadErrorCode.UnsupportedSignedCount, CollisionFile, at, "Negative signed band count enters an unadmitted native traversal domain.");
                    int first = bands.Count;
                    if (count > options.MaxBands - first) Fail(RetailCollisionReadErrorCode.AllocationLimitExceeded, CollisionFile, at, "Band count budget.");
                    Reserve((long)count * 64, CollisionFile, at);
                    short? previous = null;
                    for (int b = 0; b < count; b++)
                    {
                        int bandAt = blob.Take(4);
                        short z = BinaryPrimitives.ReadInt16LittleEndian(collisionOwned.AsSpan(bandAt));
                        ushort n = BinaryPrimitives.ReadUInt16LittleEndian(collisionOwned.AsSpan(bandAt + 2));
                        if (n > options.MaxReferences - references) Fail(RetailCollisionReadErrorCode.AllocationLimitExceeded, CollisionFile, bandAt + 2, "Reference count budget.");
                        int refsAt = blob.Take((long)n * 4);
                        for (int r = 0; r < n; r++)
                        {
                            int refAt = refsAt + r * 4;
                            uint id = BinaryPrimitives.ReadUInt32LittleEndian(collisionOwned.AsSpan(refAt));
                            if ((id & 0x7fffffffu) >= triangles) Fail(RetailCollisionReadErrorCode.TriangleReference, CollisionFile, refAt, "Consumed raw triangle reference is outside the loaded mesh after high-bit masking.");
                        }
                        if (previous is short p && z < p) diagnostics.Add(new("NonmonotonicBandHeight", CollisionFile, bandAt, "Preserved serialized order; native query early-exit order is not repaired."));
                        previous = z; bands.Add(new(z, n, refsAt, bandAt)); references += n;
                    }
                    cells[s * 1024 + c] = new(first, count, at, false);
                }
                if (blob.At != blob.End) Fail(RetailCollisionReadErrorCode.InvalidLength, CollisionFile, blob.At, "Sector traversal does not end at its declared blob boundary.");
            }
            if (dimensions.WidthMetres % 64 != 0 || dimensions.HeightMetres % 64 != 0)
                diagnostics.Add(new("NativeFloorSectorDimensions", CollisionFile, 0, "Native sector dimensions truncate at64m; edge storage outside the loaded sectors is not inferred."));
            if (meshHeader != 0 && collisionHeader != 0 && !meshOwned.AsSpan(4, 16).SequenceEqual(collisionOwned.AsSpan(4, 16)))
                diagnostics.Add(new("GuidMismatch", CollisionFile, 4, "Native warns but can continue; parsing does not establish compatible assets."));
            foreach (var input in new[] { mesh, collision })
            {
                if (input.At == input.End) continue;
                if (options.RequireExactEnd) Fail(RetailCollisionReadErrorCode.TrailingBytes, input.File, input.At, "Explicit exact-end policy rejects retained trailing bytes.");
                diagnostics.Add(new("TrailingBytes", input.File, input.At, "Trailing bytes retained without interpretation."));
            }
            return new(new(meshOwned, collisionOwned, dimensions, meshHeader, collisionHeader, vertices, vertexDataAt,
                triangles, trianglesAt, materialAt, mesh.At, collision.At, sectors, cells, bands.ToArray(), fields.ToArray(), metadata, references, vertices == 0),
                null, diagnostics.AsReadOnly());
        }
        catch (ReadStop stop) { return new(null, stop.Error, diagnostics.AsReadOnly()); }
    }

    private sealed class Cursor(byte[] bytes, string file, int start = 0, int? end = null)
    {
        internal int At { get; private set; } = start;
        internal int End { get; } = end ?? bytes.Length;
        internal string File { get; } = file;
        internal int Take(long count)
        {
            if (count < 0 || count > End - At) Fail(RetailCollisionReadErrorCode.Truncated, File, At, "Incomplete required field/carrier.");
            int at = At; At += (int)count; return at;
        }
        internal int Int32() => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(Take(4)));
        internal int Header()
        {
            if (End - At < 4) Fail(RetailCollisionReadErrorCode.Truncated, File, At, "Missing header/count prefix.");
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(At)) != 0xaa55aa55u) return 0;
            Take(128); return 128;
        }
        internal ReadOnlyMemory<byte> Name(int maximum)
        {
            int first = At;
            for (int i = 0; i < maximum; i++) if (bytes[Take(1)] == 0) return bytes.AsMemory(first, i);
            Fail(maximum < 255 ? RetailCollisionReadErrorCode.AllocationLimitExceeded : RetailCollisionReadErrorCode.InvalidLength,
                File, first, "NUL name exceeds its bounded native carrier domain.");
            return default;
        }
    }
    private sealed class ReadStop(RetailCollisionReadError error) : Exception(error.Detail)
    { internal RetailCollisionReadError Error { get; } = error; }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail(RetailCollisionReadErrorCode code, string file, int at, string detail) => throw new ReadStop(new(code, file, at, detail));
}
