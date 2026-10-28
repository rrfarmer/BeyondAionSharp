namespace Aion.GameServer.GeoEngine.RetailPath;

public enum RetailSurfaceQueryStatus { Complete, Found, NoLink, Ambiguous, OutsideWorld, InvalidIdentity, TerrainUnavailable, Unsupported, ResourceLimit, Malformed }
public enum RetailSurfaceCandidateStatus { WithinTolerance, OutsideTolerance, NoGround, TerrainUnavailable, Unsupported }
public enum RetailSurfaceHeightStatus { Resolved, NoGround, TerrainUnavailable, Unsupported }
public enum RetailPathLinkKind { None, LocalGrid, LocalExitMask, EdgeMask, EdgePortal, SparseRelative, SparseFull, SparseAbsent }

public sealed record RetailPathSurfaceQueryOptions
{
    /// <summary>Count bounds for this additional index/result collection, not a total managed-memory limit.</summary>
    public int MaxIndexedSparseRecords { get; init; } = 1_000_000;
    public int MaxCandidates { get; init; } = 65_536;
    /// <summary>An explicit identity waiver for headerless input; dimensions must still match. Never set implicitly.</summary>
    public bool AllowHeaderlessTerrain { get; init; }
}

/// <summary>Identity includes its owning file object, native node/layer, half-metre cell and optional sparse pointer. XY alone is insufficient.</summary>
public sealed class RetailPathSurfaceRef
{
    internal RetailPathSurfaceRef(RetailPathData owner, RetailPathNode node, int x, int y, RetailPathSparseRecord? record)
    { Owner = owner; Node = node; CellX = x; CellY = y; SparseRecord = record; }
    internal RetailPathData Owner { get; }
    public RetailPathNode Node { get; }
    public int CellX { get; }
    public int CellY { get; }
    public int Layer => Node.Layer;
    public int SectorX => Node.SectorX;
    public int SectorY => Node.SectorY;
    public int NodeFileOffset => Node.FileOffset;
    public RetailPathSparseRecord? SparseRecord { get; }
    public bool IsGridIdentity => SparseRecord is null;
    public int LocalCellIndex => (CellY & 31) * 32 + (CellX & 31);
    public uint NativeId => IsGridIdentity ? 0x80000000u | (uint)(Layer * 1024 + LocalCellIndex) : (uint)SparseRecord!.BlobOffset;
    public float CentreXMetres => CellX * 0.5f + 0.25f;
    public float CentreYMetres => CellY * 0.5f + 0.25f;
}

/// <summary>FileOffset is the sparse record start or grid payload base. A resolved float can lack a supported int32 endpoint conversion.</summary>
public readonly record struct RetailSurfaceHeight(RetailSurfaceHeightStatus Status, float Metres, int? ComparisonCentimetres,
    RetailPathHeight RawHeight, int FileOffset);
public sealed record RetailSurfaceCandidate(RetailPathSurfaceRef Surface, RetailSurfaceCandidateStatus Status, RetailSurfaceHeight Height);
public sealed record RetailSurfaceCandidates(RetailSurfaceQueryStatus Status, int CellX, int CellY, int QueryCentimetres,
    IReadOnlyList<RetailSurfaceCandidate> Candidates, string? Detail = null);
public readonly record struct RetailPathLinkProvenance(RetailPathLinkKind Kind, int FileOffset, uint StoredValue, uint DecodedValue, bool CrossesSector);
public sealed record RetailSurfaceNeighbor(RetailSurfaceQueryStatus Status, RetailPathSurfaceRef? Target,
    RetailPathLinkProvenance Provenance, string? Detail = null)
{
    public RetailPathSurfaceRef? Source { get; init; }
}

/// <summary>
/// Inactive surface/topology data access. Native endpoint 0x1400fa8d0, type lookups 0x14021f450/21f920/2204d0/2209d0,
/// cardinal consumers 0x14021ef60/21f500/21fa40/220600/220b20. No diagonals, search, actor costs, dynamic collision or movement.
/// </summary>
public sealed class RetailPathSurfaceQuery
{
    private readonly RetailPathData path;
    private readonly RetailHeightMap40Data? terrain;
    private readonly RetailPathSurfaceQueryOptions options;
    private readonly Dictionary<int, RetailPathSparseRecord> records = [];
    private readonly bool terrainUsable;

