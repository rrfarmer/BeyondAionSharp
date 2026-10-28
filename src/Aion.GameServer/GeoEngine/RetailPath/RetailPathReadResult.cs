namespace Aion.GameServer.GeoEngine.RetailPath;

public enum RetailPathErrorCode
{
    InvalidOptions, InputLimitExceeded, Truncated, UnsupportedVersion, InvalidLength,
    AllocationLimitExceeded, InvalidNodeCount, UnknownNodeType, InvalidSparseRecord,
    SparseRecordOverlap, LinkTableOutOfRange, TargetSectorOutOfRange,
    TargetLayerOutOfRange, TargetRecordOutOfRange, TrailingBytes, InputOutput
}

public sealed record RetailPathReadError(RetailPathErrorCode Code, long FileOffset, string Message);
public sealed record RetailPathDiagnostic(string Code, long FileOffset, string Message);

/// <summary>Structural reading and compatibility are separate; success never enables routing.</summary>
public sealed class RetailPathReadResult
{
    internal RetailPathReadResult(RetailPathData? data, RetailPathReadError? error,
        IReadOnlyList<RetailPathDiagnostic> diagnostics, int suppressedDiagnostics)
    {
        Data = data;
        Error = error;
        Diagnostics = diagnostics;
        SuppressedDiagnostics = suppressedDiagnostics;
    }

    public bool Success => Data is not null && Error is null;
    public RetailPathData? Data { get; }
    public RetailPathReadError? Error { get; }
    public IReadOnlyList<RetailPathDiagnostic> Diagnostics { get; }
    public int SuppressedDiagnostics { get; }
}
