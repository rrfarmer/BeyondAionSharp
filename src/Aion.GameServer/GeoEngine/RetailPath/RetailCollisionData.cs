using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Aion.GameServer.GeoEngine.RetailPath;

public enum RetailCollisionStatus
{
    Computed, InvalidInput, SourceIdentityMismatch, StaleSource, MissingCosineEvidence,
    MissingMaterialPolicy, UnsupportedFormat, UnsupportedArithmetic, UnsupportedDynamicScene,
    UnsupportedEmptySectorStorage, DefensiveLimit
}
public enum RetailCollisionReadErrorCode
{
    InvalidOptions, InvalidDimensions, Truncated, InvalidLength, UnsupportedSignedCount,
    VertexReference, TriangleReference, TrailingBytes, AllocationLimitExceeded
}
public sealed record RetailCollisionReadError(RetailCollisionReadErrorCode Code, string File, int FileOffset, string Detail);
public sealed record RetailCollisionDiagnostic(string Code, string File, int FileOffset, string Detail);

/// <summary>All budgets are caller-selected managed support limits, never native allocation defaults or a total process-memory cap.</summary>
public sealed record RetailCollisionReadOptions(int MaxMeshBytes, int MaxCollisionBytes, int MaxVertices,
    int MaxTriangles, int MaxSectors, int MaxBands, int MaxReferences, long MaxMetadataBytes,
    int MaxFieldRecords, int MaxNameBytes, bool RequireExactEnd);

public readonly record struct RetailCollisionDimensions(int WidthMetres, int HeightMetres);
public readonly record struct RetailCollisionTriangleDescriptor(uint BaseFloatOffset, short Delta1,
    short Delta2, int FileOffset);
public readonly record struct RetailCollisionTriangle(uint Id, RetailSearchPoint A, RetailSearchPoint B,
    RetailSearchPoint C, byte Material, int DescriptorFileOffset);
public readonly record struct RetailCollisionBand(short Height, ushort ReferenceCount,
    int ReferencesFileOffset, int HeaderFileOffset);
public readonly record struct RetailCollisionCell(int FirstBand, int BandCount,
    int CountFileOffset, bool SharedEmptyStorage);
public readonly record struct RetailCollisionSector(int X, int Y, int LengthFileOffset, int DeclaredBlobBytes,
    int CountPlaneFileOffset, int BlobFileOffset);
public sealed record RetailCollisionFieldRecord(uint Id, ReadOnlyMemory<byte> RawKind,
    ReadOnlyMemory<byte> RawName, ReadOnlyMemory<byte> Payload, int RecordFileOffset, int PayloadFileOffset);
public sealed record RetailCollisionReadResult(RetailCollisionData? Data, RetailCollisionReadError? Error,
    IReadOnlyList<RetailCollisionDiagnostic> Diagnostics)
{
    public bool Success => Data is not null && Error is null;
    public bool NativeAcceptanceVerified => false;
}

