using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.Commons.Utils.RetailRandom;
using Xunit;

namespace Aion.GameServer.Tests;

public sealed class RetailRandomEngineTests
{
    public static IEnumerable<object[]> BoundaryVectors()
    {
        using var corpus = RetailRandomFixtures.Corpus();
        var cases = RetailRandomFixtures.Data(corpus.RootElement.GetProperty("conditional")).GetProperty("cases");
        int count = 0;
        foreach (var item in cases.EnumerateArray())
            if (item.GetProperty("id").GetString()!.StartsWith("index", StringComparison.Ordinal) &&
                item.GetProperty("id").GetString()!.EndsWith("-range0_1", StringComparison.Ordinal))
            { count++; yield return [item.GetProperty("id").GetString()!, item.GetRawText()]; }
        if (count != 28) throw new InvalidDataException("Expected seven indexes by four masks");
    }
    public static IEnumerable<object[]> SeedVectors()
    {
        using var corpus = RetailRandomFixtures.Corpus();
        var cases = RetailRandomFixtures.Data(corpus.RootElement.GetProperty("conditional")).GetProperty("cases");
        int count = 0;
        foreach (var item in cases.EnumerateArray())
            if (item.GetProperty("id").GetString()!.StartsWith("seed", StringComparison.Ordinal))
            { count++; yield return [item.GetProperty("id").GetString()!, item.GetRawText()]; }
        if (count != 5) throw new InvalidDataException("Expected five original uint32 seeds");
    }
    public static IEnumerable<object[]> LongStreams()
    {
        using var corpus = RetailRandomFixtures.Corpus();
        var streams = RetailRandomFixtures.Data(corpus.RootElement.GetProperty("long_stream")).GetProperty("streams");
        foreach (var item in streams.EnumerateArray()) yield return [item.GetProperty("seed_u32").GetUInt32(), item.GetRawText()];
    }

    [Theory]
    [MemberData(nameof(BoundaryVectors))]
    public void ExactRawBytesIndexesMasksAndBothRolloverBranchesMatchIndependentModels(string id, string json)
    {
        using var document = JsonDocument.Parse(json);
        var expected = document.RootElement;
        var original = RetailRandomFixtures.State(expected.GetProperty("call_state"));
        var result = RetailRandomEngine.Draw(original);
        var trace = expected.GetProperty("trace")[0];
        Assert.Equal(RetailRandomFixtures.Bytes(expected.GetProperty("final_state")), result.State.Bytes.ToArray());
        Assert.Equal(RetailRandomFixtures.Hex(trace.GetProperty("raw_u64").GetString()!), result.Step.Value);
        Assert.Equal(trace.GetProperty("index_before").GetUInt32(), result.Step.IndexBefore);
        Assert.Equal(trace.GetProperty("selected_index").GetUInt32(), result.Step.SelectedIndex);
        Assert.Equal(trace.GetProperty("index_after").GetUInt32(), result.Step.IndexAfter);
        Assert.Equal(trace.GetProperty("stage").GetString(), result.Step.Regeneration.ToString());
        Assert.Equal(RetailRandomFixtures.Bytes(expected.GetProperty("call_state")), original.Bytes.ToArray());
        Assert.Equal(original.PaddingWord, result.State.PaddingWord);
        Assert.False(string.IsNullOrEmpty(id));
    }

    [Theory]
    [MemberData(nameof(SeedVectors))]
    public void ConstructorWritesExactFirst312MaskAndIndexWhilePreservingPaddingAndSecond312(string id, string json)
    {
        using var document = JsonDocument.Parse(json);
        var expected = document.RootElement;
        var original = RetailRandomFixtures.State(expected.GetProperty("initial_storage"));
        uint seed = expected.GetProperty("seed_after_normal_return_u32").GetUInt32();
        var result = RetailRandomEngine.ApplyNormalSeedReturn(original, seed);
        Assert.Equal(RetailRandomFixtures.Bytes(expected.GetProperty("constructor_state")), result.Bytes.ToArray());
        Assert.Equal((ulong)seed, result.Word(0)); Assert.Equal(312u, result.Index);
        Assert.Equal(0x5555555555555555UL, result.TemperingMask);
        Assert.Equal(original.Bytes.AsSpan(4, 4).ToArray(), result.Bytes.AsSpan(4, 4).ToArray());
        Assert.Equal(original.Bytes.AsSpan(2504, 2496).ToArray(), result.Bytes.AsSpan(2504, 2496).ToArray());
        Assert.Equal(RetailRandomFixtures.Bytes(expected.GetProperty("initial_storage")), original.Bytes.ToArray());
        Assert.False(string.IsNullOrEmpty(id));
    }