    public RetailPathSurfaceQuery(RetailPathData path, RetailHeightMap40Data? terrain = null, RetailPathSurfaceQueryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        this.path = path; this.terrain = terrain; this.options = options ?? new();
        if (this.options.MaxIndexedSparseRecords < 0 || this.options.MaxCandidates < 1)
            throw new ArgumentOutOfRangeException(nameof(options));
        SparseIndexAvailable = path.UniqueSparseRecordCount <= this.options.MaxIndexedSparseRecords;
        if (SparseIndexAvailable)
            foreach (var sector in path.Sectors)
            foreach (var node in sector.Nodes)
            foreach (var record in node.SparseRecords) records.TryAdd(record.BlobOffset, record);
        var diagnostics = new List<string>();
        if (terrain is not null)
        {
            bool dimensions = terrain.WorldWidthMetres == path.WorldWidthMetres && terrain.WorldHeightMetres == path.WorldHeightMetres;
            bool identity = terrain.BuildGuid is Guid guid ? guid == path.BuildGuid : this.options.AllowHeaderlessTerrain;
            if (!dimensions) diagnostics.Add("Heightmap/path dimensions differ; terrain projection is unavailable.");
            if (terrain.BuildGuid is Guid actual && actual != path.BuildGuid) diagnostics.Add("Heightmap/path GUIDs differ; terrain projection is unavailable.");
            if (terrain.BuildGuid is null) diagnostics.Add(this.options.AllowHeaderlessTerrain
                ? "Headerless terrain identity was explicitly waived; this does not establish mesh compatibility."
                : "Headerless terrain has no bound GUID; terrain projection is unavailable without an explicit waiver.");
            terrainUsable = dimensions && identity;
        }
        CompatibilityDiagnostics = diagnostics.AsReadOnly();
    }

    public bool SparseIndexAvailable { get; }
    public IReadOnlyList<string> CompatibilityDiagnostics { get; }

    /// <summary>Access an explicit identity without endpoint tolerance/NoGround filtering. Sparse pointers can refer to globally parsed records outside the node's own record list.</summary>
    public RetailSurfaceNeighbor GetSurface(int cellX, int cellY, int layer, int? sparseBlobOffset = null)
    {
        var domain = CellDomain(cellX, cellY);
        if (domain != RetailSurfaceQueryStatus.Found) return Result(domain, "Cell is outside the supported in-world signed-int16 domain.");
        var node = path.GetSector(cellX / 32, cellY / 32).GetLayer(layer);
        if (node is null) return Result(RetailSurfaceQueryStatus.InvalidIdentity, "Layer does not exist.");
        if (node.Type != 16)
            return sparseBlobOffset is not null ? Result(RetailSurfaceQueryStatus.InvalidIdentity, "Grid identity cannot carry a sparse pointer.") :
                new(RetailSurfaceQueryStatus.Found, new(path, node, cellX, cellY, null), default);
        if (!SparseIndexAvailable) return Result(RetailSurfaceQueryStatus.ResourceLimit, "Sparse index exceeds its explicitly configured record budget.");
        if (sparseBlobOffset is not int offset || !records.TryGetValue(offset, out var record))
            return Result(RetailSurfaceQueryStatus.InvalidIdentity, "Sparse identity requires a parsed record boundary.");
        return new(RetailSurfaceQueryStatus.Found, new(path, node, cellX, cellY, record), default);
    }

