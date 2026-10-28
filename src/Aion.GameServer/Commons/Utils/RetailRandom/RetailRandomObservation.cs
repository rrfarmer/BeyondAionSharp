using System.Collections.Immutable;

namespace Aion.GameServer.Commons.Utils.RetailRandom;

public enum RetailRandomInputOrigin { ConstructedFixture, DeclaredCurrentMemory }
public enum RetailRandomOperationKind { ApplyNormalSeedReturn, SignedInclusive, Raw64, BoundedUInt32, DiscardRaw }
public enum RetailRandomSeedReturnKind { NormalReturn, ProviderErrorOutsideUnit, Unknown }
public enum RetailRandomDrawDisposition { Raw, Discarded, Accepted, Rejected }

/// <summary>Exact declared executable/source identities; matching them does not attest a live process.</summary>
public sealed record RetailRandomSourceDeclaration(string MainBinarySha256, string RngEvidenceSha256);

/// <summary>
/// Caller declaration of one serialized interval. This unit cannot acquire memory, prove exclusivity,
/// observe a seed provider or verify an external acquisition receipt. DeclaredCurrentMemory retains that meaning.
/// </summary>
public sealed record RetailRandomOwnershipDeclaration(
    RetailRandomInputOrigin Origin, string StreamIdentity, string LifetimeIdentity, string OrderIdentity,
    string AcquisitionDeclarationIdentity, ulong StorageAddress, ulong InitialGeneration,
    bool ExclusiveSerializedIntervalDeclared);

/// <summary>The actual signed-mapper descriptor is fixed64/max at the pinned global engine address.</summary>
public sealed record RetailRandomDescriptorDeclaration(ulong EngineAddress, ulong BitWidth, ulong Cutoff);

/// <summary>
/// Original external uint32 seed return, not a chosen generated RNG result. A normal declaration admits
/// only the constructor writes after that return; it never proves that the original provider executed.
/// </summary>
public sealed record RetailRandomSeedReturnDeclaration(
    RetailRandomSeedReturnKind Kind, uint Value, RetailRandomInputOrigin Origin,
    string StreamIdentity, string LifetimeIdentity, string OrderIdentity, string RequestIdentity,
    ulong ExpectedGeneration, ulong NormalReturnSite, string ReceiptDeclarationIdentity);

public sealed record RetailRandomOperation(
    int Ordinal, string RequestIdentity, RetailRandomOperationKind Kind, ulong ExpectedGeneration,
    int First = 0, int Second = 0, uint Bound = 0, ulong DiscardCount = 0,
    RetailRandomSeedReturnDeclaration? SeedReturn = null);

/// <summary>
/// Defensive adapter capacities, never native limits. Trace bytes charge the fixed logical40-byte raw
/// trace schema; they are not measured CLR allocation bytes or a global memory guarantee. Snapshots,
/// completed operation records, strings and collection overhead have separate bounded-by-count costs.
/// </summary>
public sealed record RetailRandomLimits
{
    public int MaxOperations { get; init; } = 4096;
    public int MaxRawDraws { get; init; } = 1_000_000;
    public long MaxTraceBytes { get; init; } = 40_000_000;
}

/// <summary>
/// The state is an immutable caller-owned byte image. A default array means absent; a nondefault empty
/// array is a present image with invalid size. Equal/bounded-one operations never admit or hash that image.
/// </summary>
public sealed record RetailRandomTransaction(
    RetailRandomInputOrigin Origin, ulong InitialGeneration, ImmutableArray<RetailRandomOperation> Operations,
    ImmutableArray<byte> InitialStateBytes = default, string? InitialStateSha256 = null,
    RetailRandomSourceDeclaration? Sources = null, RetailRandomOwnershipDeclaration? Ownership = null,
    RetailRandomDescriptorDeclaration? Descriptor = null, RetailRandomLimits? Limits = null);


public readonly record struct RetailRandomDrawTrace(
    ulong DrawOrdinal, int OperationOrdinal, uint IndexBefore, uint SelectedIndex, uint IndexAfter,
    ulong Value, RetailRandomRegeneration Regeneration, RetailRandomDrawDisposition Disposition);

/// <summary>Generation increments are adapter accounting, not native stores.</summary>
public sealed record RetailRandomCompletedOperation(
    int Ordinal, string RequestIdentity, RetailRandomOperationKind Kind, ulong GenerationBefore,
    ulong GenerationAfter, int? SignedValue, uint? BoundedValue, ulong? RawValue, ulong RawDrawsConsumed,
    bool StateAdmitted, string? StateAfterSha256);

/// <summary>Only byte hash validation and declared metadata checks are performed.</summary>
public readonly record struct RetailRandomValidation(
    bool SourceDeclarationsMatchPins, bool OwnershipDeclarationConsistent, bool StateBytesHashVerified);

public sealed record RetailRandomTransactionResult(
    RetailRandomStatus Status, RetailRandomInputOrigin InputOrigin, int ReachedOperationOrdinal,
    string ReachedStage, string Detail, ImmutableArray<RetailRandomCompletedOperation> CompletedPrefix,
    ImmutableArray<RetailRandomDrawTrace> Draws, RetailRandomState? CompleteFinalState,
    RetailRandomState? DiagnosticPrefixState, ulong? CompleteNextGeneration, ulong DiagnosticNextGeneration,
    bool EngineStateAdmitted, RetailRandomValidation Validation, long LogicalTraceBytesCharged)
{
    public bool Completed => Status == RetailRandomStatus.Completed;
    public bool HostEffectsExecuted => false;
    public bool OriginalSeedProviderExecuted => false;
    public bool CurrentMemoryAcquisitionAttested => false;
    public bool GlobalNativeCallOrderProved => false;
    public bool CompleteNativeStartupReplayed => false;
    public bool ActivationReady => false;
}