/// <summary>Owned lossless source bytes plus bounded structural indices. No preprocessing, runtime scene or geometry policy is inferred.</summary>
public sealed class RetailCollisionData
{
    private readonly RetailCollisionCell[] cells;
    private readonly RetailCollisionBand[] bands;
    internal RetailCollisionData(byte[] mesh, byte[] collision, RetailCollisionDimensions dimensions,
        int meshHeader, int collisionHeader, int vertexCount, int verticesOffset, int triangleCount,
        int trianglesOffset, int materialsOffset, int meshParsed, int collisionParsed,
        RetailCollisionSector[] sectors, RetailCollisionCell[] cells, RetailCollisionBand[] bands,
        RetailCollisionFieldRecord[] fields, long metadataEstimate, int referenceCount, bool emptyMesh)
    {
        MeshBytes = mesh; CollisionBytes = collision; Dimensions = dimensions;
        MeshHeaderBytes = meshHeader; CollisionHeaderBytes = collisionHeader;
        VertexCount = vertexCount; VerticesFileOffset = verticesOffset; TriangleCount = triangleCount;
        TrianglesFileOffset = trianglesOffset; MaterialsFileOffset = materialsOffset;
        MeshParsedBytes = meshParsed; CollisionParsedBytes = collisionParsed;
        Sectors = Array.AsReadOnly(sectors); this.cells = cells; this.bands = bands;
        Cells = Array.AsReadOnly(cells); Bands = Array.AsReadOnly(bands); Fields = Array.AsReadOnly(fields);
        EstimatedMetadataBytes = metadataEstimate; ReferenceCount = referenceCount; EmptyVertexBranch = emptyMesh;
        MeshSha256 = Convert.ToHexString(SHA256.HashData(mesh)).ToLowerInvariant();
        CollisionSha256 = Convert.ToHexString(SHA256.HashData(collision)).ToLowerInvariant();
    }
    public ReadOnlyMemory<byte> MeshBytes { get; }
    public ReadOnlyMemory<byte> CollisionBytes { get; }
    public RetailCollisionDimensions Dimensions { get; }
    public int SectorsX => Dimensions.WidthMetres / 64;
    public int SectorsY => Dimensions.HeightMetres / 64;
    public int MeshHeaderBytes { get; }
    public int CollisionHeaderBytes { get; }
    public Guid? MeshGuid => MeshHeaderBytes == 128 ? new Guid(MeshBytes.Span.Slice(4, 16)) : null;
    public Guid? CollisionGuid => CollisionHeaderBytes == 128 ? new Guid(CollisionBytes.Span.Slice(4, 16)) : null;
    public int VertexCount { get; }
    public int TriangleCount { get; }
    public int VerticesFileOffset { get; }
    public int TrianglesFileOffset { get; }
    public int MaterialsFileOffset { get; }
    public int MeshParsedBytes { get; }
    public int CollisionParsedBytes { get; }
    public ReadOnlyMemory<byte> MeshTrailingBytes => MeshBytes[MeshParsedBytes..];
    public ReadOnlyMemory<byte> CollisionTrailingBytes => CollisionBytes[CollisionParsedBytes..];
    public string MeshSha256 { get; }
    public string CollisionSha256 { get; }
    public IReadOnlyList<RetailCollisionSector> Sectors { get; }
    public IReadOnlyList<RetailCollisionCell> Cells { get; }
    public IReadOnlyList<RetailCollisionBand> Bands { get; }
    public IReadOnlyList<RetailCollisionFieldRecord> Fields { get; }
    public int ReferenceCount { get; }
    public bool EmptyVertexBranch { get; }
    public long EstimatedMetadataBytes { get; }
    public bool RuntimeVerified => false;

    public RetailCollisionTriangleDescriptor GetDescriptor(uint triangleId)
    {
        if (triangleId >= TriangleCount) throw new ArgumentOutOfRangeException(nameof(triangleId));
        int at = TrianglesFileOffset + (int)triangleId * 8;
        var span = MeshBytes.Span;
        return new(BinaryPrimitives.ReadUInt32LittleEndian(span[at..]),
            BinaryPrimitives.ReadInt16LittleEndian(span[(at + 4)..]),
            BinaryPrimitives.ReadInt16LittleEndian(span[(at + 6)..]), at);
    }
    public RetailCollisionTriangle GetTriangle(uint triangleId)
    {
        var descriptor = GetDescriptor(triangleId);
        long origin = descriptor.BaseFloatOffset;
        return new(triangleId, Point(origin), Point(origin + descriptor.Delta1), Point(origin + descriptor.Delta2),
            MeshBytes.Span[MaterialsFileOffset + (int)triangleId], descriptor.FileOffset);
    }
    private RetailSearchPoint Point(long floatOffset)
    {
        int at = VerticesFileOffset + checked((int)(floatOffset * 4));
        var span = MeshBytes.Span;
        return new(ReadFloat(span, at), ReadFloat(span, at + 4), ReadFloat(span, at + 8));
    }
    private static float ReadFloat(ReadOnlySpan<byte> bytes, int at) =>
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[at..]));
    public uint GetRawReference(RetailCollisionBand band, int index)
    {
        if ((uint)index >= band.ReferenceCount) throw new ArgumentOutOfRangeException(nameof(index));
        // Reject foreign structural records rather than reading arbitrary carrier offsets.
        if (band.HeaderFileOffset < CollisionHeaderBytes || band.ReferencesFileOffset != band.HeaderFileOffset + 4 ||
            (long)band.ReferencesFileOffset + (long)band.ReferenceCount * 4 > CollisionParsedBytes)
            throw new ArgumentException("Foreign collision band.", nameof(band));
        int low = 0, high = bands.Length - 1;
        bool bound = false;
        while (low <= high)
        {
            int middle = low + (high - low) / 2;
            if (bands[middle].HeaderFileOffset < band.HeaderFileOffset) low = middle + 1;
            else if (band.HeaderFileOffset < bands[middle].HeaderFileOffset) high = middle - 1;
            else { bound = bands[middle] == band; break; }
        }
        if (!bound) throw new ArgumentException("Band does not match this owned structural index.", nameof(band));
        return BinaryPrimitives.ReadUInt32LittleEndian(CollisionBytes.Span[(band.ReferencesFileOffset + index * 4)..]);
    }
    internal RetailCollisionCell Cell(int x, int y)
    {
        int sector = (y >> 5) * SectorsX + (x >> 5);
        return cells[sector * 1024 + ((y & 31) * 32 + (x & 31))];
    }
    internal RetailCollisionBand Band(int index) => bands[index];
}

