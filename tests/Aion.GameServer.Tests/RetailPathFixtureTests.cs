using System.Security.Cryptography;
using System.Text.Json;
using System.Buffers.Binary;
using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

/// <summary>Installed asset check, not a retail runtime oracle. Missing input is explicitly skipped.</summary>
public sealed class RetailPathFixtureTests
{
    [SkippableFact]
    public void SkyTempleMatchesIndependentSerializedStructureAndPayloadHashes()
    {
        string path = Environment.GetEnvironmentVariable("AION_RETAIL_PATH_FIXTURE")
            ?? @"D:\Aion58ServerTesting\Server\Map\Worlds\IDDF2Flying\IDDF2Flying-path.dat";
        Skip.IfNot(File.Exists(path), $"Installed Sky Temple path fixture is absent: {path}");
        byte[] file = File.ReadAllBytes(path);
        Assert.Equal("e59d0a7df72c55edb4aa6575b38d8a5c9878eefd9a960b6fd25458d21e069be5", Sha(file));
        var result = RetailPathDataReader.Read(file, new(512, 512)
        {
            ExpectedCollisionGuid = Guid.Parse("ff47ba3c-ac6d-4fbf-a6b1-a7d0945a7045"),
            RequireExactEnd = true
        });
        Assert.True(result.Success, result.Error?.ToString());
        var data = result.Data!;
        Assert.True(data.ReferencesValidated);
        Assert.Equal(628023, data.ParsedBytes);
        Assert.Equal(32, data.SectorsX);
        Assert.Equal(32, data.SectorsY);
        Assert.Equal(1024, data.Sectors.Count);
        Assert.Equal(495, data.NodeCount);
        Assert.Equal(7626, data.UniqueSparseRecordCount);
        Assert.Equal(128486, data.SparseBlob.Length);
        Assert.Equal(15648, data.LinkTableCount);
        Assert.Equal("9136a8908a3f2e8d58ba824db6c3054e5d6133e48e92b138757552ebd42f3965", Sha(data.SparseBlob.Span));
        Assert.Equal("05d47f6e0d750cf15dabdd2bf800107d209bd506ceaf43c9d9801607c4b3631b",
            Sha(data.RawFile.Span.Slice(data.LinkTableFileOffset, data.LinkTableCount * 4)));
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var sector in data.Sectors)
        foreach (var node in sector.Nodes) digest.AppendData(node.RawBytes.Span);
        Assert.Equal("ac156a456b97eec9fa8eef7e142141cba063812a48f9b199a3ec597ac5582974",
            Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant());
        var counts = data.Sectors.SelectMany(s => s.Nodes).GroupBy(n => n.Type)
            .ToDictionary(g => (int)g.Key, g => g.Count());
        Assert.Equal(7, counts.Count);
        Assert.Equal(1, counts[0]); Assert.Equal(110, counts[8]); Assert.Equal(60, counts[10]);
        Assert.Equal(154, counts[12]); Assert.Equal(93, counts[14]); Assert.Equal(6, counts[15]); Assert.Equal(71, counts[16]);
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == "CollisionGuidMismatch");
        Assert.Equal(file, data.RawFile.ToArray());
    }

    [SkippableFact]
    public void OptInRetailBatchRecordsEveryAssetAndUnresolvedDimension()
    {
        string? manifestPath = Environment.GetEnvironmentVariable("AION_RETAIL_PATH_BATCH_MANIFEST");
        string? outputPath = Environment.GetEnvironmentVariable("AION_RETAIL_PATH_BATCH_OUTPUT");
        Skip.If(string.IsNullOrWhiteSpace(manifestPath) || string.IsNullOrWhiteSpace(outputPath),
            "Batch requires explicit inventory manifest and external JSON output paths.");
        int metadataMiB = BatchLimit("AION_RETAIL_PATH_BATCH_METADATA_MIB", 128, 1024);
        int maxRecords = BatchLimit("AION_RETAIL_PATH_BATCH_MAX_RECORDS", 1_000_000, 4_000_000);
        string dimensionPolicy = Environment.GetEnvironmentVariable("AION_RETAIL_PATH_BATCH_DIMENSIONS") ?? "declared48";
        Assert.Contains(dimensionPolicy, new[] { "declared48", "retail-heightmap-native" });
        var budget = new RetailPathReadOptions(16, 16)
        { MaxMetadataBytes = metadataMiB * 1024L * 1024, MaxSparseRecords = maxRecords };
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath!));
        var rows = new List<BatchRow>();
        foreach (var map in manifest.RootElement.GetProperty("maps").EnumerateArray())
        {
            var source = map.GetProperty("retailPath");
            if (source.ValueKind == JsonValueKind.Null) continue;
            var row = new BatchRow
            {
                MapId = map.GetProperty("mapId").GetInt32(), Devname = map.GetProperty("devname").GetString()!,
                MappingConfidence = map.GetProperty("mappingConfidence").GetString()!,
                SourcePath = source.GetProperty("path").GetString()!, SourceSha256 = source.GetProperty("sha256").GetString()!,
                DimensionPolicy = dimensionPolicy
            };
            rows.Add(row);
            try
            {
                if (!File.Exists(row.SourcePath)) { row.Status = "SOURCE_ABSENT"; continue; }
                row.InputBytes = new FileInfo(row.SourcePath).Length;
                if (row.InputBytes > budget.MaxInputBytes)
                { row.Status = "INPUT_LIMIT"; continue; }
                byte[] bytes = File.ReadAllBytes(row.SourcePath);
                row.ActualSha256 = Sha(bytes);
                if (!string.Equals(row.SourceSha256, row.ActualSha256, StringComparison.OrdinalIgnoreCase))
                { row.Status = "SOURCE_IDENTITY_MISMATCH"; continue; }
                var template = map.GetProperty("template");
                if (template.TryGetProperty("world_size", out var size) && int.TryParse(size.GetString(), out int metres))
                    row.Declared48WidthMetres = row.Declared48HeightMetres = metres;
                // Saved FUN_1402f3ec0 sets World+0x30/+0x34 from selected heightmap
                // fields +0x18/+0x1c times two. Read the individually hashed source,
                // never infer dimensions from the path's EOF or a neighboring map.
                if (!ReadNativeDimensions(map, bytes, row, budget.MaxInputBytes)) continue;
                row.DimensionMismatch = row.Declared48WidthMetres != row.NativeWidthMetres || row.Declared48HeightMetres != row.NativeHeightMetres;
                if (dimensionPolicy == "declared48")
                {
                    if (row.Declared48WidthMetres is not > 0 || row.Declared48HeightMetres is not > 0)
                    { row.Status = "DIMENSION_UNRESOLVED"; continue; }
                    row.WorldWidthMetres = row.Declared48WidthMetres;
                    row.WorldHeightMetres = row.Declared48HeightMetres;
                }
                else
                {
                    row.WorldWidthMetres = row.NativeWidthMetres;
                    row.WorldHeightMetres = row.NativeHeightMetres;
                }
                Guid? collisionGuid = null;
                if (map.GetProperty("retailAssets").TryGetProperty("retail-collision", out var collision) &&
                    collision.TryGetProperty("guid", out var guid) && Guid.TryParse(guid.GetString(), out var parsedGuid)) collisionGuid = parsedGuid;
                long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
                row.WorkingSetBeforeBytes = Environment.WorkingSet;
                var result = RetailPathDataReader.Read(bytes, budget with
                {
                    WorldWidthMetres = row.WorldWidthMetres!.Value, WorldHeightMetres = row.WorldHeightMetres!.Value,
                    ExpectedCollisionGuid = collisionGuid, RequireExactEnd = true
                });
                row.ProcessGcAllocatedDeltaBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
                row.WorkingSetAfterBytes = Environment.WorkingSet;
                row.Status = result.Success ? "STRUCTURALLY_PARSED" : "PARSER_FAILURE";
                row.Error = result.Error;
                row.Diagnostics = result.Diagnostics;
                row.SuppressedDiagnostics = result.SuppressedDiagnostics;
                if (result.Data is { } data)
                {
                    row.Sectors = data.Sectors.Count; row.Nodes = data.NodeCount;
                    row.UniqueSparseRecords = data.UniqueSparseRecordCount;
                    row.DeclaredSparseRecords = data.Sectors.SelectMany(s => s.Nodes).Sum(n => (long)n.SparseRecords.Count);
                    row.DescriptorBudgetEstimateBytes = data.Sectors.Count * 128L + data.NodeCount * 256L +
                        row.DeclaredSparseRecords * 256L + result.Diagnostics.Count * 256L;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { row.Status = "SOURCE_IO_FAILURE"; row.SourceError = ex.Message; }
        }
        var counts = rows.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count());
        string target = Path.GetFullPath(outputPath!);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, JsonSerializer.Serialize(new
        {
            Scope = "Inactive serialization/used-target validation only; no native passability, path search, movement or activation proof",
            InventoryManifest = Path.GetFullPath(manifestPath!), InventoryManifestSha256 = Sha(File.ReadAllBytes(manifestPath!)),
            AvailableAssetRows = rows.Count, Counts = counts, DimensionPolicy = dimensionPolicy,
            Budget = new { budget.MaxInputBytes, budget.MaxMetadataBytes, budget.MaxSparseRecords,
                budget.MaxSectors, budget.MaxNodes, budget.MaxLinkTableEntries, budget.MaxDiagnostics },
            MemoryObservationScope = "Process-wide GC allocation deltas and working-set snapshots include runner noise; not isolated live-map residency, peak memory or Phase8 capacity proof",
            AllAvailableAssetsParsed = rows.All(r => r.Status == "STRUCTURALLY_PARSED"), Rows = rows
        }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(156, rows.Count);
        Assert.All(rows, row => Assert.True(row.Status is "STRUCTURALLY_PARSED" or "DIMENSION_UNRESOLVED",
            $"{row.MapId} {row.Devname}: {row.Status}, {row.Error}; see {target}"));
        // Passing this harness allows individually reported unresolved dimensions; the receipt's
        // AllAvailableAssetsParsed and per-map statuses, not the test result, are the coverage gate.
    }

    private sealed class BatchRow
    {
        public int MapId { get; init; }
        public string Devname { get; init; } = "";
        public string MappingConfidence { get; init; } = "";
        public string SourcePath { get; init; } = "";
        public string SourceSha256 { get; init; } = "";
        public string? ActualSha256 { get; set; }
        public long InputBytes { get; set; }
        public string DimensionPolicy { get; init; } = "";
        public int? Declared48WidthMetres { get; set; }
        public int? Declared48HeightMetres { get; set; }
        public int? NativeWidthMetres { get; set; }
        public int? NativeHeightMetres { get; set; }
        public bool DimensionMismatch { get; set; }
        public string? NativeDimensionSource { get; set; }
        public string? NativeDimensionSourceSha256 { get; set; }
        public bool NativeHeightmapPathGuidMatches { get; set; }
        public string Status { get; set; } = "NOT_ATTEMPTED";
        public int? WorldWidthMetres { get; set; }
        public int? WorldHeightMetres { get; set; }
        public int? Sectors { get; set; }
        public int? Nodes { get; set; }
        public int? UniqueSparseRecords { get; set; }
        public long? DeclaredSparseRecords { get; set; }
        public long? DescriptorBudgetEstimateBytes { get; set; }
        public long? ProcessGcAllocatedDeltaBytes { get; set; }
        public long? WorkingSetBeforeBytes { get; set; }
        public long? WorkingSetAfterBytes { get; set; }
        public RetailPathReadError? Error { get; set; }
        public string? SourceError { get; set; }
        public IReadOnlyList<RetailPathDiagnostic> Diagnostics { get; set; } = Array.Empty<RetailPathDiagnostic>();
        public int SuppressedDiagnostics { get; set; }
    }

    private static int BatchLimit(string name, int fallback, int maximum)
    {
        string? raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        Assert.True(int.TryParse(raw, out int value) && value > 0 && value <= maximum,
            $"{name} must be a positive integer <= {maximum}.");
        return value;
    }

    private static bool ReadNativeDimensions(JsonElement map, byte[] pathBytes, BatchRow row, int byteBudget)
    {
        var assets = map.GetProperty("retailAssets");
        JsonElement selected = default;
        foreach (string kind in new[] { "retail-heightmap40", "retail-heightmap32", "retail-heightmap24" })
            if (assets.TryGetProperty(kind, out selected)) break;
        if (selected.ValueKind != JsonValueKind.Object)
        { row.Status = "DIMENSION_SOURCE_ABSENT"; return false; }
        row.NativeDimensionSource = selected.GetProperty("path").GetString();
        if (!File.Exists(row.NativeDimensionSource))
        { row.Status = "DIMENSION_SOURCE_ABSENT"; return false; }
        if (new FileInfo(row.NativeDimensionSource!).Length > byteBudget)
        { row.Status = "DIMENSION_SOURCE_LIMIT"; return false; }
        byte[] heightmap = File.ReadAllBytes(row.NativeDimensionSource!);
        row.NativeDimensionSourceSha256 = Sha(heightmap);
        if (!string.Equals(row.NativeDimensionSourceSha256, selected.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
        { row.Status = "DIMENSION_SOURCE_IDENTITY_MISMATCH"; return false; }
        bool versioned = heightmap.Length >= 128 && BinaryPrimitives.ReadUInt32LittleEndian(heightmap) == 0xaa55aa55;
        int start = versioned ? 128 : 0;
        if (heightmap.Length < start + 8)
        { row.Status = "DIMENSION_SOURCE_TRUNCATED"; return false; }
        int first = BinaryPrimitives.ReadInt32LittleEndian(heightmap.AsSpan(start));
        int second = BinaryPrimitives.ReadInt32LittleEndian(heightmap.AsSpan(start + 4));
        if (first <= 0 || second <= 0 || first > int.MaxValue / 2 || second > int.MaxValue / 2)
        { row.Status = "DIMENSION_SOURCE_INVALID"; return false; }
        row.NativeWidthMetres = second * 2;
        row.NativeHeightMetres = first * 2;
        row.NativeHeightmapPathGuidMatches = versioned && pathBytes.Length >= 16 && heightmap.AsSpan(4, 16).SequenceEqual(pathBytes.AsSpan(0, 16));
        return true;
    }

    private static string Sha(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
