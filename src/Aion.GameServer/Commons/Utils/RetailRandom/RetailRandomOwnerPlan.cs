using System.Collections.Immutable;

namespace Aion.GameServer.Commons.Utils.RetailRandom;

/// <summary>
/// Inactive finite calculation over declared owned bytes and ordered requests. All candidates,
/// rejection decisions and state writes are computed. No result callbacks or runtime/system RNGs.
/// </summary>
public static class RetailRandomOwnerPlan
{
    public const int LogicalTraceBytesPerDraw = 40;

    public static RetailRandomTransactionResult Calculate(RetailRandomTransaction? transaction)
    {
        var completed = new List<RetailRandomCompletedOperation>();
        var draws = new List<RetailRandomDrawTrace>();
        RetailRandomEngine.Mutable? engine = null;
        bool sourceMatches = false, ownershipConsistent = false, bytesVerified = false;
        var origin = transaction?.Origin ?? (RetailRandomInputOrigin)(-1);
        ulong generation = transaction?.InitialGeneration ?? 0;
        int ordinal = -1;
        string stage = "adapter.transaction";
        RetailRandomTransactionResult Result(RetailRandomStatus status, string detail)
        {
            var snapshot = engine?.Snapshot();
            bool success = status == RetailRandomStatus.Completed;
            return new(status, origin, ordinal, stage, detail, completed.ToImmutableArray(), draws.ToImmutableArray(),
                success ? snapshot : null, success ? null : snapshot, success ? generation : null, generation,
                engine is not null, new(sourceMatches, ownershipConsistent, bytesVerified),
                (long)draws.Count * LogicalTraceBytesPerDraw);
        }
        static void Reject(RetailRandomStatus status, string message) => throw new AdmissionFailure(status, message);
        static bool Present(string? value) => !string.IsNullOrWhiteSpace(value);
        try
        {
            if (transaction is null) Reject(RetailRandomStatus.InvalidRequest, "Transaction is absent");
            // Require flow annotation avoids null-forgiving assumptions after the rejection helper.
            Require(transaction is not null);
            if (!Enum.IsDefined(origin)) Reject(RetailRandomStatus.InvalidRequest, "Undefined input origin");
            if (transaction.Operations.IsDefault) Reject(RetailRandomStatus.InvalidRequest, "Operation array is absent");
            var limits = transaction.Limits;
            if (limits is null) Reject(RetailRandomStatus.InvalidRequest, "Explicit adapter capacities are required");
            Require(limits is not null);
            if (limits.MaxOperations < 0 || limits.MaxRawDraws < 0 || limits.MaxTraceBytes < 0)
                Reject(RetailRandomStatus.InvalidRequest, "Negative adapter capacity");
            // Adapter source admission is distinct from the native engine-read bypass.
            // Equal/bounded-one cannot establish the requested retail source if its declared pins differ.
            stage = "adapter.source_declarations";
            var sources = transaction.Sources;
            if (sources is null ||
                !string.Equals(sources.MainBinarySha256, RetailRandomEngine.MainBinarySha256, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(sources.RngEvidenceSha256, RetailRandomEngine.RngEvidenceSha256, StringComparison.OrdinalIgnoreCase))
                Reject(RetailRandomStatus.UnsupportedSourceIdentity, "Declared binary/evidence does not match the exact pinned source");
            sourceMatches = true;
            var requests = new HashSet<string>(StringComparer.Ordinal);

            void EnsureState()
            {
                if (engine is not null) return;
                stage = "adapter.ownership_declaration";
                var owner = transaction.Ownership;
                if (owner is null || owner.Origin != origin || !Enum.IsDefined(owner.Origin) ||
                    !Present(owner.StreamIdentity) || !Present(owner.LifetimeIdentity) || !Present(owner.OrderIdentity) ||
                    !Present(owner.AcquisitionDeclarationIdentity) || !owner.ExclusiveSerializedIntervalDeclared ||
                    owner.StorageAddress != RetailRandomEngine.NativeEngineAddress)
                    Reject(RetailRandomStatus.UnknownCurrentOwnership, "Missing or inconsistent exclusive ownership declaration; no acquisition is attested");
                Require(owner is not null);
                if (owner.InitialGeneration != transaction.InitialGeneration)
                    Reject(RetailRandomStatus.StaleGeneration, "Initial ownership generation differs from transaction");
                ownershipConsistent = true;
                stage = "adapter.state_bytes";
                if (transaction.InitialStateBytes.IsDefault)
                    Reject(RetailRandomStatus.MissingState, "Reached state consumer has no complete owned image");
                if (!RetailRandomState.TryCreate(transaction.InitialStateBytes.AsSpan(), transaction.InitialStateSha256,
                        out var state, out var stateStatus))
                    Reject(stateStatus, "Reached state image length/hash is invalid");
                Require(state is not null);
                bytesVerified = true;
                engine = new(state);
            }

            void EnsureDescriptor()
            {
                stage = "adapter.fixed_descriptor";
                var descriptor = transaction.Descriptor;
                if (descriptor is null || descriptor.EngineAddress != RetailRandomEngine.NativeEngineAddress ||
                    descriptor.BitWidth != 64 || descriptor.Cutoff != ulong.MaxValue)
                    Reject(RetailRandomStatus.UnsupportedDescriptor, "Only the actual fixed64/max descriptor is admitted");
            }

            RetailRandomSeedReturnDeclaration AdmitSeed(RetailRandomOperation operation)
            {
                stage = "constructor.external_seed_return";
                var seed = operation.SeedReturn;
                if (seed is null) Reject(RetailRandomStatus.SeedReturnNotBound, "Original uint32 provider return declaration is absent");
                Require(seed is not null);
                if (!Enum.IsDefined(seed.Kind)) Reject(RetailRandomStatus.SeedReturnNotBound, "Undefined seed return kind");
                if (seed.Kind != RetailRandomSeedReturnKind.NormalReturn)
                    Reject(RetailRandomStatus.IncompleteAtSeedProvider, "Original provider error/unknown return is outside this calculated constructor suffix");
                var owner = transaction.Ownership;
                if (owner is null || !Enum.IsDefined(seed.Origin) || seed.Origin != origin ||
                    seed.StreamIdentity != owner.StreamIdentity || seed.LifetimeIdentity != owner.LifetimeIdentity ||
                    seed.OrderIdentity != owner.OrderIdentity || seed.RequestIdentity != operation.RequestIdentity ||
                    seed.ExpectedGeneration != generation || seed.NormalReturnSite != RetailRandomEngine.NormalSeedReturnSite ||
                    !Present(seed.ReceiptDeclarationIdentity))
                    Reject(RetailRandomStatus.SeedReturnNotBound, "Seed return is not bound to this exact stream/lifetime/request/generation/site");
                return seed;
            }

            RetailRandomRawStep Draw(RetailRandomDrawDisposition disposition)
            {
                stage = "engine.before_raw_draw";
                // Reject before a state mutation or trace allocation. These capacities are not native limits.
                if (draws.Count >= limits.MaxRawDraws ||
                    (long)draws.Count * LogicalTraceBytesPerDraw > limits.MaxTraceBytes - LogicalTraceBytesPerDraw)
                    Reject(RetailRandomStatus.BudgetExceeded, "Raw draw or logical trace capacity reached; native would continue");
                Require(engine is not null);
                var step = engine.Draw();
                draws.Add(new((ulong)draws.Count, ordinal, step.IndexBefore, step.SelectedIndex,
                    step.IndexAfter, step.Value, step.Regeneration, disposition));
                return step;
            }

            uint Bounded(uint bound)
            {
                // bound1 bypass is handled before state/descriptor admission in the dispatcher.
                ulong qMax = ulong.MaxValue / bound;
                ulong rMax = ulong.MaxValue % bound;
                while (true)
                {
                    ulong value = Draw(RetailRandomDrawDisposition.Rejected).Value;
                    bool accepted = value / bound < qMax || rMax == (ulong)bound - 1;
                    if (accepted)
                    {
                        draws[^1] = draws[^1] with { Disposition = RetailRandomDrawDisposition.Accepted };
                        stage = "bounded.accepted";
                        return (uint)(value % bound);
                    }
                    stage = "bounded.rejected_continue";
                }
            }

            for (ordinal = 0; ordinal < transaction.Operations.Length; ordinal++)
            {
                stage = "adapter.operation";
                if (ordinal >= limits.MaxOperations) Reject(RetailRandomStatus.BudgetExceeded, "Operation capacity reached");
                var operation = transaction.Operations[ordinal];
                if (operation is null || !Enum.IsDefined(operation.Kind) || operation.Ordinal != ordinal ||
                    !Present(operation.RequestIdentity) || !requests.Add(operation.RequestIdentity))
                    Reject(RetailRandomStatus.InvalidRequest, "Missing/undefined/out-of-order/duplicate request");
                Require(operation is not null);
                if (operation.ExpectedGeneration != generation)
                    Reject(RetailRandomStatus.StaleGeneration, "Ordered request generation differs from current calculated generation");
                if (generation == ulong.MaxValue)
                    Reject(RetailRandomStatus.InvalidRequest, "Adapter generation cannot advance without overflow");
                int beforeDraws = draws.Count;
                int? signedValue = null;
                uint? boundedValue = null;
                ulong? rawValue = null;
                switch (operation.Kind)
                {
                    case RetailRandomOperationKind.SignedInclusive when operation.First == operation.Second:
                        stage = "wrapper.equal_no_engine";
                        signedValue = operation.First;
                        break;
                    case RetailRandomOperationKind.BoundedUInt32 when operation.Bound == 1:
                        stage = "bounded.one_no_engine";
                        boundedValue = 0;
                        break;
                    case RetailRandomOperationKind.DiscardRaw when operation.DiscardCount == 0:
                        stage = "discard.zero_calls_no_engine";
                        break;
                    case RetailRandomOperationKind.ApplyNormalSeedReturn:
                        var seed = AdmitSeed(operation);
                        EnsureState();
                        stage = "constructor.apply_normal_return_suffix";
                        Require(engine is not null);
                        engine.ApplyNormalSeedReturn(seed.Value);
                        break;
                    case RetailRandomOperationKind.SignedInclusive:
                        EnsureState(); EnsureDescriptor();
                        int lo = System.Math.Min(operation.First, operation.Second);
                        int hi = System.Math.Max(operation.First, operation.Second);
                        ulong width = (ulong)((long)hi - lo) + 1;
                        uint sample = width == 0x100000000UL
                            ? unchecked((uint)Draw(RetailRandomDrawDisposition.Accepted).Value)
                            : Bounded((uint)width);
                        signedValue = unchecked((int)(unchecked((uint)lo) + sample));
                        stage = width == 0x100000000UL ? "signed.full_range_low32" : "signed.bounded_add_min";
                        break;
                    case RetailRandomOperationKind.Raw64:
                        EnsureState(); EnsureDescriptor();
                        rawValue = Draw(RetailRandomDrawDisposition.Raw).Value;
                        stage = "raw64.return";
                        break;
                    case RetailRandomOperationKind.BoundedUInt32:
                        if (operation.Bound == 0)
                            Reject(RetailRandomStatus.InvalidRequest, "Bound zero has no admitted result; native division is invalid");
                        EnsureState(); EnsureDescriptor();
                        boundedValue = Bounded(operation.Bound);
                        break;
                    case RetailRandomOperationKind.DiscardRaw:
                        EnsureState(); EnsureDescriptor();
                        for (ulong n = 0; n < operation.DiscardCount; n++) Draw(RetailRandomDrawDisposition.Discarded);
                        stage = "discard.finite_calls_complete_not_whole_startup";
                        break;
                    default:
                        Reject(RetailRandomStatus.InvalidRequest, "Undefined operation");
                        break;
                }
                ulong previous = generation++;
                completed.Add(new(ordinal, operation.RequestIdentity, operation.Kind, previous, generation,
                    signedValue, boundedValue, rawValue, (ulong)(draws.Count - beforeDraws), engine is not null,
                    engine?.Snapshot().Sha256));
            }
            ordinal = transaction.Operations.Length;
            stage = "transaction.calculated_host_effects_unexecuted";
            return Result(RetailRandomStatus.Completed, "Complete finite calculation; external current-memory declarations remain unattested");
        }
        catch (AdmissionFailure error)
        {
            return Result(error.Status, error.Message);
        }
    }

    private sealed class AdmissionFailure(RetailRandomStatus status, string message) : Exception(message)
    {
        internal RetailRandomStatus Status { get; } = status;
    }

    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition)
    {
        if (!condition) throw new InvalidOperationException("Internal admission invariant");
    }
}
