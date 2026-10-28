using System.Collections.Immutable;
using System.Text.Json;
using Aion.GameServer.Commons.Utils.RetailRandom;
using Xunit;

namespace Aion.GameServer.Tests;

public sealed class RetailRandomDistributionTests
{
    public static IEnumerable<object[]> IndependentCases()
    {
        using var corpus = RetailRandomFixtures.Corpus();
        var cases = RetailRandomFixtures.Data(corpus.RootElement.GetProperty("conditional")).GetProperty("cases");
        int count = 0;
        foreach (var item in cases.EnumerateArray())
        { count++; yield return [item.GetProperty("id").GetString()!, item.GetRawText()]; }
        if (count != 369) throw new InvalidDataException("All369 frozen conditional cases must be exercised");
    }

    [Theory]
    [MemberData(nameof(IndependentCases))]
    public void EveryIndependentSignedRangeTailRejectionBypassAndBudgetCaseMatches(string id, string json)
    {
        using var document = JsonDocument.Parse(json);
        var expected = document.RootElement;
        string classification = expected.GetProperty("classification").GetString()!;
        if (classification == "UNREACHED_ENGINE_BRANCH")
        {
            bool directOne = expected.TryGetProperty("bound_u32", out var bound);
            var operation = directOne
                ? RetailRandomFixtures.Operation(0, RetailRandomOperationKind.BoundedUInt32, bound: bound.GetUInt32())
                : RetailRandomFixtures.Operation(0, RetailRandomOperationKind.SignedInclusive,
                    expected.GetProperty("first_i32").GetInt32(), expected.GetProperty("second_i32").GetInt32());
            var input = new RetailRandomTransaction(RetailRandomInputOrigin.ConstructedFixture, 0, [operation],
                Sources: RetailRandomFixtures.Sources, Limits: new() { MaxRawDraws = 0, MaxTraceBytes = 0 });
            var result = RetailRandomOwnerPlan.Calculate(input);
            Assert.True(result.Completed, $"{id}: {result.Detail}"); Assert.Empty(result.Draws);
            Assert.False(result.EngineStateAdmitted); Assert.False(result.Validation.StateBytesHashVerified);
            Assert.True(result.Validation.SourceDeclarationsMatchPins); Assert.False(result.Validation.OwnershipDeclarationConsistent);
            Assert.Null(result.CompleteFinalState); Assert.Null(result.DiagnosticPrefixState);
            if (directOne) Assert.Equal<uint?>(0, result.CompletedPrefix[0].BoundedValue);
            else Assert.Equal<int?>(expected.GetProperty("result_i32").GetInt32(), result.CompletedPrefix[0].SignedValue);
            RetailRandomFixtures.NoExecutionClaims(result);
            return;
        }

        byte[] initial = RetailRandomFixtures.Bytes(expected.GetProperty("initial_storage"));
        if (classification == "ANALYTIC_BUDGET_DIAGNOSTIC_NOT_NATIVE_RETURN")
        {
            int budget = expected.GetProperty("budget").GetInt32();
            var input = RetailRandomFixtures.Transaction(initial, RetailRandomFixtures.Operation(0,
                RetailRandomOperationKind.SignedInclusive, expected.GetProperty("first_i32").GetInt32(), expected.GetProperty("second_i32").GetInt32()))
                with { Limits = new() { MaxRawDraws = budget } };
            var result = RetailRandomOwnerPlan.Calculate(input);
            Assert.Equal(RetailRandomStatus.BudgetExceeded, result.Status);
            Assert.Empty(result.CompletedPrefix); Assert.Null(result.CompleteFinalState); Assert.Null(result.CompleteNextGeneration);
            Assert.NotNull(result.DiagnosticPrefixState);
            Assert.Equal(RetailRandomFixtures.Bytes(expected.GetProperty("diagnostic_prefix_state")), result.DiagnosticPrefixState.Bytes.ToArray());
            Assert.Equal(budget, result.Draws.Length);
            for (int i = 0; i < budget; i++) RetailRandomFixtures.Trace(expected.GetProperty("trace")[i], result.Draws[i], 0, false);
            Assert.Equal(0UL, result.DiagnosticNextGeneration);
            RetailRandomFixtures.NoExecutionClaims(result);
            return;
        }

        var operations = new List<RetailRandomOperation>();
        var seedValue = expected.GetProperty("seed_after_normal_return_u32");
        if (seedValue.ValueKind != JsonValueKind.Null)
            operations.Add(RetailRandomFixtures.Operation(operations.Count, RetailRandomOperationKind.ApplyNormalSeedReturn, seed: seedValue.GetUInt32()));
        int discard = expected.GetProperty("discard_raw_count").GetInt32();
        if (discard != 0) operations.Add(RetailRandomFixtures.Operation(operations.Count, RetailRandomOperationKind.DiscardRaw, discard: (ulong)discard));
        int signedOrdinal = operations.Count;
        operations.Add(RetailRandomFixtures.Operation(signedOrdinal, RetailRandomOperationKind.SignedInclusive,
            expected.GetProperty("first_i32").GetInt32(), expected.GetProperty("second_i32").GetInt32()));
        var transaction = RetailRandomFixtures.Transaction(initial, operations.ToArray());
        var calculated = RetailRandomOwnerPlan.Calculate(transaction);
        Assert.True(calculated.Completed, $"{id}: {calculated.ReachedStage}: {calculated.Detail}");
        Assert.Equal<int?>(expected.GetProperty("result_i32").GetInt32(), calculated.CompletedPrefix[^1].SignedValue);
        Assert.Equal((ulong)operations.Count, calculated.CompleteNextGeneration);
        Assert.Equal((ulong)expected.GetProperty("raw_draws_consumed").GetInt32(), calculated.CompletedPrefix[^1].RawDrawsConsumed);
        var actualTrace = calculated.Draws.Where(x => x.OperationOrdinal == signedOrdinal).ToArray();
        var expectedTrace = expected.GetProperty("trace");
        Assert.Equal(expectedTrace.GetArrayLength(), actualTrace.Length);
        for (int i = 0; i < actualTrace.Length; i++)
            RetailRandomFixtures.Trace(expectedTrace[i], actualTrace[i], signedOrdinal, expectedTrace[i].GetProperty("accepted").GetBoolean());
        for (int i = 0; i < calculated.Draws.Length; i++) Assert.Equal((ulong)i, calculated.Draws[i].DrawOrdinal);
        if (calculated.EngineStateAdmitted)
        {
            Assert.NotNull(calculated.CompleteFinalState);
            Assert.Equal(RetailRandomFixtures.Bytes(expected.GetProperty("final_state")), calculated.CompleteFinalState.Bytes.ToArray());
            Assert.True(calculated.Validation.StateBytesHashVerified);
            if (seedValue.ValueKind != JsonValueKind.Null)
                Assert.Equal(expected.GetProperty("constructor_state").GetProperty("sha256").GetString(), calculated.CompletedPrefix[0].StateAfterSha256);
            if (discard != 0) Assert.Equal(expected.GetProperty("call_state").GetProperty("sha256").GetString(), calculated.CompletedPrefix[signedOrdinal - 1].StateAfterSha256);
        }
        else
        {
            // An equal wrapper leaves the optional input opaque; do not upgrade its byte identity.
            Assert.Null(calculated.CompleteFinalState); Assert.False(calculated.Validation.StateBytesHashVerified);
            Assert.Equal(initial, RetailRandomFixtures.Bytes(expected.GetProperty("final_state")));
        }
        Assert.Equal(initial, transaction.InitialStateBytes.ToArray());
        Assert.Null(calculated.DiagnosticPrefixState);
        RetailRandomFixtures.NoExecutionClaims(calculated);
    }
}
