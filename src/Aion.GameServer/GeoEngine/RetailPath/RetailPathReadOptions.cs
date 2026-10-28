namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>Explicit world dimensions and defensive limits for the inactive retail 6.5 reader.</summary>
public sealed record RetailPathReadOptions(int WorldWidthMetres, int WorldHeightMetres)
{
    public int MaxInputBytes { get; init; } = 128 * 1024 * 1024;
    public int MaxSectors { get; init; } = 1_048_576;
    public int MaxNodes { get; init; } = 500_000;
    public int MaxSparseRecords { get; init; } = 1_000_000;
    public int MaxLinkTableEntries { get; init; } = 16_000_000;
    /// <summary>Conservative descriptor-cost estimate, not a measured managed-allocation guarantee.</summary>
    public long MaxMetadataBytes { get; init; } = 128L * 1024 * 1024;
    public int MaxDiagnostics { get; init; } = 128;
    public Guid? ExpectedCollisionGuid { get; init; }
    /// <summary>Other major-6 minors are reported rather than interpreted under strict mode.</summary>
    public bool AllowOtherMajor6MinorVersions { get; init; }
    public bool ValidateReferences { get; init; } = true;
    public bool RequireExactEnd { get; init; }
    public bool RejectOverlappingSparseRecords { get; init; }
}