    /// <summary>Finite, nonnegative, in-world coordinates that do not narrow/wrap int16 are supported. Native out-of-domain casts remain explicitly unsupported.</summary>
    public RetailSurfaceCandidates EnumerateSurfaceCandidates(float worldX, float worldY, float queryZ)
    {
        if (!float.IsFinite(worldX) || !float.IsFinite(worldY) || !float.IsFinite(queryZ))
            return Empty(RetailSurfaceQueryStatus.Unsupported, "Nonfinite coordinate conversion is unsupported.");
        float x = worldX * 2f, y = worldY * 2f, z = queryZ * 100f;
        if (worldX < 0 || worldY < 0 || worldX >= path.WorldWidthMetres || worldY >= path.WorldHeightMetres)
            return Empty(RetailSurfaceQueryStatus.OutsideWorld, "World coordinates are outside the explicit world bounds.");
        if (x >= 32768f || y >= 32768f || !TryInt32(z, out int cm))
            return Empty(RetailSurfaceQueryStatus.Unsupported, "Coordinate conversion would narrow/wrap or exceed int32 centimetres.");
        return EnumerateSurfaceCandidates((int)x, (int)y, cm);
    }

    /// <summary>
    /// Enumerates grid candidates and same-XY sparse records in reverse serialized layer order, retaining record order.
    /// Complete means this enumeration finished, not that native bsearch or actor surface selection was reproduced.
    /// </summary>
    public RetailSurfaceCandidates EnumerateSurfaceCandidates(int cellX, int cellY, int queryCentimetres)
    {
        var domain = CellDomain(cellX, cellY);
        if (domain != RetailSurfaceQueryStatus.Found) return new(domain, cellX, cellY, queryCentimetres, Array.Empty<RetailSurfaceCandidate>());
        var candidates = new List<RetailSurfaceCandidate>();
        var sector = path.GetSector(cellX / 32, cellY / 32);
        bool ambiguous = false, unsupported = false, missingTerrain = false;
        for (int layer = 0; layer < sector.Nodes.Count; layer++)
        {
            var node = sector.GetLayer(layer)!;
            if (node.Type != 16)
            {
                if (!Append(new(path, node, cellX, cellY, null))) return Limit();
                continue;
            }
            int matches = 0;
            RetailPathSparseRecord? previous = null;
            bool ordered = true;
            foreach (var record in node.SparseRecords)
            {
                if (record.HeightCentimetres < int.MinValue + 69 || record.HeightCentimetres > int.MaxValue - 69) unsupported = true;
                if (previous is not null && CompareRecord(previous, record) > 0) ordered = false;
                previous = record;
                if (record.CellX != cellX || record.CellY != cellY) continue;
                if (!Append(new(path, node, cellX, cellY, record))) return Limit();
                if (candidates[^1].Status == RetailSurfaceCandidateStatus.WithinTolerance) matches++;
            }
            ambiguous |= matches > 1;
            unsupported |= !ordered;
        }
        var status = unsupported ? RetailSurfaceQueryStatus.Unsupported : ambiguous ? RetailSurfaceQueryStatus.Ambiguous :
            missingTerrain ? RetailSurfaceQueryStatus.TerrainUnavailable : RetailSurfaceQueryStatus.Complete;
        return new(status, cellX, cellY, queryCentimetres, candidates.AsReadOnly(), unsupported
            ? "Unordered sparse records or unsupported integer arithmetic; no native bsearch selection is claimed."
            : ambiguous ? "Multiple records in one sparse node satisfy native tolerance; exact bsearch selection is unresolved." : null);

        bool Append(RetailPathSurfaceRef surface)
        {
            if (candidates.Count >= options.MaxCandidates) return false;
            var height = ReadHeight(surface);
            var candidateStatus = height.Status switch
            {
                RetailSurfaceHeightStatus.NoGround => RetailSurfaceCandidateStatus.NoGround,
                RetailSurfaceHeightStatus.TerrainUnavailable => RetailSurfaceCandidateStatus.TerrainUnavailable,
                RetailSurfaceHeightStatus.Unsupported => RetailSurfaceCandidateStatus.Unsupported,
                _ => RetailSurfaceCandidateStatus.OutsideTolerance
            };
            if (height.Status == RetailSurfaceHeightStatus.Resolved && height.ComparisonCentimetres is null)
                candidateStatus = RetailSurfaceCandidateStatus.Unsupported;
            if (height.Status == RetailSurfaceHeightStatus.Resolved && height.ComparisonCentimetres is int target)
            {
                long difference = (long)queryCentimetres - target;
                // Native signed-int32 subtraction/abs has exceptional overflow cases. Refuse that domain explicitly.
                if (difference <= int.MinValue || difference > int.MaxValue) candidateStatus = RetailSurfaceCandidateStatus.Unsupported;
                else if (System.Math.Abs(difference) < 70) candidateStatus = RetailSurfaceCandidateStatus.WithinTolerance;
            }
            unsupported |= candidateStatus == RetailSurfaceCandidateStatus.Unsupported;
            missingTerrain |= candidateStatus == RetailSurfaceCandidateStatus.TerrainUnavailable;
            candidates.Add(new(surface, candidateStatus, height));
            return true;
        }
        RetailSurfaceCandidates Limit() => new(RetailSurfaceQueryStatus.ResourceLimit, cellX, cellY, queryCentimetres,
            candidates.AsReadOnly(), "Candidate prefix retained; enumeration exceeded its explicit count budget.");
    }

