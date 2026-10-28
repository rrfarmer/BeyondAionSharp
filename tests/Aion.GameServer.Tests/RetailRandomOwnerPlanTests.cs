using System.Collections.Immutable;
using System.Text.Json;
using Aion.GameServer.Commons.Utils.RetailRandom;
using Xunit;

namespace Aion.GameServer.Tests;

public sealed class RetailRandomOwnerPlanTests
{
    private static readonly Lazy<JsonElement> Conditional = new(() =>
    {
        using var corpus = RetailRandomFixtures.Corpus();
        return RetailRandomFixtures.Data(corpus.RootElement.GetProperty("conditional"));
    });
    private static byte[] Initial => RetailRandomFixtures.Bytes(Conditional.Value.GetProperty("cases")[0].GetProperty("initial_storage"));
    private static RetailRandomTransaction Raw => RetailRandomFixtures.Transaction(Initial,
        RetailRandomFixtures.Operation(0, RetailRandomOperationKind.Raw64));
    private static JsonElement Case(string id) => Conditional.Value.GetProperty("cases").EnumerateArray().Single(x => x.GetProperty("id").GetString() == id);

    [Fact]
    public void EntireIndependentElevenOperationProducerAndCallSequenceHasExactValuesWritesAnd317WordConsumption()
    {
        using var corpus = RetailRandomFixtures.Corpus();
        var expected = RetailRandomFixtures.Data(corpus.RootElement.GetProperty("composed"));
        var operations = new List<RetailRandomOperation>();
        foreach (var item in expected.GetProperty("operations").EnumerateArray())
        {
            int ordinal = item.GetProperty("ordinal").GetInt32();
            var inputs = item.GetProperty("inputs");
            var kind = Enum.Parse<RetailRandomOperationKind>(item.GetProperty("kind").GetString()!);
            operations.Add(RetailRandomFixtures.Operation(ordinal, kind,
                inputs.TryGetProperty("first_i32", out var first) ? first.GetInt32() : 0,
                inputs.TryGetProperty("second_i32", out var second) ? second.GetInt32() : 0,
                inputs.TryGetProperty("bound_u32", out var bound) ? bound.GetUInt32() : 0,
                inputs.TryGetProperty("count", out var count) ? count.GetUInt64() : 0,
                inputs.TryGetProperty("seed_u32", out var seed) ? seed.GetUInt32() : null));
        }
        var input = RetailRandomFixtures.Transaction(RetailRandomFixtures.Bytes(expected.GetProperty("initial_storage")), operations.ToArray());
        var result = RetailRandomOwnerPlan.Calculate(input);
        Assert.True(result.Completed, $"{result.ReachedStage}: {result.Detail}");
        Assert.Equal(11, result.CompletedPrefix.Length); Assert.Equal(317, result.Draws.Length);
        Assert.Equal(11UL, result.CompleteNextGeneration); Assert.Equal(317L * 40, result.LogicalTraceBytesCharged);
        Assert.NotNull(result.CompleteFinalState);
        Assert.Equal(RetailRandomFixtures.Bytes(expected.GetProperty("final_state")), result.CompleteFinalState.Bytes.ToArray());
        Assert.Equal("891cc5a9e987bf785ed9281c76d9432faf6bb6391cc596e0c7252d21f71bf334", result.CompleteFinalState.Sha256);
        for (int ordinal = 0; ordinal < 11; ordinal++)
        {
            var item = expected.GetProperty("operations")[ordinal];
            var actual = result.CompletedPrefix[ordinal];
            Assert.Equal(item.GetProperty("post_state").GetProperty("sha256").GetString(), actual.StateAfterSha256);
            Assert.Equal((ulong)ordinal, actual.GenerationBefore); Assert.Equal((ulong)ordinal + 1, actual.GenerationAfter);
            var expectedValue = item.GetProperty("value");
            if (actual.Kind == RetailRandomOperationKind.SignedInclusive) Assert.Equal<int?>(expectedValue.GetInt32(), actual.SignedValue);
            if (actual.Kind == RetailRandomOperationKind.BoundedUInt32) Assert.Equal<uint?>(expectedValue.GetUInt32(), actual.BoundedValue);
            if (actual.Kind == RetailRandomOperationKind.Raw64) Assert.Equal<ulong?>(RetailRandomFixtures.Hex(expectedValue.GetString()!), actual.RawValue);
            var traces = result.Draws.Where(x => x.OperationOrdinal == ordinal).ToArray();
            var expectedTraces = item.GetProperty("trace");
            Assert.Equal(expectedTraces.GetArrayLength(), traces.Length);
            for (int n = 0; n < traces.Length; n++)
            {
                bool? accepted = expectedTraces[n].TryGetProperty("accepted", out var accept) ? accept.GetBoolean() : null;
                RetailRandomFixtures.Trace(expectedTraces[n], traces[n], ordinal, accepted);
                if (actual.Kind == RetailRandomOperationKind.DiscardRaw) Assert.Equal(RetailRandomDrawDisposition.Discarded, traces[n].Disposition);
                if (actual.Kind == RetailRandomOperationKind.Raw64) Assert.Equal(RetailRandomDrawDisposition.Raw, traces[n].Disposition);
            }
        }
        Assert.Equal(input.InitialStateBytes.ToArray(), RetailRandomFixtures.Bytes(expected.GetProperty("initial_storage")));
        RetailRandomFixtures.NoExecutionClaims(result);
    }

