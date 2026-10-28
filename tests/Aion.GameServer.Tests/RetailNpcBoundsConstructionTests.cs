using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aion.GameServer.Dataholders.RetailNpc;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcBoundsConstructionTests
{
    [Fact]
    public void AbsentGroupKeepsIndependentNativeConstructorRadiusAndIgnoresScale()
    {
        var source = Source(null, null, null, 180, false);
        var plan = Plan(source);
        Assert.Null(plan.OriginalParserReturn); Assert.Equal(new(.5f, .5f, 2f), plan.FinalAxes);
        Assert.Equal(.5f, plan.HorizontalRadius); Assert.Equal(1f, plan.TemplateScale);
        Assert.False(plan.ScaleApplied); Assert.True(plan.CanSupplySourceDerivedBounds);
    }

    [Theory]
    [InlineData("front", .5f, .5f, 2f, .5f, false)]
    [InlineData("side", 4f, .5f, 2f, .5f, false)]
    [InlineData("upper", 8f, 4f, 4f, 4f, true)]
    public void ParserPartialWritesAndUpperReturnQuirkStayVisible(string missing, float front, float side,
        float upper, float radius, bool returned)
    {
        var source = Source(missing == "front" ? null : "4", missing == "side" ? null : "2",
            missing == "upper" ? null : "8", 200);
        var plan = Plan(source);
        Assert.Equal(returned, plan.OriginalParserReturn); Assert.True(plan.HasParserDiagnostics);
        Assert.False(plan.CanSupplySourceDerivedBounds); Assert.Equal(new(front, side, upper), plan.FinalAxes);
        Assert.Equal(radius, plan.HorizontalRadius); Assert.Equal(returned, plan.ScaleApplied);
    }

    [Fact]
    public void ExplicitZeroIsScaledAndCopiedWithoutBeingRefilled()
    {
        var source = Source("0", "0", "0", 0);
        var plan = Plan(source);
        Assert.Equal(RetailNpcSourcePresence.ExplicitZero, source.Bounds.Axes["front"].Presence);
        Assert.Equal(new(0, 0, 0), plan.FinalAxes); Assert.Equal(0f, plan.HorizontalRadius);
        Assert.Equal(0f, plan.TemplateScale); Assert.True(plan.ScaleApplied); Assert.True(plan.OriginalParserReturn);
        Assert.All(plan.CommonTailCopies, x => Assert.Equal(0ul, x.Bits));
    }

    [Fact]
    public void EqualitySelectsSideAndRetainsSignedZero()
    {
        var plan = Plan(Source("0", "-0.0", "2", 100));
        Assert.Equal(0x80000000u, BitConverter.SingleToUInt32Bits(plan.FinalAxes.Side));
        Assert.Equal(0x80000000u, BitConverter.SingleToUInt32Bits(plan.HorizontalRadius));
        Assert.Equal(0x8000000000000000ul, plan.CommonTailCopies[0].Bits);
        Assert.False(plan.ScaleApplied);
    }

    [Theory]
    [InlineData("constructor-null")] [InlineData("constructor-false")]
    [InlineData("rounding-null")] [InlineData("rounding-false")]
    [InlineData("evidence")] [InlineData("source")] [InlineData("binary")]
    public void UnknownConstructorRoundingOrSourceDoesNotPublishAPlan(string fault)
    {
        var source = Source("2", "4", "8", 100);
        var context = Context(source);
        context = fault switch
        {
            "constructor-null" => context with { FreshObjectClassConstructor = null },
            "constructor-false" => context with { FreshObjectClassConstructor = false },
            "rounding-null" => context with { NearestEvenNormalArithmetic = null },
            "rounding-false" => context with { NearestEvenNormalArithmetic = false },
            "evidence" => context with { Evidence = "" },
            "source" => context with { ExpectedSource = source.Receipt with { NpcId = 2 } },
            _ => context with { ExpectedSource = source.Receipt with { NpcSha256 = new('0', 64) } }
        };
        var result = RetailNpcBoundsConstruction.Construct(source, context);
        Assert.False(result.Success); Assert.Null(result.Plan); Assert.Single(result.Issues);
    }

    [Fact]
    public void SubnormalArithmeticStopsWithoutPublishingButUnconsumedAxesAreNotRead()
    {
        var consumed = Source("1e-44", "1", "2", 100);
        Assert.False(RetailNpcBoundsConstruction.Construct(consumed, Context(consumed)).Success);
        var unconsumed = Source(null, "1e-44", "1e-44", 100);
        Assert.True(RetailNpcBoundsConstruction.Construct(unconsumed, Context(unconsumed)).Success);
        var underflow = Source("1.17549435e-38", "1.17549435e-38", "2", 1);
        Assert.False(RetailNpcBoundsConstruction.Construct(underflow, Context(underflow)).Success);
    }

    [Fact]
    public void IndependentNativeCopyOrderIsQwordThenUpperThenRadiusAndDoesNotClaimCurrentBody()
    {
        var plan = Plan(Source("2", "4", "8", null));
        Assert.Equal(new[] { 0xc0, 0xc8, 0xbc }, plan.CommonTailCopies.Select(x => x.ActorOffset));
        Assert.Equal(new[] { 8, 4, 4 }, plan.CommonTailCopies.Select(x => x.Width));
        Assert.Equal(new[] { 1, 2, 3 }, plan.CommonTailCopies.Select(x => x.Order));
        Assert.Equal(0x4080000040000000ul, plan.CommonTailCopies[0].Bits);
        Assert.False(plan.CurrentActorBoundsObserved); Assert.False(plan.ActorClearanceVerified);
        Assert.False(plan.RuntimeVerified); Assert.False(plan.ActivationReady);
    }

    [Theory]
    [InlineData("3.4028234663852886e38", 200)]
    [InlineData("1.17549435e-38", 100)]
    public void AxisOverflowAndSeparateRadiusUnderflowWithholdTheWholePlan(string horizontal, int scale)
    {
        var source = Source(horizontal, horizontal, "2", scale);
        var result = RetailNpcBoundsConstruction.Construct(source, Context(source));
        Assert.False(result.Success); Assert.Null(result.Plan);
        Assert.Equal("numeric_domain", Assert.Single(result.Issues).Code);
    }

    public static IEnumerable<object[]> IndependentVectors()
    {
        using var document = JsonDocument.Parse(Vectors);
        foreach (var vector in document.RootElement.EnumerateArray()) yield return [vector.Clone()];
    }

    [Theory]
    [MemberData(nameof(IndependentVectors))]
    public void AllPilotDimensionsAndIndependentIntegerRoundingVectorsMatchExactRationalOracles(JsonElement vector)
    {
        var axes = vector.GetProperty("lexemes");
        int? scale = vector.GetProperty("scale").ValueKind == JsonValueKind.Null ? null : vector.GetProperty("scale").GetInt32();
        // Minimal, explicitly synthetic source receipts; dimensions/oracles are independently
        // derived from the pinned source. This does not forge a complete pilot source image.
        var plan = Plan(Source(axes[0].GetString(), axes[1].GetString(), axes[2].GetString(), scale));
        Assert.Equal(vector.GetProperty("axis_bits").EnumerateArray().Select(x => x.GetString()),
            new[] { plan.FinalAxes.Front, plan.FinalAxes.Side, plan.FinalAxes.Upper }.Select(x => BitConverter.SingleToUInt32Bits(x).ToString("x8", CultureInfo.InvariantCulture)));
        Assert.Equal(vector.GetProperty("radius_bits").GetString(), BitConverter.SingleToUInt32Bits(plan.HorizontalRadius).ToString("x8", CultureInfo.InvariantCulture));
        Assert.Equal(vector.GetProperty("scale_ratio_bits").GetString(), BitConverter.SingleToUInt32Bits(plan.TemplateScale).ToString("x8", CultureInfo.InvariantCulture));
        Assert.False(plan.HasParserDiagnostics);
    }

    private static RetailNpcBoundsContext Context(RetailNpcTemplateInputs source) => new(source.Receipt, true, true, "conditional source construction; no actor or native invocation");
    private static RetailNpcBoundsPlan Plan(RetailNpcTemplateInputs source)
    {
        var result = RetailNpcBoundsConstruction.Construct(source, Context(source));
        Assert.True(result.Success, string.Join(';', result.Issues.Select(x => x.Detail)));
        return result.Plan!;
    }
    private static JsonArray Node(string tag, string? value, params JsonNode[] children)
    {
        var content = new JsonArray(); if (value is not null) content.Add(value);
        foreach (var child in children) content.Add(child);
        return new(JsonValue.Create(tag), new JsonArray(), content);
    }
    private static RetailNpcTemplateInputs Source(string? front, string? side, string? upper, int? scale, bool group = true)
    {
        const string path = @"Z:\synthetic-do-not-open\npcs_monsters.xml";
        string datasetId = new('d', 64), manifestHash = new('e', 64);
        var root = Node("npc", null, Node("id", "1"), Node("name", "BoundsFixture"));
        if (group)
        {
            var fields = new List<JsonNode>();
            if (front is not null) fields.Add(Node("front", front));
            if (side is not null) fields.Add(Node("side", side));
            if (upper is not null) fields.Add(Node("upper", upper));
            root[2]!.AsArray().Add(Node("bound_radius", null, fields.ToArray()));
        }
        if (scale.HasValue) root[2]!.AsArray().Add(Node("scale", scale.Value.ToString(CultureInfo.InvariantCulture)));
        var profile = new JsonObject { ["schema_version"] = 1, ["dataset_id"] = datasetId, ["npc_id"] = 1, ["devname"] = "BoundsFixture",
            ["scalar_fields"] = new JsonObject(), ["raw_record"] = root, ["skill_bindings"] = new JsonArray(),
            ["source"] = new JsonObject { ["path"] = path, ["byte_start"] = 0, ["byte_end_exclusive"] = 64, ["line"] = 1, ["record_index"] = 0,
                ["raw_sha256"] = new string('a', 64), ["semantic_sha256"] = new string('b', 64) },
            ["ordered_lists"] = new JsonObject(), ["binding48"] = new JsonObject(), ["aggregate_crosscheck"] = new JsonObject(), ["blockers"] = new JsonArray("synthetic_source_only") };
        using var sourceDoc = JsonDocument.Parse(profile.ToJsonString());
        using var provenance = JsonDocument.Parse(new JsonObject { ["consumer_registry"] = new JsonObject { ["builds"] = new JsonObject {
            ["main"] = new JsonObject { ["sha256"] = RetailNpcTemplateProjection.MainBinarySha256 }, ["npc"] = new JsonObject { ["sha256"] = RetailNpcTemplateProjection.NpcBinarySha256 } } },
            ["selection"] = new JsonArray(new JsonObject { ["filename"] = "npcs_monsters.xml", ["selected"] = path, ["region"] = "USA", ["reason"] = "explicit_synthetic_fixture" }) }.ToJsonString());
        using var sources = JsonDocument.Parse(new JsonArray(new JsonObject { ["path"] = path, ["bytes"] = 1000, ["sha256"] = new string('c', 64),
            ["record_count"] = 1, ["record_tag"] = "npc", ["strict_xml_complete"] = true }).ToJsonString());
        var dataset = new RetailNpcStaticDataset(datasetId, manifestHash, new Dictionary<int, RetailNpcSourceProfile> { [1] = new(sourceDoc.RootElement) },
            1, 0, 0, ImmutableArray<RetailNpcArtifactReceipt>.Empty, provenance.RootElement, sources.RootElement);
        var result = RetailNpcTemplateProjection.Project(dataset, 1, new(datasetId, manifestHash, "USA"));
        Assert.True(result.Success, string.Join(';', result.Issues.Select(x => x.Message)));
        return result.Inputs!;
    }

    // Independently generated Fraction/RNE vectors; no C# implementation was used.
    private const string Vectors = """
[{"lexemes":["0.600000","1.000000","1.830000"],"scale":100,"scale_ratio_bits":"3f800000","axis_bits":["3f19999a","3f800000","3fea3d71"],"radius_bits":"3f000000"},{"lexemes":["1.000000","1.000000","3.900000"],"scale":100,"scale_ratio_bits":"3f800000","axis_bits":["3f800000","3f800000","4079999a"],"radius_bits":"3f000000"},{"lexemes":["1.000000","1.000000","3.900000"],"scale":100,"scale_ratio_bits":"3f800000","axis_bits":["3f800000","3f800000","4079999a"],"radius_bits":"3f000000"},{"lexemes":["1.200000","1.000000","3.600000"],"scale":100,"scale_ratio_bits":"3f800000","axis_bits":["3f99999a","3f800000","40666666"],"radius_bits":"3f19999a"},{"lexemes":["0.700000","0.700000","1.000000"],"scale":100,"scale_ratio_bits":"3f800000","axis_bits":["3f333333","3f333333","3f800000"],"radius_bits":"3eb33333"},{"lexemes":["1.000000","0.900000","1.600000"],"scale":100,"scale_ratio_bits":"3f800000","axis_bits":["3f800000","3f666666","3fcccccd"],"radius_bits":"3f000000"},{"lexemes":["1.000000","0.900000","1.600000"],"scale":100,"scale_ratio_bits":"3f800000","axis_bits":["3f800000","3f666666","3fcccccd"],"radius_bits":"3f000000"},{"lexemes":["1.800000","1.500000","2.550000"],"scale":100,"scale_ratio_bits":"3f800000","axis_bits":["3fe66666","3fc00000","40233333"],"radius_bits":"3f666666"},{"lexemes":["0.700000","1.200000","1.170000"],"scale":100,"scale_ratio_bits":"3f800000","axis_bits":["3f333333","3f99999a","3f95c28f"],"radius_bits":"3f19999a"},{"lexemes":["0.800000","1.800000","1.730000"],"scale":100,"scale_ratio_bits":"3f800000","axis_bits":["3f4ccccd","3fe66666","3fdd70a4"],"radius_bits":"3f666666"},{"lexemes":["0.800000","1.800000","1.730000"],"scale":100,"scale_ratio_bits":"3f800000","axis_bits":["3f4ccccd","3fe66666","3fdd70a4"],"radius_bits":"3f666666"},{"lexemes":["1.200000","2.500000","2.130000"],"scale":100,"scale_ratio_bits":"3f800000","axis_bits":["3f99999a","40200000","400851ec"],"radius_bits":"3fa00000"},{"lexemes":["1.000000","1.000000","1.300000"],"scale":100,"scale_ratio_bits":"3f800000","axis_bits":["3f800000","3f800000","3fa66666"],"radius_bits":"3f000000"},{"lexemes":["0.800000","0.800000","1.360000"],"scale":140,"scale_ratio_bits":"3fb33333","axis_bits":["3f8f5c29","3f8f5c29","3ff3b646"],"radius_bits":"3f0f5c29"},{"lexemes":["0.800000","0.800000","1.360000"],"scale":140,"scale_ratio_bits":"3fb33333","axis_bits":["3f8f5c29","3f8f5c29","3ff3b646"],"radius_bits":"3f0f5c29"},{"lexemes":["2.450000","1.950000","2.855000"],"scale":100,"scale_ratio_bits":"3f800000","axis_bits":["401ccccd","3ff9999a","4036b852"],"radius_bits":"3f9ccccd"},{"lexemes":["1.000000","0.800000","1.590000"],"scale":180,"scale_ratio_bits":"3fe66666","axis_bits":["3fe66666","3fb851eb","40372b02"],"radius_bits":"3f666666"},{"lexemes":["1.000000","0.800000","1.590000"],"scale":180,"scale_ratio_bits":"3fe66666","axis_bits":["3fe66666","3fb851eb","40372b02"],"radius_bits":"3f666666"},{"lexemes":["3.080000","2.610000","3.000000"],"scale":100,"scale_ratio_bits":"3f800000","axis_bits":["40451eb8","40270a3d","40400000"],"radius_bits":"3fc51eb8"},{"lexemes":["1.500000","1.000000","3.050000"],"scale":180,"scale_ratio_bits":"3fe66666","axis_bits":["402ccccc","3fe66666","40afae14"],"radius_bits":"3faccccc"},{"lexemes":["2.450000","1.950000","2.855000"],"scale":130,"scale_ratio_bits":"3fa66666","axis_bits":["404bd70a","40223d71","406d8937"],"radius_bits":"3fcbd70a"},{"lexemes":["3.080000","2.610000","3.000000"],"scale":130,"scale_ratio_bits":"3fa66666","axis_bits":["408020c4","405926e8","40799999"],"radius_bits":"400020c4"},{"lexemes":["1.200000","1.000000","3.600000"],"scale":130,"scale_ratio_bits":"3fa66666","axis_bits":["3fc7ae15","3fa66666","4095c28f"],"radius_bits":"3f47ae15"},{"lexemes":["1.800000","1.500000","2.550000"],"scale":130,"scale_ratio_bits":"3fa66666","axis_bits":["4015c28f","3ff99999","405428f5"],"radius_bits":"3f95c28f"},{"lexemes":["1.500000","1.000000","3.050000"],"scale":180,"scale_ratio_bits":"3fe66666","axis_bits":["402ccccc","3fe66666","40afae14"],"radius_bits":"3faccccc"},{"lexemes":["0.6","1","1.83"],"scale":100,"scale_ratio_bits":"3f800000","axis_bits":["3f19999a","3f800000","3fea3d71"],"radius_bits":"3f000000"},{"lexemes":["0.6","1","1.83"],"scale":130,"scale_ratio_bits":"3fa66666","axis_bits":["3f47ae15","3fa66666","40184189"],"radius_bits":"3f266666"},{"lexemes":["0.6","1","1.83"],"scale":180,"scale_ratio_bits":"3fe66666","axis_bits":["3f8a3d71","3fe66666","4052d0e5"],"radius_bits":"3f666666"},{"lexemes":["2","4","8"],"scale":null,"scale_ratio_bits":"3f800000","axis_bits":["40000000","40800000","41000000"],"radius_bits":"40000000"},{"lexemes":["2","4","8"],"scale":0,"scale_ratio_bits":"00000000","axis_bits":["00000000","00000000","00000000"],"radius_bits":"00000000"},{"lexemes":["2","4","8"],"scale":-100,"scale_ratio_bits":"bf800000","axis_bits":["c0000000","c0800000","c1000000"],"radius_bits":"bf800000"},{"lexemes":["0","0","0"],"scale":130,"scale_ratio_bits":"3fa66666","axis_bits":["00000000","00000000","00000000"],"radius_bits":"00000000"},{"lexemes":["4","2","8"],"scale":50,"scale_ratio_bits":"3f000000","axis_bits":["40000000","3f800000","40800000"],"radius_bits":"3f800000"},{"lexemes":["1.0000001","1.0000002","3"],"scale":16777217,"scale_ratio_bits":"4823d70a","axis_bits":["4823d70b","4823d70d","48f5c28f"],"radius_bits":"47a3d70d"},{"lexemes":["1","1","2"],"scale":2147483647,"scale_ratio_bits":"4ba3d70a","axis_bits":["4ba3d70a","4ba3d70a","4c23d70a"],"radius_bits":"4b23d70a"},{"lexemes":["1","2","3"],"scale":-2147483648,"scale_ratio_bits":"cba3d70a","axis_bits":["cba3d70a","cc23d70a","cc75c28f"],"radius_bits":"cb23d70a"}]
""";
}