    public RetailSurfaceHeight ReadHeight(RetailPathSurfaceRef surface)
    {
        if (!Owns(surface)) return Height(RetailSurfaceHeightStatus.Unsupported, default, -1);
        int offset = surface.SparseRecord?.FileOffset ?? surface.Node.FileOffset + 18;
        if (surface.Node.Type == 16)
        {
            if (surface.SparseRecord is not { } record) return Height(RetailSurfaceHeightStatus.Unsupported, default, offset);
            var rawSparse = new RetailPathHeight(RetailPathHeightKind.ExplicitCentimetres, record.HeightCentimetres);
            return new(RetailSurfaceHeightStatus.Resolved, record.HeightCentimetres / 100f, record.HeightCentimetres, rawSparse, offset);
        }
        var raw = surface.Node.GetHeight(surface.LocalCellIndex);
        if (raw.Kind == RetailPathHeightKind.NoGround) return Height(RetailSurfaceHeightStatus.NoGround, raw, offset);
        float metres;
        if (raw.Kind == RetailPathHeightKind.Terrain)
        {
            if (!terrainUsable || terrain is null) return Height(RetailSurfaceHeightStatus.TerrainUnavailable, raw, offset);
            var sample = terrain.SampleTerrain(surface.CellX / 4, surface.CellY / 4, surface.CentreXMetres, surface.CentreYMetres);
            if (sample.Status != RetailTerrainSampleStatus.Sampled) return Height(RetailSurfaceHeightStatus.Unsupported, raw, offset);
            metres = sample.HeightMetres;
        }
        else metres = raw.Centimetres / 100f;
        // Flat lookups compare the raw int32, while terrain/simple compare a cached float32 round-trip.
        if (surface.Node.Type >= 4 && metres <= -10240f) return Height(RetailSurfaceHeightStatus.NoGround, raw, offset);
        int cm;
        if (surface.Node.Type <= 1) cm = raw.Centimetres;
        else if (!TryInt32(metres * 100f, out cm))
            return new(RetailSurfaceHeightStatus.Resolved, metres, null, raw, offset);
        return new(RetailSurfaceHeightStatus.Resolved, metres, cm, raw, offset);
    }

    /// <summary>Returns directed data only. Found may name NoGround or mismatched sparse coordinates on a boundary; it is never actor passability.</summary>
    public RetailSurfaceNeighbor GetCardinalNeighbor(RetailPathSurfaceRef source, int direction) =>
        GetCardinalNeighborCore(source, direction) with { Source = source };