    [Fact]
    public void CurrentMemoryDeclarationDoesNotBecomeLiveObservationOrProviderExecution()
    {
        var input = Raw with { Origin = RetailRandomInputOrigin.DeclaredCurrentMemory,
            Ownership = RetailRandomFixtures.Ownership(RetailRandomInputOrigin.DeclaredCurrentMemory) };
        var result = RetailRandomOwnerPlan.Calculate(input);
        Assert.True(result.Completed); Assert.Equal(RetailRandomInputOrigin.DeclaredCurrentMemory, result.InputOrigin);
        Assert.True(result.Validation.SourceDeclarationsMatchPins);
        Assert.True(result.Validation.OwnershipDeclarationConsistent); Assert.True(result.Validation.StateBytesHashVerified);
        RetailRandomFixtures.NoExecutionClaims(result);
    }

    [Theory]
    [InlineData("missing", 0)] [InlineData("binary", 0)] [InlineData("evidence", 0)]
    [InlineData("missing", 1)] [InlineData("binary", 1)] [InlineData("evidence", 1)]
    public void WrongStaticSourceIsRejectedEvenWhenNativeBypassNeverReadsEngine(string fault, int choice)
    {
        var operation = choice == 0 ? RetailRandomFixtures.Operation(0, RetailRandomOperationKind.SignedInclusive, 7, 7)
            : RetailRandomFixtures.Operation(0, RetailRandomOperationKind.BoundedUInt32, bound: 1);
        var input = Raw with { Operations = [operation] };
        input = input with { Sources = fault switch {
            "missing" => null,
            "binary" => RetailRandomFixtures.Sources with { MainBinarySha256 = new string('0', 64) },
            _ => RetailRandomFixtures.Sources with { RngEvidenceSha256 = new string('0', 64) } } };
        var result = RetailRandomOwnerPlan.Calculate(input);
        Assert.Equal(RetailRandomStatus.UnsupportedSourceIdentity, result.Status);
        Assert.Empty(result.CompletedPrefix); Assert.Empty(result.Draws); Assert.False(result.EngineStateAdmitted);
        Assert.Null(result.CompleteFinalState); Assert.Null(result.DiagnosticPrefixState);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void ValidStaticSourceBypassesAllUnreachedCurrentStateSeedOwnerAndDescriptorChecks(int choice)
    {
        var operation = choice == 0 ? RetailRandomFixtures.Operation(0, RetailRandomOperationKind.SignedInclusive, int.MinValue, int.MinValue)
            : RetailRandomFixtures.Operation(0, RetailRandomOperationKind.BoundedUInt32, bound: 1);
        var input = new RetailRandomTransaction(RetailRandomInputOrigin.ConstructedFixture, 0, [operation],
            InitialStateBytes: [1, 2], InitialStateSha256: "invalid-unreached-hash", Sources: RetailRandomFixtures.Sources,
            Ownership: RetailRandomFixtures.Ownership() with { Origin = (RetailRandomInputOrigin)99, ExclusiveSerializedIntervalDeclared = false },
            Descriptor: new(0, 0, 0), Limits: new() { MaxRawDraws = 0, MaxTraceBytes = 0 });
        var result = RetailRandomOwnerPlan.Calculate(input);
        Assert.True(result.Completed); Assert.True(result.Validation.SourceDeclarationsMatchPins);
        Assert.False(result.EngineStateAdmitted); Assert.False(result.Validation.StateBytesHashVerified);
        Assert.False(result.Validation.OwnershipDeclarationConsistent); Assert.Empty(result.Draws);
        Assert.Null(result.CompleteFinalState); Assert.Null(result.CompletedPrefix[0].StateAfterSha256);
    }

    [Theory]
    [InlineData("null")] [InlineData("origin")] [InlineData("kind")] [InlineData("ordinal")]
    [InlineData("empty-id")] [InlineData("absent-operations")] [InlineData("null-operation")] [InlineData("absent-limits")]
    public void MalformedAdapterRequestsDoNotProduceRandomResults(string fault)
    {
        RetailRandomTransaction? input = Raw;
        input = fault switch {
            "null" => null,
            "origin" => input with { Origin = (RetailRandomInputOrigin)99 },
            "kind" => input with { Operations = [input.Operations[0] with { Kind = (RetailRandomOperationKind)99 }] },
            "ordinal" => input with { Operations = [input.Operations[0] with { Ordinal = 1 }] },
            "empty-id" => input with { Operations = [input.Operations[0] with { RequestIdentity = " " }] },
            "absent-operations" => input with { Operations = default },
            "absent-limits" => input with { Limits = null },
            _ => input with { Operations = [null!] } };
        var result = RetailRandomOwnerPlan.Calculate(input);
        Assert.Equal(RetailRandomStatus.InvalidRequest, result.Status);
        Assert.Empty(result.Draws); Assert.Empty(result.CompletedPrefix); Assert.Null(result.CompleteFinalState);
        RetailRandomFixtures.NoExecutionClaims(result);
    }

    [Theory]
    [InlineData("missing")] [InlineData("short")] [InlineData("long")] [InlineData("hash-missing")] [InlineData("hash-wrong")]
    public void StateConsumingCallRequiresExactCompleteBytesAndTheirActualHash(string fault)
    {
        var input = Raw;
        input = fault switch {
            "missing" => input with { InitialStateBytes = default },
            "short" => input with { InitialStateBytes = ImmutableArray.CreateRange(new byte[5007]) },
            "long" => input with { InitialStateBytes = ImmutableArray.CreateRange(new byte[5009]) },
            "hash-missing" => input with { InitialStateSha256 = null },
            _ => input with { InitialStateSha256 = new string('0', 64) } };
        var result = RetailRandomOwnerPlan.Calculate(input);
        Assert.Equal(fault == "missing" ? RetailRandomStatus.MissingState : fault is "short" or "long" ? RetailRandomStatus.StateSizeMismatch : RetailRandomStatus.StateHashMismatch, result.Status);
        Assert.Empty(result.Draws); Assert.False(result.EngineStateAdmitted); Assert.Null(result.DiagnosticPrefixState);
    }

    [Theory]
    [InlineData("absent")] [InlineData("origin")] [InlineData("undefined-origin")] [InlineData("stream")] [InlineData("life")]
    [InlineData("order")] [InlineData("acquisition")] [InlineData("storage")] [InlineData("exclusive")]
    public void RequiredOwnerMetadataIsConsistentDeclarationWithoutPretendingToAttestIt(string fault)
    {
        var owner = RetailRandomFixtures.Ownership();
        var input = Raw with { Ownership = fault switch {
            "absent" => null,
            "origin" => owner with { Origin = RetailRandomInputOrigin.DeclaredCurrentMemory },
            "undefined-origin" => owner with { Origin = (RetailRandomInputOrigin)99 },
            "stream" => owner with { StreamIdentity = "" },
            "life" => owner with { LifetimeIdentity = "" },
            "order" => owner with { OrderIdentity = "" },
            "acquisition" => owner with { AcquisitionDeclarationIdentity = "" },
            "storage" => owner with { StorageAddress = 0 },
            _ => owner with { ExclusiveSerializedIntervalDeclared = false } } };
        var result = RetailRandomOwnerPlan.Calculate(input);
        Assert.Equal(RetailRandomStatus.UnknownCurrentOwnership, result.Status);
        Assert.Empty(result.Draws); Assert.False(result.Validation.OwnershipDeclarationConsistent);
        RetailRandomFixtures.NoExecutionClaims(result);
    }

    [Theory]
    [InlineData("initial-owner")] [InlineData("request")]
    public void StaleGenerationIsRejectedWithoutConsumingAnyWord(string fault)
    {
        var input = Raw;
        input = fault == "initial-owner" ? input with { Ownership = input.Ownership! with { InitialGeneration = 1 } }
            : input with { Operations = [input.Operations[0] with { ExpectedGeneration = 1 }] };
        var result = RetailRandomOwnerPlan.Calculate(input);
        Assert.Equal(RetailRandomStatus.StaleGeneration, result.Status); Assert.Empty(result.Draws);
        Assert.Empty(result.CompletedPrefix); Assert.Null(result.CompleteNextGeneration);
    }

    [Theory]
    [InlineData("absent")] [InlineData("address")] [InlineData("width")] [InlineData("cutoff")]
    public void UnclosedDescriptorDomainIsNotSilentlyNormalized(string fault)
    {
        var descriptor = RetailRandomFixtures.Descriptor;
        var input = Raw with { Descriptor = fault switch {
            "absent" => null, "address" => descriptor with { EngineAddress = 1 },
            "width" => descriptor with { BitWidth = 32 }, _ => descriptor with { Cutoff = uint.MaxValue } } };
        var result = RetailRandomOwnerPlan.Calculate(input);
        Assert.Equal(RetailRandomStatus.UnsupportedDescriptor, result.Status); Assert.Empty(result.Draws);
        Assert.Null(result.CompleteFinalState); Assert.NotNull(result.DiagnosticPrefixState);
        Assert.Equal(Initial, result.DiagnosticPrefixState.Bytes.ToArray());
    }

    [Theory]
    [InlineData("missing")] [InlineData("error")] [InlineData("unknown")] [InlineData("kind")]
    [InlineData("origin")] [InlineData("stream")] [InlineData("life")] [InlineData("order")]
    [InlineData("request")] [InlineData("generation")] [InlineData("site")] [InlineData("receipt")]
    public void SeedSuffixRequiresOriginalNormalReturnBindingAndNeverInventsProviderErrorEffects(string fault)
    {
        var operation = RetailRandomFixtures.Operation(0, RetailRandomOperationKind.ApplyNormalSeedReturn, seed: 5489);
        var seed = operation.SeedReturn!;
        operation = operation with { SeedReturn = fault switch {
            "missing" => null,
            "error" => seed with { Kind = RetailRandomSeedReturnKind.ProviderErrorOutsideUnit },
            "unknown" => seed with { Kind = RetailRandomSeedReturnKind.Unknown },
            "kind" => seed with { Kind = (RetailRandomSeedReturnKind)99 },
            "origin" => seed with { Origin = RetailRandomInputOrigin.DeclaredCurrentMemory },
            "stream" => seed with { StreamIdentity = "other" },
            "life" => seed with { LifetimeIdentity = "other" },
            "order" => seed with { OrderIdentity = "other" },
            "request" => seed with { RequestIdentity = "other" },
            "generation" => seed with { ExpectedGeneration = 1 },
            "site" => seed with { NormalReturnSite = 0 },
            _ => seed with { ReceiptDeclarationIdentity = "" } } };
        var result = RetailRandomOwnerPlan.Calculate(RetailRandomFixtures.Transaction(Initial, operation));
        Assert.Equal(fault is "error" or "unknown" ? RetailRandomStatus.IncompleteAtSeedProvider : RetailRandomStatus.SeedReturnNotBound, result.Status);
        Assert.Empty(result.Draws); Assert.Empty(result.CompletedPrefix); Assert.False(result.EngineStateAdmitted);
        Assert.Null(result.DiagnosticPrefixState); Assert.Null(result.CompleteFinalState);
        RetailRandomFixtures.NoExecutionClaims(result);
    }

    [Fact]
    public void ConstructorSuffixDoesNotRequireUnreachedDistributionDescriptorOrRawDrawBudget()
    {
        var expected = Case("seed4294967295-discard310");
        var input = RetailRandomFixtures.Transaction(RetailRandomFixtures.Bytes(expected.GetProperty("initial_storage")),
            RetailRandomFixtures.Operation(0, RetailRandomOperationKind.ApplyNormalSeedReturn, seed: uint.MaxValue))
            with { Descriptor = null, Limits = new() { MaxRawDraws = 0, MaxTraceBytes = 0 } };
        var result = RetailRandomOwnerPlan.Calculate(input);
        Assert.True(result.Completed); Assert.Empty(result.Draws); Assert.NotNull(result.CompleteFinalState);
        Assert.Equal(RetailRandomFixtures.Bytes(expected.GetProperty("constructor_state")), result.CompleteFinalState.Bytes.ToArray());
    }

    [Theory]
    [InlineData(0, 40)] [InlineData(1, 0)] [InlineData(1, 39)]
    public void RawCapacityFailsBeforeFirstMutationAndNeverFabricatesANativeReturn(int rawLimit, long traceBytes)
    {
        var input = Raw with { Limits = new() { MaxRawDraws = rawLimit, MaxTraceBytes = traceBytes } };
        var result = RetailRandomOwnerPlan.Calculate(input);
        Assert.Equal(RetailRandomStatus.BudgetExceeded, result.Status); Assert.Empty(result.Draws); Assert.Empty(result.CompletedPrefix);
        Assert.NotNull(result.DiagnosticPrefixState); Assert.Equal(Initial, result.DiagnosticPrefixState.Bytes.ToArray());
        Assert.Null(result.CompleteFinalState); Assert.Null(result.CompleteNextGeneration); Assert.Equal(0, result.LogicalTraceBytesCharged);
    }

    [Fact]
    public void ExactlyOneLogicalTraceRecordIsAdmittedAt40Bytes()
    {
        var result = RetailRandomOwnerPlan.Calculate(Raw with { Limits = new() { MaxRawDraws = 1, MaxTraceBytes = 40 } });
        Assert.True(result.Completed); Assert.Single(result.Draws); Assert.Equal(40, result.LogicalTraceBytesCharged);
    }

    [Theory]
    [InlineData("operations")] [InlineData("draws")] [InlineData("trace")]
    public void NegativeAdapterCapacitiesAreInvalidEvenForNoDrawBypass(string which)
    {
        var limits = which switch { "operations" => new RetailRandomLimits { MaxOperations = -1 },
            "draws" => new RetailRandomLimits { MaxRawDraws = -1 }, _ => new RetailRandomLimits { MaxTraceBytes = -1 } };
        var input = Raw with { Limits = limits, Operations = [RetailRandomFixtures.Operation(0, RetailRandomOperationKind.SignedInclusive, 1, 1)] };
        Assert.Equal(RetailRandomStatus.InvalidRequest, RetailRandomOwnerPlan.Calculate(input).Status);
    }

    [Theory]
    [InlineData("operation-capacity")] [InlineData("duplicate")] [InlineData("late-generation")]
    public void LateFailureKeepsExactCalculatedPrefixWithoutPublishingACompleteState(string fault)
    {
        var second = RetailRandomFixtures.Operation(1, RetailRandomOperationKind.Raw64);
        if (fault == "duplicate") second = second with { RequestIdentity = "request-0" };
        if (fault == "late-generation") second = second with { ExpectedGeneration = 0 };
        var input = Raw with { Operations = [Raw.Operations[0], second],
            Limits = new() { MaxOperations = fault == "operation-capacity" ? 1 : 2 } };
        var expected = Conditional.Value.GetProperty("cases")[0].GetProperty("final_state");
        var result = RetailRandomOwnerPlan.Calculate(input);
        Assert.Equal(fault == "operation-capacity" ? RetailRandomStatus.BudgetExceeded : fault == "duplicate" ? RetailRandomStatus.InvalidRequest : RetailRandomStatus.StaleGeneration, result.Status);
        Assert.Single(result.CompletedPrefix); Assert.Single(result.Draws); Assert.Equal(1UL, result.DiagnosticNextGeneration);
        Assert.NotNull(result.DiagnosticPrefixState); Assert.Equal(RetailRandomFixtures.Bytes(expected), result.DiagnosticPrefixState.Bytes.ToArray());
        Assert.Null(result.CompleteFinalState); Assert.Null(result.CompleteNextGeneration);
        Assert.Equal(Initial, input.InitialStateBytes.ToArray());
    }

    [Fact]
    public void TraceBudgetExhaustionMatchesIndependentRejectedFourWordPrefix()
    {
        var expected = Case("four-rejections-budget");
        var input = RetailRandomFixtures.Transaction(RetailRandomFixtures.Bytes(expected.GetProperty("initial_storage")),
            RetailRandomFixtures.Operation(0, RetailRandomOperationKind.SignedInclusive, 0, 2))
            with { Limits = new() { MaxRawDraws = 100, MaxTraceBytes = 160 } };
        var result = RetailRandomOwnerPlan.Calculate(input);
        Assert.Equal(RetailRandomStatus.BudgetExceeded, result.Status); Assert.Equal(4, result.Draws.Length);
        Assert.All(result.Draws, x => Assert.Equal(RetailRandomDrawDisposition.Rejected, x.Disposition));
        Assert.NotNull(result.DiagnosticPrefixState);
        Assert.Equal(RetailRandomFixtures.Bytes(expected.GetProperty("diagnostic_prefix_state")), result.DiagnosticPrefixState.Bytes.ToArray());
        Assert.Null(result.CompleteFinalState); Assert.Empty(result.CompletedPrefix);
    }

    [Fact]
    public void ConstructorPrefixIsRetainedWhenNextDrawRunsOutOfCapacity()
    {
        var expected = Case("seed5489-discard310");
        var input = RetailRandomFixtures.Transaction(RetailRandomFixtures.Bytes(expected.GetProperty("initial_storage")),
            RetailRandomFixtures.Operation(0, RetailRandomOperationKind.ApplyNormalSeedReturn, seed: 5489),
            RetailRandomFixtures.Operation(1, RetailRandomOperationKind.Raw64))
            with { Limits = new() { MaxRawDraws = 0 } };
        var result = RetailRandomOwnerPlan.Calculate(input);
        Assert.Equal(RetailRandomStatus.BudgetExceeded, result.Status); Assert.Single(result.CompletedPrefix); Assert.Empty(result.Draws);
        Assert.NotNull(result.DiagnosticPrefixState);
        Assert.Equal(RetailRandomFixtures.Bytes(expected.GetProperty("constructor_state")), result.DiagnosticPrefixState.Bytes.ToArray());
        Assert.Equal(1UL, result.DiagnosticNextGeneration); Assert.Null(result.CompleteFinalState);
    }

    [Fact]
    public void DirectBoundZeroIsNotConvertedIntoFullRangeOrZeroResult()
    {
        var input = Raw with { Operations = [RetailRandomFixtures.Operation(0, RetailRandomOperationKind.BoundedUInt32)] };
        var result = RetailRandomOwnerPlan.Calculate(input);
        Assert.Equal(RetailRandomStatus.InvalidRequest, result.Status); Assert.Empty(result.Draws); Assert.Empty(result.CompletedPrefix);
        Assert.False(result.EngineStateAdmitted);
    }

    [Fact]
    public void AdapterGenerationDoesNotWrapAndConsumeUnaccountedWords()
    {
        var input = Raw with { InitialGeneration = ulong.MaxValue,
            Ownership = RetailRandomFixtures.Ownership(generation: ulong.MaxValue),
            Operations = [Raw.Operations[0] with { ExpectedGeneration = ulong.MaxValue }] };
        var result = RetailRandomOwnerPlan.Calculate(input);
        Assert.Equal(RetailRandomStatus.InvalidRequest, result.Status); Assert.Empty(result.Draws); Assert.False(result.EngineStateAdmitted);
    }

    [Fact]
    public void EmptySequenceAndZeroDiscardCallNoNativeEngineAndRequireNoCurrentImage()
    {
        var empty = new RetailRandomTransaction(RetailRandomInputOrigin.ConstructedFixture, 5, [],
            Sources: RetailRandomFixtures.Sources, Limits: new() { MaxOperations = 0, MaxRawDraws = 0, MaxTraceBytes = 0 });
        var first = RetailRandomOwnerPlan.Calculate(empty);
        Assert.True(first.Completed); Assert.Equal(5UL, first.CompleteNextGeneration); Assert.Empty(first.Draws); Assert.False(first.EngineStateAdmitted);
        var zero = empty with { Operations = [RetailRandomFixtures.Operation(0, RetailRandomOperationKind.DiscardRaw, initialGeneration: 5)], Limits = new() { MaxRawDraws = 0 } };
        var second = RetailRandomOwnerPlan.Calculate(zero);
        Assert.True(second.Completed); Assert.Equal(6UL, second.CompleteNextGeneration); Assert.Empty(second.Draws); Assert.False(second.EngineStateAdmitted);
    }

    [Fact]
    public void CalculatedResultSnapshotsAndArraysStayIndependentOfCallerCopies()
    {
        var input = Raw;
        var result = RetailRandomOwnerPlan.Calculate(input);
        Assert.True(result.Completed); Assert.NotNull(result.CompleteFinalState);
        string before = result.CompleteFinalState.Sha256;
        byte[] copied = result.CompleteFinalState.Bytes.ToArray(); Array.Fill(copied, (byte)0);
        var traceCopy = result.Draws.ToArray(); traceCopy[0] = default;
        Assert.Equal(before, result.CompleteFinalState.Sha256); Assert.NotEqual(default, result.Draws[0]);
        Assert.Equal(Initial, input.InitialStateBytes.ToArray());
        RetailRandomFixtures.NoExecutionClaims(result);
    }

    [Fact]
    public void RawEntryIgnoresUnconsumedEndpointBoundDiscardAndSeedFields()
    {
        var operation = Raw.Operations[0] with { First = int.MaxValue, Second = int.MinValue,
            Bound = 0, DiscardCount = ulong.MaxValue,
            SeedReturn = RetailRandomFixtures.Seed(7, 0, 0) with { Kind = RetailRandomSeedReturnKind.Unknown } };
        var result = RetailRandomOwnerPlan.Calculate(Raw with { Operations = [operation] });
        Assert.True(result.Completed); Assert.Single(result.Draws); Assert.NotNull(result.CompleteFinalState);
        var expected = Conditional.Value.GetProperty("cases")[0];
        Assert.Equal(RetailRandomFixtures.Bytes(expected.GetProperty("final_state")), result.CompleteFinalState.Bytes.ToArray());
        Assert.Equal<ulong?>(RetailRandomFixtures.Hex(expected.GetProperty("trace")[0].GetProperty("raw_u64").GetString()!), result.CompletedPrefix[0].RawValue);
    }
}