    [Theory]
    [MemberData(nameof(LongStreams))]
    public void All4096WordsAndBoundaryStatesMatchIndependentLongStreamDigest(uint seed, string json)
    {
        using var document = JsonDocument.Parse(json);
        var expected = document.RootElement;
        var state = RetailRandomEngine.ApplyNormalSeedReturn(RetailRandomFixtures.State(expected.GetProperty("initial_storage")), seed);
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var checkpoints = expected.GetProperty("samples").EnumerateArray().ToDictionary(x => x.GetProperty("draw_zero_based").GetInt32());
        byte[] word = new byte[8];
        for (int i = 0; i < expected.GetProperty("draws").GetInt32(); i++)
        {
            var result = RetailRandomEngine.Draw(state);
            state = result.State;
            BinaryPrimitives.WriteUInt64LittleEndian(word, result.Step.Value);
            digest.AppendData(word);
            if (checkpoints.TryGetValue(i, out var point))
            {
                Assert.Equal(RetailRandomFixtures.Hex(point.GetProperty("raw_u64").GetString()!), result.Step.Value);
                Assert.Equal(point.GetProperty("state_sha256").GetString(), state.Sha256);
                Assert.Equal(point.GetProperty("stage").GetString(), result.Step.Regeneration.ToString());
                Assert.Equal(point.GetProperty("selected_index").GetUInt32(), result.Step.SelectedIndex);
            }
        }
        Assert.Equal(expected.GetProperty("raw_words_le_sha256").GetString(), Convert.ToHexStringLower(digest.GetHashAndReset()));
        Assert.Equal(RetailRandomFixtures.Bytes(expected.GetProperty("final_state")), state.Bytes.ToArray());
    }

    [Theory]
    [MemberData(nameof(LongStreams))]
    public void SeedPreservesOldTailUntilFirstDrawThenReplacesItWithoutReadingOldTailPoison(uint seed, string json)
    {
        using var document = JsonDocument.Parse(json);
        var expected = document.RootElement;
        byte[] raw = RetailRandomFixtures.Bytes(expected.GetProperty("initial_storage"));
        byte[] poisoned = raw.ToArray();
        for (int i = 2504; i < 5000; i++) poisoned[i] ^= 255;
        poisoned[4] ^= 255;
        var first = RetailRandomEngine.ApplyNormalSeedReturn(RetailRandomFixtures.State(raw), seed);
        var second = RetailRandomEngine.ApplyNormalSeedReturn(RetailRandomFixtures.State(poisoned), seed);
        Assert.Equal(raw.AsSpan(2504, 2496).ToArray(), first.Bytes.AsSpan(2504, 2496).ToArray());
        Assert.Equal(poisoned.AsSpan(2504, 2496).ToArray(), second.Bytes.AsSpan(2504, 2496).ToArray());
        var a = RetailRandomEngine.Draw(first); var b = RetailRandomEngine.Draw(second);
        ulong independentlyExpected = RetailRandomFixtures.Hex(expected.GetProperty("samples")[0].GetProperty("raw_u64").GetString()!);
        Assert.Equal(independentlyExpected, a.Step.Value); Assert.Equal(independentlyExpected, b.Step.Value);
        Assert.Equal(a.State.Bytes.AsSpan(2504, 2496).ToArray(), b.State.Bytes.AsSpan(2504, 2496).ToArray());
        Assert.Equal(raw[4], a.State.Bytes[4]); Assert.Equal(poisoned[4], b.State.Bytes[4]);
        Assert.NotEqual(a.State.Bytes[4], b.State.Bytes[4]);
    }
}