    private RetailSurfaceNeighbor GetCardinalNeighborCore(RetailPathSurfaceRef source, int direction)
    {
        if ((uint)direction >= 4) return Result(RetailSurfaceQueryStatus.Unsupported, "Only cardinal native directions 0:+X,1:+Y,2:-X,3:-Y are supported.");
        if (!Owns(source)) return Result(RetailSurfaceQueryStatus.InvalidIdentity, "Source belongs to another file/query domain or is not a valid native identity.");
        int tx = source.CellX + (direction == 0 ? 1 : direction == 2 ? -1 : 0);
        int ty = source.CellY + (direction == 1 ? 1 : direction == 3 ? -1 : 0);
        bool crossing = tx / 32 != source.CellX / 32 || ty / 32 != source.CellY / 32 || tx < 0 || ty < 0;
        var node = source.Node;
        if (node.Type == 16)
        {
            if (source.SparseRecord is null) return Result(RetailSurfaceQueryStatus.Unsupported, "Native grid identity on a sparse node has unresolved downstream interpretation.");
            var raw = source.SparseRecord.GetLink(direction);
            // The same raw pointer can be consumed through a different sparse node. Short links
            // are relative to this consumer node's base, not whichever descriptor first parsed it.
            uint decoded = raw.Encoding == 1 ? unchecked(((uint)node.BlobOffset + raw.StoredValue) << 7) : raw.DecodedValue;
            var kind = raw.Encoding == 1 ? RetailPathLinkKind.SparseRelative : raw.Encoding == 2 ? RetailPathLinkKind.SparseFull : RetailPathLinkKind.SparseAbsent;
            var provenance = new RetailPathLinkProvenance(kind, raw.FileOffset, raw.StoredValue, decoded, crossing);
            if (decoded == 0 || raw.Encoding is 0 or 3) return new(RetailSurfaceQueryStatus.NoLink, null, provenance);
            return Follow(decoded, tx, ty, crossing ? null : node, false, provenance);
        }
        if (!crossing)
        {
            var provenance = new RetailPathLinkProvenance(RetailPathLinkKind.LocalGrid, node.FileOffset, 0, 0, false);
            if ((node.Type & 1) != 0)
            {
                int mask = node.GetExitMask(source.LocalCellIndex);
                provenance = new(RetailPathLinkKind.LocalExitMask, node.FileOffset + 18 + node.Payload.Length + source.LocalCellIndex / 2, (uint)mask, 0, false);
                if ((mask & (1 << direction)) == 0) return new(RetailSurfaceQueryStatus.NoLink, null, provenance);
            }
            var domain = CellDomain(tx, ty);
            if (domain != RetailSurfaceQueryStatus.Found) return new(domain, null, provenance);
            var target = new RetailPathSurfaceRef(path, node, tx, ty, null);
            if (node.Type >= 4)
            {
                var height = ReadHeight(target);
                if (height.Status == RetailSurfaceHeightStatus.NoGround) return new(RetailSurfaceQueryStatus.NoLink, null, provenance);
                if (height.Status != RetailSurfaceHeightStatus.Resolved) return new(height.Status == RetailSurfaceHeightStatus.TerrainUnavailable
                    ? RetailSurfaceQueryStatus.TerrainUnavailable : RetailSurfaceQueryStatus.Unsupported, target, provenance);
            }
            return new(RetailSurfaceQueryStatus.Found, target, provenance);
        }
        int along = (direction & 1) == 0 ? source.CellY & 31 : source.CellX & 31;
        int edgeOffset = node.FileOffset + 2 + direction * 4;
        if ((node.EdgeMode & (1 << direction)) != 0)
        {
            uint bits = node.Edges[direction];
            var provenance = new RetailPathLinkProvenance(RetailPathLinkKind.EdgeMask, edgeOffset, bits, 0, true);
            if ((bits & (1u << along)) != 0) return new(RetailSurfaceQueryStatus.NoLink, null, provenance);
            return Follow(0, tx, ty, null, true, provenance);
        }
        long index = (long)node.Edges[direction] + along;
        if (index >= path.LinkTableCount) return new(RetailSurfaceQueryStatus.Malformed, null,
            new(RetailPathLinkKind.EdgePortal, edgeOffset, node.Edges[direction], 0, true), "Portal table index is outside the parsed input.");
        uint link = path.GetLinkTableEntry((int)index);
        var portal = new RetailPathLinkProvenance(RetailPathLinkKind.EdgePortal, path.LinkTableFileOffset + (int)index * 4, link, link, true);
        return link == 0 ? new(RetailSurfaceQueryStatus.NoLink, null, portal) : Follow(link, tx, ty, null, false, portal);
    }