public sealed record RetailCollisionPrepareLimits(int MaxReferences, int MaxBands, long MaxMetadataBytes);
public sealed record RetailCollisionCosineReceipt(bool Known, string NativeBinarySha256, string NativeFunction,
    uint ArgumentBits, uint ResultBits, string Revision, string CaptureSha256, bool SyntheticFixture)
{
    /// <summary>Explicit conditional source proof, never an observed runtime return or default.</summary>
    public RetailCollisionCosineSourceEvidence? ConditionalSourceProof { get; init; }
    public string Origin => ConditionalSourceProof is not null ? "ConditionalStaticSourceProof"
        : SyntheticFixture ? "SyntheticFixture" : "OriginalReturnDeclaration";
    public bool RuntimeVerified => false;
}
public readonly record struct RetailCollisionPreparedBand(short Height, int FirstReference, int ReferenceCount, int HeaderFileOffset);
public readonly record struct RetailCollisionPreparedCell(int FirstBand, int BandCount, bool SharedEmptyStorage, bool SpecialMaterial);
public sealed record RetailCollisionPreparationResult(RetailCollisionStatus Status, RetailCollisionPreparedData? Data,
    string? Detail, int ProcessedReferences, bool CosineEvidenceConsumed)
{
    public bool Success => Status == RetailCollisionStatus.Computed && Data is not null;
    public bool RuntimeVerified => false;
}

/// <summary>Native derived ordering is separate from the unchanged file carrier. No geometry service or actor is modified.</summary>
public sealed class RetailCollisionPreparedData
{
    private readonly RetailCollisionPreparedCell[] cells;
    private readonly RetailCollisionPreparedBand[] bands;
    private readonly uint[] references;
    internal RetailCollisionPreparedData(RetailCollisionData raw, RetailCollisionPreparedCell[] cells,
        RetailCollisionPreparedBand[] bands, uint[] references, RetailCollisionCosineReceipt? cosine, bool consumed)
    {
        Raw = raw; this.cells = cells; this.bands = bands; this.references = references;
        Cells = Array.AsReadOnly(cells); Bands = Array.AsReadOnly(bands); References = Array.AsReadOnly(references);
        Cosine = cosine; CosineEvidenceConsumed = consumed;
        HasSpecialMaterial = cells.Any(cell => cell.SpecialMaterial);
    }
    public RetailCollisionData Raw { get; }
    public IReadOnlyList<RetailCollisionPreparedCell> Cells { get; }
    public IReadOnlyList<RetailCollisionPreparedBand> Bands { get; }
    public IReadOnlyList<uint> References { get; }
    public RetailCollisionCosineReceipt? Cosine { get; }
    public bool CosineEvidenceConsumed { get; }
    /// <summary>Derived native world-material flag; publishing this value does not mutate a world.</summary>
    public bool HasSpecialMaterial { get; }
    public bool RuntimeVerified => false;
    internal RetailCollisionPreparedCell Cell(int x, int y) => cells[((y >> 5) * Raw.SectorsX + (x >> 5)) * 1024 + ((y & 31) * 32 + (x & 31))];
    internal RetailCollisionPreparedBand Band(int index) => bands[index];
    internal uint Reference(int index) => references[index];
}

internal sealed class RetailCollisionStop(RetailCollisionStatus status, string detail) : Exception(detail)
{
    internal RetailCollisionStatus Status { get; } = status;
}