    private RetailSurfaceNeighbor Follow(uint link, int x, int y, RetailPathNode? sameSector, bool mask, RetailPathLinkProvenance provenance)
    {
        var domain = CellDomain(x, y);
        if (domain != RetailSurfaceQueryStatus.Found) return new(domain, null, provenance);
        var node = sameSector ?? path.GetSector(x / 32, y / 32).GetLayer(mask ? 0 : (int)(link & 127));
        if (node is null) return new(RetailSurfaceQueryStatus.Malformed, null, provenance, "Destination layer does not exist.");
        if (mask || node.Type != 16)
        {
            var target = new RetailPathSurfaceRef(path, node, x, y, null);
            // Common mask branch writes a grid identity unconditionally; do not turn it into a guessed sparse pointer.
            return node.Type == 16 ? new(RetailSurfaceQueryStatus.Unsupported, target, provenance, "Mask branch produces grid identity on a sparse node; downstream interpretation is unresolved.") :
                new(RetailSurfaceQueryStatus.Found, target, provenance);
        }
        if (!SparseIndexAvailable) return new(RetailSurfaceQueryStatus.ResourceLimit, null, provenance, "Sparse index exceeds its explicit count budget.");
        if (!records.TryGetValue((int)(link >> 7), out var record))
            return new(RetailSurfaceQueryStatus.Malformed, null, provenance, "Link does not name a parsed sparse record boundary.");
        return new(RetailSurfaceQueryStatus.Found, new(path, node, x, y, record), provenance,
            record.CellX != x || record.CellY != y ? "Raw sparse record XY differs from the adjacent identity; pointer identity is retained." : null);
    }

    private RetailSurfaceQueryStatus CellDomain(int x, int y)
    {
        if (x < 0 || y < 0 || (long)x >= (long)path.WorldWidthMetres * 2 || (long)y >= (long)path.WorldHeightMetres * 2)
            return RetailSurfaceQueryStatus.OutsideWorld;
        return x > short.MaxValue || y > short.MaxValue ? RetailSurfaceQueryStatus.Unsupported : RetailSurfaceQueryStatus.Found;
    }
    private bool SupportedCell(int x, int y) => CellDomain(x, y) == RetailSurfaceQueryStatus.Found;
    private bool Owns(RetailPathSurfaceRef? surface) => surface is not null && ReferenceEquals(surface.Owner, path) &&
        SupportedCell(surface.CellX, surface.CellY) && surface.CellX / 32 == surface.Node.SectorX && surface.CellY / 32 == surface.Node.SectorY &&
        ReferenceEquals(path.GetSector(surface.Node.SectorX, surface.Node.SectorY).GetLayer(surface.Layer), surface.Node);
    private static bool TryInt32(float value, out int result)
    {
        if (!float.IsFinite(value) || value < -2147483648f || value >= 2147483648f) { result = 0; return false; }
        result = (int)value; return true;
    }
    private static int CompareRecord(RetailPathSparseRecord a, RetailPathSparseRecord b) => a.CellX != b.CellX ? a.CellX.CompareTo(b.CellX) :
        a.CellY != b.CellY ? a.CellY.CompareTo(b.CellY) : a.HeightCentimetres.CompareTo(b.HeightCentimetres);
    private static RetailSurfaceHeight Height(RetailSurfaceHeightStatus status, RetailPathHeight raw, int offset) => new(status, float.NaN, null, raw, offset);
    private static RetailSurfaceNeighbor Result(RetailSurfaceQueryStatus status, string detail) => new(status, null, default, detail);
    private static RetailSurfaceCandidates Empty(RetailSurfaceQueryStatus status, string detail) => new(status, 0, 0, 0, Array.Empty<RetailSurfaceCandidate>(), detail);
}
