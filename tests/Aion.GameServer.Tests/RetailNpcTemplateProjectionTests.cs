using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aion.GameServer.Dataholders.RetailNpc;

namespace Aion.GameServer.Tests;

/// <summary>Inactive source contracts and fixed pilot source vectors; no native runtime oracle.</summary>
public sealed class RetailNpcTemplateProjectionTests
{
    [Fact]
    public void ZeroAbsentAndSourceWidthsNeverBecomeDefaultsOrEffectiveStats()
    {
        var fixture = new ProjectionFixture();
        fixture.Add("magical_defend", "0"); fixture.Add("parry", "0");
        fixture.Add("min_damage", "65536"); fixture.Add("max_damage", "-1");
        fixture.Add("physical_attack", "ignored and not numeric");
        var inputs = Project(fixture);
        Assert.Equal(30, inputs.Integers.Count);
        Assert.Equal(0, inputs.Integers["magical_defend"].ParsedValue);
        Assert.Equal(RetailNpcSourcePresence.ExplicitZero, inputs.Integers["parry"].Presence);
        Assert.Null(inputs.StatRatio.ParsedValue);
        Assert.Equal(RetailNpcSourcePresence.Absent, inputs.StatRatio.Presence);
        Assert.Null(inputs.Integers["max_mp"].ParsedValue);
        Assert.Null(inputs.Integers["mp_regen"].ParsedValue);
        Assert.Equal(65536, inputs.Integers["min_damage"].ParsedValue); // No low-word projection.
        Assert.Equal(-1, inputs.Integers["max_damage"].ParsedValue);
        Assert.Equal("ignored and not numeric", Assert.Single(inputs.RawPhysicalAttack.RawValues));
        Assert.All(inputs.Integers.Values, x => Assert.False(x.DefaultApplied));
        var assessment = RetailNpcInitializationRules.Assess(inputs);
        Assert.Equal(RetailNpcInitializationStage.SourceProjection, assessment.Stage);
        Assert.Contains(assessment.Obligations, x => x.Code == "U00-G02" && x.Reason.StartsWith("Absent", StringComparison.Ordinal));
        Assert.False(assessment.CanConstructOrdinarySeed);
        Assert.False(assessment.CanConstructEffectiveInputs);
        Assert.False(inputs.DefaultsResolved); Assert.False(inputs.EffectiveInputsVerified);
        Assert.False(inputs.FixtureVerified); Assert.False(inputs.ActivationReady);
        Assert.Throws<InvalidOperationException>(() => RetailNpcInitializationRules.RequireEffectiveInputs(inputs));
    }

    [Fact]
    public void ExplicitRatioZeroAndMpZeroRemainSourceValuesWithSeparateUnresolvedRules()
    {
        var fixture = new ProjectionFixture();
        fixture.Add("stat_ratio", "0"); fixture.Add("max_mp", "0");
        var inputs = Project(fixture);
        Assert.Equal(0, inputs.StatRatio.ParsedValue);
        Assert.Equal(RetailNpcSourcePresence.ExplicitZero, inputs.StatRatio.Presence);
        Assert.Equal(0, inputs.Integers["max_mp"].ParsedValue);
        var assessment = RetailNpcInitializationRules.Assess(inputs);
        Assert.Contains(assessment.Obligations, x => x.Code == "U00-G02" && x.Reason.StartsWith("Explicit", StringComparison.Ordinal));
        Assert.Contains(assessment.Obligations, x => x.Code == "U00-G05");
        Assert.Equal(12, assessment.Obligations.Length);
        Assert.Empty(RetailNpcInitializationRules.InstructionCompleteInitializationContracts);
    }

    [Fact]
    public void ListsKeepOrderUnknownNodesAndUnnamedAbnormalPlaceholders()
    {
        var fixture = new ProjectionFixture();
        fixture.AddNode(Node("attacks", null,
            Node("data", null, Node("attack_rate", "0"), Node("attack_type", "magical_Earth"), Node("opaque", "keep-a")),
            Node("data", null, Node("attack_rate", "700"), Node("attack_mod", "125"), Node("attack_range", "16.000000"), Node("attack_type", "physical"))));
        fixture.AddSkills((16724, "NPEL_SpellLong_Water"), (16595, "NPEL_SpellELSA_Earth"));
        fixture.AddNode(Node("abnormal_resists", null,
            Node("data", null, Node("abnormal_resist_name", "Bleed,Poison"), Node("abnormal_resist_value", "900")),
            Node("data", null, Node("abnormal_resist_name", "Sleep"), Node("abnormal_resist_value", "200"))));
        fixture.AddNode(Node("abnormal_penetrations", null, Node("data", null, Node("abnormal_penetration_value", "0"))));
        var inputs = Project(fixture);
        Assert.Equal(new[] { 0, 1 }, inputs.Attacks.Entries.Select(x => x.Ordinal));
        Assert.Equal(new[] { "magical_Earth", "physical" }, inputs.Attacks.Entries.Select(x => x.Type.Value));
        Assert.Equal(0, inputs.Attacks.Entries[0].Weight.ParsedValue);
        Assert.Null(inputs.Attacks.Entries[0].Modifier.ParsedValue); // No native100 default inserted.
        Assert.Equal(new[] { 16724, 16595 }, inputs.Skills.Entries.Select(x => x.Supported48Id));
        Assert.Equal(new[] { "Bleed,Poison", "Sleep" }, inputs.AbnormalResists.Entries.Select(x => x.Name.Value));
        Assert.Equal((ushort)900, inputs.AbnormalResists.Entries[0].Value.ParsedValue);
        var placeholder = Assert.Single(inputs.AbnormalPenetrations.Entries);
        Assert.Null(placeholder.Name.Value);
        Assert.Equal(RetailNpcSourcePresence.Absent, placeholder.Name.Presence);
        Assert.Equal((ushort)0, placeholder.Value.ParsedValue);
        Assert.False(placeholder.NativeSlotsResolved);
        Assert.Contains("keep-a", inputs.Attacks.Entries[0].RawNode.GetRawText(), StringComparison.Ordinal);
        Assert.All(inputs.Skills.Entries, x => Assert.False(x.EffectSemanticsVerified));
    }

    [Theory]
    [InlineData("0.800000", 0x3f4ccccdu)]
    [InlineData("-0.0", 0x80000000u)]
    [InlineData("1e-50", 0u)]
    [InlineData("3.4028234663852886e38", 0x7f7fffffu)]
    public void FloatSourceUsesDoubleThenBinary32AndKeepsItsLexeme(string lexeme, uint bits)
    {
        var fixture = new ProjectionFixture(); fixture.Add("move_speed_normal_walk", lexeme);
        var field = Project(fixture).Speeds["move_speed_normal_walk"];
        Assert.Equal(bits, BitConverter.SingleToUInt32Bits(field.ParsedValue!.Value));
        Assert.Equal(lexeme, Assert.Single(field.Lexemes));
        Assert.Equal(lexeme == "-0.0" ? RetailNpcSourcePresence.ExplicitZero : RetailNpcSourcePresence.ExplicitValue, field.Presence);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    [InlineData("-2147483649")]
    [InlineData("NaN")]
    [InlineData("0e0")]
    [InlineData("1 0")]
    [InlineData("１２")]
    public void ExplicitUnsupportedIntegerLexemesBlockInsteadOfTruncatingOrFallingBack(string lexeme)
    {
        var fixture = new ProjectionFixture(); fixture.Add("max_hp", lexeme);
        var result = fixture.Project();
        Assert.False(result.Success); Assert.Null(result.Inputs);
        Assert.Contains(result.Issues, x => x.Code == "invalid_int32_lexeme" && x.RecordPath == "/npc/max_hp");
    }

    [Theory]
    [InlineData("1e40")]
    [InlineData("-1e400")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1,25")]
    public void NonfiniteOrUnsupportedFloatLexemesBlockSourceProjection(string lexeme)
    {
        var fixture = new ProjectionFixture(); fixture.Add("move_speed_normal_run", lexeme);
        var result = fixture.Project();
        Assert.False(result.Success);
        Assert.Contains(result.Issues, x => x.Code == "invalid_finite_binary32_lexeme");
    }

    [Fact]
    public void SignedUnsignedSourceBoundariesAreCheckedAndBoundsAreNotScaled()
    {
        var fixture = new ProjectionFixture(); fixture.Add("scale", "180");
        fixture.Add("max_hp", int.MaxValue.ToString(CultureInfo.InvariantCulture));
        fixture.AddNode(Node("first_level_attr", null, Node("str", "65535"), Node("vit", "0"),
            Node("elemental_defend_water", "-32768"), Node("elemental_defend_fire", "32767")));
        fixture.AddNode(Node("bound_radius", null, Node("front", "2.0")));
        var inputs = Project(fixture);
        Assert.Equal(int.MaxValue, inputs.Integers["max_hp"].ParsedValue); // No native HP clamp.
        Assert.Equal(ushort.MaxValue, inputs.FirstLevel.Primaries["str"].ParsedValue);
        Assert.Equal(short.MinValue, inputs.FirstLevel.Elements["elemental_defend_water"].ParsedValue);
        Assert.Equal(short.MaxValue, inputs.FirstLevel.Elements["elemental_defend_fire"].ParsedValue);
        Assert.Null(inputs.FirstLevel.Primaries["agi"].ParsedValue); // No constant100 primary.
        Assert.Equal(180, inputs.VisualScale.ParsedValue);
        Assert.Equal(2f, inputs.Bounds.Axes["front"].ParsedValue);
        Assert.Null(inputs.Bounds.Axes["side"].ParsedValue); // No default0.5 axis.
        Assert.False(inputs.Bounds.VisualScaleApplied);
    }

    [Theory]
    [InlineData("str", "65536", "invalid_uint16_lexeme")]
    [InlineData("str", "-1", "invalid_uint16_lexeme")]
    [InlineData("elemental_defend_fire", "32768", "invalid_int16_lexeme")]
    [InlineData("elemental_defend_water", "-32769", "invalid_int16_lexeme")]
    public void FirstLevelWidthsRejectOverflow(string tag, string value, string code)
    {
        var fixture = new ProjectionFixture(); fixture.AddNode(Node("first_level_attr", null, Node(tag, value)));
        Assert.Contains(fixture.Project().Issues, x => x.Code == code);
    }

    [Fact]
    public void DuplicateKnownFieldsAndContainersNeverOverwriteOrMerge()
    {
        var scalar = new ProjectionFixture(); scalar.Add("parry", "0"); scalar.Add("parry", "0");
        Assert.Contains(scalar.Project().Issues, x => x.Code == "duplicate_field");
        var group = new ProjectionFixture(); group.AddNode(Node("attacks", null)); group.AddNode(Node("attacks", null));
        Assert.Contains(group.Project().Issues, x => x.Code == "duplicate_group");
    }

    [Fact]
    public void NestedKnownScalarOrUnknownOrderedEntryCannotBeSilentlyDropped()
    {
        var scalar = new ProjectionFixture(); scalar.AddNode(Node("max_hp", "10", Node("hidden", "20")));
        Assert.Contains(scalar.Project().Issues, x => x.Code == "invalid_int32_lexeme");
        var ordered = new ProjectionFixture(); ordered.AddNode(Node("attacks", null, Node("unexpected", "opaque")));
        Assert.Contains(ordered.Project().Issues, x => x.Code == "unsupported_ordered_entry");
    }

    [Fact]
    public void RequiredSkillBindingsMustRemainUniqueCompleteAndCompatible()
    {
        var missing = new ProjectionFixture(); missing.AddSkills((1, "One")); missing.Bindings.Clear();
        Assert.Contains(missing.Project().Issues, x => x.Code == "missing_skill_binding");
        var duplicate = new ProjectionFixture(); duplicate.AddSkills((1, "One")); duplicate.Bindings.Add(duplicate.Bindings[0]!.DeepClone());
        Assert.Contains(duplicate.Project().Issues, x => x.Code == "duplicate_skill_binding");
        var disagreement = new ProjectionFixture(); disagreement.AddSkills((1, "One"));
        disagreement.Bindings[0]!["supported48_stack_id_candidates"] = new JsonArray(JsonValue.Create(2));
        Assert.Contains(disagreement.Project().Issues, x => x.Code == "skill_identity");
    }

    [Fact]
    public void PinnedDatasetRegionBinarySelectedFileAndSpanAreAllRequired()
    {
        var fixture = new ProjectionFixture();
        Assert.Contains(fixture.Project(ProjectionFixture.Policy with { ExpectedDatasetId = new string('f', 64) }).Issues, x => x.Code == "dataset_identity");
        Assert.Contains(fixture.Project(ProjectionFixture.Policy with { ExpectedOutputManifestSha256 = new string('f', 64) }).Issues, x => x.Code == "dataset_identity");
        Assert.Contains(fixture.Project(ProjectionFixture.Policy with { ExpectedRegion = "China" }).Issues, x => x.Code == "selected_detail_receipt");
        fixture.Provenance["consumer_registry"]!["builds"]!["main"]!["sha256"] = new string('f', 64);
        Assert.Contains(fixture.Project().Issues, x => x.Code == "unsupported_binary");
        fixture = new ProjectionFixture(); fixture.Selection["filename"] = "npcs.xml";
        Assert.Contains(fixture.Project().Issues, x => x.Code == "selected_detail_receipt");
        fixture = new ProjectionFixture(); fixture.SourceReceipt["byte_end_exclusive"] = 1001;
        Assert.Contains(fixture.Project().Issues, x => x.Code == "source_span");
    }

    [Fact]
    public void DuplicateMetadataCannotSpoofTheRegionAndMissingNpcCannotUseAnotherProfile()
    {
        var fixture = new ProjectionFixture();
        var dataset = fixture.Dataset();
        Assert.Contains(RetailNpcTemplateProjection.Project(dataset, 2, ProjectionFixture.Policy).Issues, x => x.Code == "missing_profile");
        string duplicate = fixture.Provenance.ToJsonString().Replace("\"region\":\"USA\"", "\"region\":\"China\",\"region\":\"USA\"", StringComparison.Ordinal);
        using var document = JsonDocument.Parse(duplicate);
        var spoofed = new RetailNpcStaticDataset(dataset.DatasetId, dataset.OutputManifestSha256,
            dataset.Profiles.ToDictionary(x => x.Key, x => x.Value), 1, 0, 0,
            ImmutableArray<RetailNpcArtifactReceipt>.Empty, document.RootElement, dataset.SourceManifest);
        var result = RetailNpcTemplateProjection.Project(spoofed, 1, ProjectionFixture.Policy);
        Assert.False(result.Success); Assert.Null(result.Inputs);
        Assert.Contains(result.Issues, x => x.Code == "source_contract" && x.Message.Contains("Duplicate JSON property", StringComparison.Ordinal));
    }

    [Fact]
    public void EntryLimitsAndDisposedParsingDocumentsDoNotLoseOriginalEvidence()
    {
        var fixture = new ProjectionFixture(); fixture.AddSkills((1, "One"), (2, "Two"));
        Assert.Contains(fixture.Project(ProjectionFixture.Policy with { MaxOrderedEntries = 1 }).Issues, x => x.Code == "entry_limit");
        fixture.AddNode(Node("unknown", "retained", Node("nested", "opaque")));
        var dataset = fixture.Dataset(); // All temporary JsonDocuments have now been disposed.
        string original = dataset.Profiles[1].CompleteSourceProfile.GetRawText();
        var inputs = RetailNpcTemplateProjection.Project(dataset, 1, ProjectionFixture.Policy).Inputs!;
        Assert.Contains("opaque", inputs.RawRecord.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(ProjectionFixture.UntrustedPath, inputs.Receipt.SourcePath); // Projection never opens it.
        Assert.Equal(original, dataset.Profiles[1].CompleteSourceProfile.GetRawText());
        Assert.Equal(2, inputs.Skills.Entries.Length);
        Assert.False(inputs.ActivationReady);
    }

    [SkippableFact]
    public async Task All25FrozenPilotTypedVectorsMatchWithoutDefaultsOrActivation()
    {
        string? directory = Environment.GetEnvironmentVariable("AION_RETAIL_NPC_STATIC_DATASET");
        Skip.If(string.IsNullOrWhiteSpace(directory), "Set AION_RETAIL_NPC_STATIC_DATASET to the frozen USA generation; static source evidence only");
        using var vectors = JsonDocument.Parse(PilotVectors);
        const string datasetId = "33dfd001439620a102ccfca9b30765578efa88acb6f561f8c116ce8d98873335";
        const string manifest = "036c316db96dcb1d50ccd2b55ef7b472afff9be557ea10c385cc22369d49b0cd";
        var wanted = vectors.RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("id").GetInt32(), x => x.GetProperty("name").GetString()!);
        Assert.Equal(25, wanted.Count);
        var dataset = await RetailNpcDatasetReader.LoadAsync(new(directory!, datasetId, manifest, wanted));
        var policy = new RetailNpcTemplateProjectionPolicy(datasetId, manifest, "USA");
        int skills = 0;
        foreach (var vector in vectors.RootElement.EnumerateArray())
        {
            int id = vector.GetProperty("id").GetInt32();
            var result = RetailNpcTemplateProjection.Project(dataset, id, policy);
            Assert.True(result.Success, string.Join("; ", result.Issues));
            var inputs = result.Inputs!;
            foreach (var field in vector.GetProperty("integers").EnumerateObject())
                Assert.Equal(field.Value.ValueKind == JsonValueKind.Null ? (int?)null : field.Value.GetInt32(), inputs.Integers[field.Name].ParsedValue);
            foreach (var field in vector.GetProperty("speed_bits").EnumerateObject())
                Assert.Equal(field.Value.GetUInt32(), BitConverter.SingleToUInt32Bits(inputs.Speeds[field.Name].ParsedValue!.Value));
            foreach (var field in vector.GetProperty("elements").EnumerateObject())
                Assert.Equal(field.Value.GetInt16(), inputs.FirstLevel.Elements[field.Name].ParsedValue);
            foreach (var field in vector.GetProperty("bounds_bits").EnumerateObject())
                Assert.Equal(field.Value.GetUInt32(), BitConverter.SingleToUInt32Bits(inputs.Bounds.Axes[field.Name].ParsedValue!.Value));
            Assert.All(inputs.FirstLevel.Primaries.Values, x => Assert.Null(x.ParsedValue));
            var attack = Assert.Single(inputs.Attacks.Entries);
            Assert.Equal(vector.GetProperty("attack_type").GetString(), attack.Type.Value);
            Assert.Equal(vector.GetProperty("attack_rate").GetInt32(), attack.Weight.ParsedValue);
            Assert.Equal(vector.GetProperty("attack_mod").GetInt32(), attack.Modifier.ParsedValue);
            Assert.Equal(vector.GetProperty("attack_range_bits").GetUInt32(), BitConverter.SingleToUInt32Bits(attack.Range.ParsedValue!.Value));
            Assert.Equal(vector.GetProperty("skills").EnumerateArray().Select(x => x.GetInt32()), inputs.Skills.Entries.Select(x => x.Supported48Id));
            Assert.Equal(vector.GetProperty("raw_sha256").GetString(), inputs.Receipt.RawRecordSha256);
            Assert.Equal(vector.GetProperty("source_file_sha256").GetString(), inputs.Receipt.SourceFileSha256);
            Assert.Equal(dataset.Profiles[id].RawRecord.GetRawText(), inputs.RawRecord.GetRawText());
            Assert.Equal(RetailNpcSourcePresence.Absent, inputs.StatRatio.Presence);
            Assert.False(inputs.Bounds.VisualScaleApplied);
            Assert.False(inputs.DefaultsResolved); Assert.False(inputs.EffectiveInputsVerified); Assert.False(inputs.ActivationReady);
            Assert.False(RetailNpcInitializationRules.Assess(inputs).CanConstructOrdinarySeed);
            skills += inputs.Skills.Entries.Length;
        }
        Assert.Equal(59, skills);
    }

    private static RetailNpcTemplateInputs Project(ProjectionFixture fixture)
    {
        var result = fixture.Project();
        Assert.True(result.Success, string.Join("; ", result.Issues));
        return result.Inputs!;
    }

    private static JsonArray Node(string tag, string? value, params JsonNode[] children)
    {
        var content = new JsonArray();
        if (value is not null) content.Add(value);
        foreach (var child in children) content.Add(child);
        return new JsonArray(JsonValue.Create(tag), new JsonArray(), content);
    }

    private sealed class ProjectionFixture
    {
        internal const string UntrustedPath = @"Z:\must-not-be-opened\npcs_monsters.xml";
        internal static RetailNpcTemplateProjectionPolicy Policy => new(new string('d', 64), new string('e', 64), "USA");
        internal JsonArray Root { get; } = Node("npc", null, Node("id", "1"), Node("name", "One"), Node("level", "32"));
        internal JsonArray Bindings { get; } = [];
        internal JsonObject Selection { get; } = new() { ["filename"] = "npcs_monsters.xml", ["selected"] = UntrustedPath, ["region"] = "USA", ["reason"] = "regional_missing_base_fallback" };
        internal JsonObject SourceReceipt { get; } = new() { ["path"] = UntrustedPath, ["byte_start"] = 0, ["byte_end_exclusive"] = 64, ["line"] = 11, ["record_index"] = 0, ["raw_sha256"] = new string('a', 64), ["semantic_sha256"] = new string('b', 64) };
        internal JsonObject Provenance { get; }
        internal ProjectionFixture()
        {
            Provenance = new() { ["consumer_registry"] = new JsonObject { ["builds"] = new JsonObject {
                ["main"] = new JsonObject { ["sha256"] = RetailNpcTemplateProjection.MainBinarySha256 },
                ["npc"] = new JsonObject { ["sha256"] = RetailNpcTemplateProjection.NpcBinarySha256 } } },
                ["selection"] = new JsonArray(Selection) };
        }
        internal void Add(string tag, string value) => AddNode(Node(tag, value));
        internal void AddNode(JsonNode node) => Root[2]!.AsArray().Add(node);
        internal void AddSkills(params (int Id, string Name)[] skills)
        {
            var entries = skills.Select(x => (JsonNode)Node("data", null, Node("skill_name", x.Name))).ToArray();
            AddNode(Node("skills", null, entries));
            for (int i = 0; i < skills.Length; i++)
                Bindings.Add(new JsonObject { ["source_group_index"] = 0, ["source_entry_index"] = i, ["name"] = skills[i].Name,
                    ["supported48_id"] = skills[i].Id, ["status"] = "dual_join_agrees_static_only", ["effect_semantics_verified"] = false,
                    ["retail_name_id_candidates"] = new JsonArray(JsonValue.Create(skills[i].Id)), ["supported48_stack_id_candidates"] = new JsonArray(JsonValue.Create(skills[i].Id)) });
        }
        internal RetailNpcTemplateProjectionResult Project(RetailNpcTemplateProjectionPolicy? policy = null) => RetailNpcTemplateProjection.Project(Dataset(), 1, policy ?? Policy);
        internal RetailNpcStaticDataset Dataset()
        {
            var profile = new JsonObject { ["schema_version"] = 1, ["dataset_id"] = Policy.ExpectedDatasetId, ["npc_id"] = 1, ["devname"] = "One",
                ["scalar_fields"] = new JsonObject(), ["raw_record"] = Root.DeepClone(), ["skill_bindings"] = Bindings.DeepClone(),
                ["source"] = SourceReceipt.DeepClone(), ["ordered_lists"] = new JsonObject(), ["binding48"] = new JsonObject(), ["aggregate_crosscheck"] = new JsonObject(),
                ["blockers"] = new JsonArray("effective_runtime_profile_not_verified"),
                ["verification"] = new JsonObject { ["stage"] = "static_source_extracted", ["fixture_verified"] = false, ["activation_ready"] = false } };
            var manifest = new JsonArray(new JsonObject { ["path"] = UntrustedPath, ["bytes"] = 1000, ["sha256"] = new string('c', 64), ["record_count"] = 1, ["record_tag"] = "npc", ["strict_xml_complete"] = true });
            using var sourceJson = JsonDocument.Parse(profile.ToJsonString());
            using var provenanceJson = JsonDocument.Parse(Provenance.ToJsonString());
            using var manifestJson = JsonDocument.Parse(manifest.ToJsonString());
            var source = new RetailNpcSourceProfile(sourceJson.RootElement);
            return new RetailNpcStaticDataset(Policy.ExpectedDatasetId, Policy.ExpectedOutputManifestSha256,
                new Dictionary<int, RetailNpcSourceProfile> { [1] = source }, 1, 0, 0,
                ImmutableArray<RetailNpcArtifactReceipt>.Empty, provenanceJson.RootElement, manifestJson.RootElement);
        }
    }

    // PILOT_TYPED_SOURCE_VECTORS: generated independently from the pinned raw source record
    // and source-manifest receipts, not from the C# projector. Source-only, never native outputs.
    private const string PilotVectors = """
[{"id":212761,"name":"Elementalwater1D_30_An","integers":{"level":30,"max_hp":3856,"hp_regen":1928,"max_mp":null,"mp_regen":null,"physical_defend":199,"magical_defend":0,"magical_resist":653,"min_damage":226,"max_damage":339,"hit_count":1,"attack_delay":2300,"hit_accuracy":822,"magical_hit_accuracy":603,"dodge":460,"parry":0,"block":0,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":null,"physical_critical_damage_reduce":null,"magical_critical_reduce_rate":null,"magical_critical_damage_reduce":null,"proc_reduce_rate":null,"stat_ratio":null,"scale":100,"dual":0},"speed_bits":{"move_speed_normal_walk":1061997773,"move_speed_normal_run":1090519040,"move_speed_combat_run":1086324736},"elements":{"elemental_defend_light":130,"elemental_defend_dark":130,"elemental_defend_earth":130,"elemental_defend_air":130,"elemental_defend_water":530,"elemental_defend_fire":130},"bounds_bits":{"front":1058642330,"side":1065353216,"upper":1072315761},"attack_type":"magical_water","attack_rate":1000,"attack_mod":100,"attack_range_bits":1098907648,"skills":[16724],"raw_sha256":"5e922a1b6a45f05a97f7e8f1f8470db8a442b63726800956d3accc2ab1fc2a7e","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212762,"name":"Elementalwater2D_30_An","integers":{"level":30,"max_hp":3856,"hp_regen":1928,"max_mp":null,"mp_regen":null,"physical_defend":199,"magical_defend":0,"magical_resist":653,"min_damage":226,"max_damage":339,"hit_count":1,"attack_delay":2300,"hit_accuracy":822,"magical_hit_accuracy":603,"dodge":460,"parry":0,"block":0,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":null,"physical_critical_damage_reduce":null,"magical_critical_reduce_rate":null,"magical_critical_damage_reduce":null,"proc_reduce_rate":null,"stat_ratio":null,"scale":100,"dual":0},"speed_bits":{"move_speed_normal_walk":1067030938,"move_speed_normal_run":1091043328,"move_speed_combat_run":1086324736},"elements":{"elemental_defend_light":130,"elemental_defend_dark":130,"elemental_defend_earth":130,"elemental_defend_air":130,"elemental_defend_water":530,"elemental_defend_fire":130},"bounds_bits":{"front":1065353216,"side":1065353216,"upper":1081711002},"attack_type":"magical_water","attack_rate":1000,"attack_mod":100,"attack_range_bits":1098907648,"skills":[16590,16724],"raw_sha256":"4ef64004f51dbf30ad436f59e22a20d757aecdbab62e1c7599ed11bd2499493f","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212763,"name":"Elementalwater2D_31_An","integers":{"level":31,"max_hp":4122,"hp_regen":2061,"max_mp":null,"mp_regen":null,"physical_defend":207,"magical_defend":0,"magical_resist":671,"min_damage":236,"max_damage":355,"hit_count":1,"attack_delay":2300,"hit_accuracy":843,"magical_hit_accuracy":621,"dodge":472,"parry":0,"block":0,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":null,"physical_critical_damage_reduce":null,"magical_critical_reduce_rate":null,"magical_critical_damage_reduce":null,"proc_reduce_rate":null,"stat_ratio":null,"scale":100,"dual":0},"speed_bits":{"move_speed_normal_walk":1067030938,"move_speed_normal_run":1091043328,"move_speed_combat_run":1086324736},"elements":{"elemental_defend_light":130,"elemental_defend_dark":130,"elemental_defend_earth":130,"elemental_defend_air":130,"elemental_defend_water":530,"elemental_defend_fire":130},"bounds_bits":{"front":1065353216,"side":1065353216,"upper":1081711002},"attack_type":"magical_water","attack_rate":1000,"attack_mod":100,"attack_range_bits":1098907648,"skills":[16590,16724],"raw_sha256":"f97f8ce03691345573050b51eb98068570ccd19a6e821f24040656b1a2e47b53","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212764,"name":"Elementalwater3D_31_An","integers":{"level":31,"max_hp":4122,"hp_regen":2061,"max_mp":null,"mp_regen":null,"physical_defend":207,"magical_defend":0,"magical_resist":671,"min_damage":236,"max_damage":355,"hit_count":1,"attack_delay":2300,"hit_accuracy":843,"magical_hit_accuracy":621,"dodge":472,"parry":0,"block":0,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":null,"physical_critical_damage_reduce":null,"magical_critical_reduce_rate":null,"magical_critical_damage_reduce":null,"proc_reduce_rate":null,"stat_ratio":null,"scale":100,"dual":0},"speed_bits":{"move_speed_normal_walk":1070805811,"move_speed_normal_run":1088421888,"move_speed_combat_run":1085276160},"elements":{"elemental_defend_light":130,"elemental_defend_dark":130,"elemental_defend_earth":130,"elemental_defend_air":130,"elemental_defend_water":530,"elemental_defend_fire":130},"bounds_bits":{"front":1067030938,"side":1065353216,"upper":1080452710},"attack_type":"magical_water","attack_rate":1000,"attack_mod":100,"attack_range_bits":1098907648,"skills":[16724,16593,16699],"raw_sha256":"2286e543e752c1e1e77f44cbb4f03dd2292d4161b018a71857632357770500c8","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212765,"name":"Elementalearth1D_30_An","integers":{"level":30,"max_hp":4808,"hp_regen":2164,"max_mp":null,"mp_regen":null,"physical_defend":524,"magical_defend":0,"magical_resist":639,"min_damage":152,"max_damage":228,"hit_count":1,"attack_delay":2310,"hit_accuracy":822,"magical_hit_accuracy":611,"dodge":0,"parry":0,"block":560,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":100,"dual":0},"speed_bits":{"move_speed_normal_walk":1067030938,"move_speed_normal_run":1090519040,"move_speed_combat_run":1086324736},"elements":{"elemental_defend_light":90,"elemental_defend_dark":90,"elemental_defend_earth":40,"elemental_defend_air":40,"elemental_defend_water":40,"elemental_defend_fire":40},"bounds_bits":{"front":1060320051,"side":1060320051,"upper":1065353216},"attack_type":"magical_Earth","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[16561],"raw_sha256":"fc1aba5650958628616c9fbaee1ebe222603573a208e64a43d68e233d12cc0f0","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212766,"name":"Elementalearth2D_30_An","integers":{"level":30,"max_hp":4808,"hp_regen":2164,"max_mp":null,"mp_regen":null,"physical_defend":524,"magical_defend":0,"magical_resist":639,"min_damage":152,"max_damage":228,"hit_count":1,"attack_delay":2310,"hit_accuracy":822,"magical_hit_accuracy":611,"dodge":0,"parry":0,"block":560,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":100,"dual":0},"speed_bits":{"move_speed_normal_walk":1067030938,"move_speed_normal_run":1090519040,"move_speed_combat_run":1086324736},"elements":{"elemental_defend_light":90,"elemental_defend_dark":90,"elemental_defend_earth":40,"elemental_defend_air":40,"elemental_defend_water":40,"elemental_defend_fire":40},"bounds_bits":{"front":1065353216,"side":1063675494,"upper":1070386381},"attack_type":"magical_Earth","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[16836,16561],"raw_sha256":"67906fb717448503fd1c826bfd763cf3a2819e69334603f3b19c3007913d0873","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212767,"name":"Elementalearth2D_31_An","integers":{"level":31,"max_hp":5141,"hp_regen":2313,"max_mp":null,"mp_regen":null,"physical_defend":538,"magical_defend":0,"magical_resist":659,"min_damage":159,"max_damage":239,"hit_count":1,"attack_delay":2310,"hit_accuracy":843,"magical_hit_accuracy":627,"dodge":0,"parry":0,"block":572,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":100,"dual":0},"speed_bits":{"move_speed_normal_walk":1067030938,"move_speed_normal_run":1090519040,"move_speed_combat_run":1086324736},"elements":{"elemental_defend_light":90,"elemental_defend_dark":90,"elemental_defend_earth":40,"elemental_defend_air":40,"elemental_defend_water":40,"elemental_defend_fire":40},"bounds_bits":{"front":1065353216,"side":1063675494,"upper":1070386381},"attack_type":"magical_Earth","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[16836,16561],"raw_sha256":"8066d649e88cd21ca27287327e60718f58c1e6ba24de5effa2c84c4ce5c4e57d","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212768,"name":"Elementalearth3D_31_An","integers":{"level":31,"max_hp":5141,"hp_regen":2313,"max_mp":null,"mp_regen":null,"physical_defend":538,"magical_defend":0,"magical_resist":659,"min_damage":159,"max_damage":239,"hit_count":1,"attack_delay":2310,"hit_accuracy":843,"magical_hit_accuracy":627,"dodge":0,"parry":0,"block":572,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":100,"dual":0},"speed_bits":{"move_speed_normal_walk":1069547520,"move_speed_normal_run":1090519040,"move_speed_combat_run":1086324736},"elements":{"elemental_defend_light":90,"elemental_defend_dark":90,"elemental_defend_earth":40,"elemental_defend_air":40,"elemental_defend_water":40,"elemental_defend_fire":40},"bounds_bits":{"front":1072064102,"side":1069547520,"upper":1076048691},"attack_type":"magical_Earth","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[16561,16595,16588],"raw_sha256":"b5fcf0114a49355fd6cd3702e2cb2e2836bf46429f430a99e9f39c9a62542da2","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212769,"name":"Elementalair1D_30_An","integers":{"level":30,"max_hp":3637,"hp_regen":1637,"max_mp":null,"mp_regen":null,"physical_defend":351,"magical_defend":0,"magical_resist":639,"min_damage":181,"max_damage":272,"hit_count":1,"attack_delay":2486,"hit_accuracy":822,"magical_hit_accuracy":611,"dodge":0,"parry":0,"block":574,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":100,"dual":0},"speed_bits":{"move_speed_normal_walk":1062484312,"move_speed_normal_run":1090519040,"move_speed_combat_run":1086324736},"elements":{"elemental_defend_light":50,"elemental_defend_dark":50,"elemental_defend_earth":0,"elemental_defend_air":0,"elemental_defend_water":0,"elemental_defend_fire":0},"bounds_bits":{"front":1060320051,"side":1067030938,"upper":1066779279},"attack_type":"magical_Air","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[16725],"raw_sha256":"bdff66ff66254003bd5f823c50b4ee85f5b377fde45b16b92733ee7bb3fe384f","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212770,"name":"Elementalair2D_30_An","integers":{"level":30,"max_hp":3637,"hp_regen":1637,"max_mp":null,"mp_regen":null,"physical_defend":351,"magical_defend":0,"magical_resist":639,"min_damage":181,"max_damage":272,"hit_count":1,"attack_delay":2486,"hit_accuracy":822,"magical_hit_accuracy":611,"dodge":0,"parry":0,"block":574,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":100,"dual":0},"speed_bits":{"move_speed_normal_walk":1067030938,"move_speed_normal_run":1090781184,"move_speed_combat_run":1086324736},"elements":{"elemental_defend_light":50,"elemental_defend_dark":50,"elemental_defend_earth":0,"elemental_defend_air":0,"elemental_defend_water":0,"elemental_defend_fire":0},"bounds_bits":{"front":1061997773,"side":1072064102,"upper":1071476900},"attack_type":"magical_Air","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[17039,16725],"raw_sha256":"93d3a604c96f6236fcfb1cf59d5db71613a365cd54cdae7e36c65485e46afd78","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212771,"name":"Elementalair2D_31_An","integers":{"level":31,"max_hp":3889,"hp_regen":1750,"max_mp":null,"mp_regen":null,"physical_defend":362,"magical_defend":0,"magical_resist":659,"min_damage":189,"max_damage":284,"hit_count":1,"attack_delay":2486,"hit_accuracy":843,"magical_hit_accuracy":627,"dodge":0,"parry":0,"block":586,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":100,"dual":0},"speed_bits":{"move_speed_normal_walk":1067030938,"move_speed_normal_run":1090781184,"move_speed_combat_run":1086324736},"elements":{"elemental_defend_light":50,"elemental_defend_dark":50,"elemental_defend_earth":0,"elemental_defend_air":0,"elemental_defend_water":0,"elemental_defend_fire":0},"bounds_bits":{"front":1061997773,"side":1072064102,"upper":1071476900},"attack_type":"magical_Air","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[17039,16725],"raw_sha256":"1173136bf414a29ed48625f3c82264dd3ffa005a4e04a2895fd32498a321ee31","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212772,"name":"Elementalair3D_32_An","integers":{"level":32,"max_hp":4125,"hp_regen":1856,"max_mp":null,"mp_regen":null,"physical_defend":372,"magical_defend":0,"magical_resist":677,"min_damage":196,"max_damage":294,"hit_count":1,"attack_delay":2486,"hit_accuracy":864,"magical_hit_accuracy":643,"dodge":0,"parry":0,"block":598,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":100,"dual":0},"speed_bits":{"move_speed_normal_walk":1068792545,"move_speed_normal_run":1090519040,"move_speed_combat_run":1086324736},"elements":{"elemental_defend_light":40,"elemental_defend_dark":40,"elemental_defend_earth":-10,"elemental_defend_air":-10,"elemental_defend_water":-10,"elemental_defend_fire":-10},"bounds_bits":{"front":1067030938,"side":1075838976,"upper":1074287084},"attack_type":"magical_air","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[16725,16594,17039],"raw_sha256":"2387124fc6b7d0d0f1cb93cc03d533410f7ef1cb6ae8e31865b2580f81e62476","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212773,"name":"ElementalFire1D_30_Ae","integers":{"level":30,"max_hp":4599,"hp_regen":2070,"max_mp":null,"mp_regen":null,"physical_defend":524,"magical_defend":0,"magical_resist":639,"min_damage":154,"max_damage":231,"hit_count":1,"attack_delay":2310,"hit_accuracy":822,"magical_hit_accuracy":611,"dodge":0,"parry":0,"block":560,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":100,"dual":0},"speed_bits":{"move_speed_normal_walk":1065353216,"move_speed_normal_run":1088421888,"move_speed_combat_run":1085276160},"elements":{"elemental_defend_light":90,"elemental_defend_dark":90,"elemental_defend_earth":40,"elemental_defend_air":40,"elemental_defend_water":40,"elemental_defend_fire":40},"bounds_bits":{"front":1065353216,"side":1065353216,"upper":1067869798},"attack_type":"magical_Fire","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[16723],"raw_sha256":"c2a94af3ee50134c2bf4a235d7a6f54549b3020bc17d0e285ec7cce720d128eb","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212774,"name":"ElementalFire2D_30_Ae","integers":{"level":30,"max_hp":4599,"hp_regen":2070,"max_mp":null,"mp_regen":null,"physical_defend":524,"magical_defend":0,"magical_resist":639,"min_damage":154,"max_damage":231,"hit_count":1,"attack_delay":2310,"hit_accuracy":822,"magical_hit_accuracy":611,"dodge":0,"parry":0,"block":560,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":140,"dual":0},"speed_bits":{"move_speed_normal_walk":1069547520,"move_speed_normal_run":1089470464,"move_speed_combat_run":1085276160},"elements":{"elemental_defend_light":90,"elemental_defend_dark":90,"elemental_defend_earth":40,"elemental_defend_air":40,"elemental_defend_water":40,"elemental_defend_fire":40},"bounds_bits":{"front":1061997773,"side":1061997773,"upper":1068373115},"attack_type":"magical_Fire","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[16589,16723],"raw_sha256":"e686e486a7b5b7880757340c93f68c7917ec8389b0dc77d969f93fe8be6cdade","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212775,"name":"ElementalFire2D_31_Ae","integers":{"level":31,"max_hp":4917,"hp_regen":2213,"max_mp":null,"mp_regen":null,"physical_defend":538,"magical_defend":0,"magical_resist":659,"min_damage":160,"max_damage":240,"hit_count":1,"attack_delay":2310,"hit_accuracy":843,"magical_hit_accuracy":627,"dodge":0,"parry":0,"block":572,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":140,"dual":0},"speed_bits":{"move_speed_normal_walk":1069547520,"move_speed_normal_run":1089470464,"move_speed_combat_run":1085276160},"elements":{"elemental_defend_light":90,"elemental_defend_dark":90,"elemental_defend_earth":40,"elemental_defend_air":40,"elemental_defend_water":40,"elemental_defend_fire":40},"bounds_bits":{"front":1061997773,"side":1061997773,"upper":1068373115},"attack_type":"magical_Fire","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[16589,16723],"raw_sha256":"5f42b4cc3f0ac6e3d678465d9bd3679d9dd1a7099b4bcf46658e9558f1c28d6a","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212776,"name":"ShadowstalkerD_30_Ae","integers":{"level":30,"max_hp":3637,"hp_regen":1637,"max_mp":null,"mp_regen":null,"physical_defend":332,"magical_defend":0,"magical_resist":739,"min_damage":181,"max_damage":272,"hit_count":1,"attack_delay":2610,"hit_accuracy":822,"magical_hit_accuracy":611,"dodge":0,"parry":0,"block":633,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":100,"dual":0},"speed_bits":{"move_speed_normal_walk":1069547520,"move_speed_normal_run":1088421888,"move_speed_combat_run":1085276160},"elements":{"elemental_defend_light":50,"elemental_defend_dark":50,"elemental_defend_earth":0,"elemental_defend_air":0,"elemental_defend_water":0,"elemental_defend_fire":0},"bounds_bits":{"front":1075629261,"side":1073322394,"upper":1077327954},"attack_type":"physical","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[16794,16677,16556],"raw_sha256":"cbbf5eef68a0d1f2a39322bd5a598d0d94db0664e31b6b3b90a15e6ca208a81f","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212777,"name":"ElementalFire3D_31_Ae","integers":{"level":31,"max_hp":4917,"hp_regen":2213,"max_mp":null,"mp_regen":null,"physical_defend":538,"magical_defend":0,"magical_resist":659,"min_damage":160,"max_damage":240,"hit_count":1,"attack_delay":2310,"hit_accuracy":843,"magical_hit_accuracy":627,"dodge":0,"parry":0,"block":572,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":180,"dual":0},"speed_bits":{"move_speed_normal_walk":1069547520,"move_speed_normal_run":1090519040,"move_speed_combat_run":1086324736},"elements":{"elemental_defend_light":90,"elemental_defend_dark":90,"elemental_defend_earth":40,"elemental_defend_air":40,"elemental_defend_water":40,"elemental_defend_fire":40},"bounds_bits":{"front":1065353216,"side":1061997773,"upper":1070302495},"attack_type":"magical_Fire","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[16723,16592,16522],"raw_sha256":"5bb00272cf90e69c43535ad884753105153ee3947f716fa8764ea99a852e13d1","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212778,"name":"ElementalFire3D_32_Ae","integers":{"level":32,"max_hp":5215,"hp_regen":2347,"max_mp":null,"mp_regen":null,"physical_defend":554,"magical_defend":0,"magical_resist":677,"min_damage":166,"max_damage":249,"hit_count":1,"attack_delay":2310,"hit_accuracy":864,"magical_hit_accuracy":643,"dodge":0,"parry":0,"block":583,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":180,"dual":0},"speed_bits":{"move_speed_normal_walk":1069547520,"move_speed_normal_run":1090519040,"move_speed_combat_run":1086324736},"elements":{"elemental_defend_light":90,"elemental_defend_dark":90,"elemental_defend_earth":40,"elemental_defend_air":40,"elemental_defend_water":40,"elemental_defend_fire":40},"bounds_bits":{"front":1065353216,"side":1061997773,"upper":1070302495},"attack_type":"magical_Fire","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[16723,16592,16522],"raw_sha256":"004309624e2f1ba00c87992181767c4f5f54aea0e083d35ebf37927d4a69575f","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212779,"name":"SouledstoneAReD_31_Ae","integers":{"level":31,"max_hp":5141,"hp_regen":2313,"max_mp":null,"mp_regen":null,"physical_defend":652,"magical_defend":0,"magical_resist":609,"min_damage":183,"max_damage":275,"hit_count":1,"attack_delay":2657,"hit_accuracy":843,"magical_hit_accuracy":627,"dodge":0,"parry":0,"block":543,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":100,"dual":0},"speed_bits":{"move_speed_normal_walk":1083179008,"move_speed_normal_run":1088421888,"move_speed_combat_run":1085276160},"elements":{"elemental_defend_light":90,"elemental_defend_dark":90,"elemental_defend_earth":40,"elemental_defend_air":40,"elemental_defend_water":40,"elemental_defend_fire":40},"bounds_bits":{"front":1078271672,"side":1076300349,"upper":1077936128},"attack_type":"magical_Fire","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[16576,16589],"raw_sha256":"fef2d6bb66f4d427401431c68a500df307aeed436a1744bf59873a79956922b1","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212780,"name":"ElementalFire4D_33_Ae","integers":{"level":33,"max_hp":5497,"hp_regen":2474,"max_mp":null,"mp_regen":null,"physical_defend":569,"magical_defend":0,"magical_resist":698,"min_damage":173,"max_damage":260,"hit_count":1,"attack_delay":2310,"hit_accuracy":886,"magical_hit_accuracy":663,"dodge":0,"parry":0,"block":596,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":180,"dual":0},"speed_bits":{"move_speed_normal_walk":1069547520,"move_speed_normal_run":1090519040,"move_speed_combat_run":1086324736},"elements":{"elemental_defend_light":90,"elemental_defend_dark":90,"elemental_defend_earth":40,"elemental_defend_air":40,"elemental_defend_water":40,"elemental_defend_fire":40},"bounds_bits":{"front":1069547520,"side":1065353216,"upper":1078145843},"attack_type":"magical_Fire","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[16723,16592,16522,16710],"raw_sha256":"23832cdd4abb0e092afb8daa58787035d9040205b9b8c3e723c2e76bdcd71185","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212781,"name":"ShadowstalkerBigD_32_Ae","integers":{"level":31,"max_hp":3889,"hp_regen":1750,"max_mp":null,"mp_regen":null,"physical_defend":343,"magical_defend":0,"magical_resist":759,"min_damage":189,"max_damage":284,"hit_count":1,"attack_delay":2610,"hit_accuracy":843,"magical_hit_accuracy":627,"dodge":0,"parry":0,"block":647,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":130,"dual":0},"speed_bits":{"move_speed_normal_walk":1069547520,"move_speed_normal_run":1088421888,"move_speed_combat_run":1085276160},"elements":{"elemental_defend_light":50,"elemental_defend_dark":50,"elemental_defend_earth":0,"elemental_defend_air":0,"elemental_defend_water":0,"elemental_defend_fire":0},"bounds_bits":{"front":1075629261,"side":1073322394,"upper":1077327954},"attack_type":"physical","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[16794,16677,16556],"raw_sha256":"bc16907e410cf71fb6e00471a27d1cb43bfca8e544d7cd5d5d414d864cfe0dff","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212782,"name":"SouledstoneAReBigS_32_Ae","integers":{"level":32,"max_hp":5452,"hp_regen":2453,"max_mp":null,"mp_regen":null,"physical_defend":670,"magical_defend":0,"magical_resist":627,"min_damage":189,"max_damage":284,"hit_count":1,"attack_delay":2657,"hit_accuracy":864,"magical_hit_accuracy":643,"dodge":0,"parry":0,"block":553,"critical":5,"magical_critical":5,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":130,"dual":0},"speed_bits":{"move_speed_normal_walk":1083179008,"move_speed_normal_run":1088421888,"move_speed_combat_run":1085276160},"elements":{"elemental_defend_light":90,"elemental_defend_dark":90,"elemental_defend_earth":40,"elemental_defend_air":40,"elemental_defend_water":40,"elemental_defend_fire":40},"bounds_bits":{"front":1078271672,"side":1076300349,"upper":1077936128},"attack_type":"magical_Fire","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[16576,16589],"raw_sha256":"fba425cd99cd0f1a358967bfd33e1eee80810eb2552cdd0a480f21e81c38783b","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212833,"name":"Elementalwater3NMDQ_31_An","integers":{"level":31,"max_hp":5359,"hp_regen":2680,"max_mp":null,"mp_regen":null,"physical_defend":207,"magical_defend":0,"magical_resist":671,"min_damage":355,"max_damage":516,"hit_count":1,"attack_delay":2300,"hit_accuracy":843,"magical_hit_accuracy":621,"dodge":607,"parry":0,"block":0,"critical":9,"magical_critical":9,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":null,"physical_critical_damage_reduce":null,"magical_critical_reduce_rate":null,"magical_critical_damage_reduce":null,"proc_reduce_rate":null,"stat_ratio":null,"scale":130,"dual":0},"speed_bits":{"move_speed_normal_walk":1070805811,"move_speed_normal_run":1088421888,"move_speed_combat_run":1085276160},"elements":{"elemental_defend_light":130,"elemental_defend_dark":130,"elemental_defend_earth":130,"elemental_defend_air":130,"elemental_defend_water":530,"elemental_defend_fire":130},"bounds_bits":{"front":1067030938,"side":1065353216,"upper":1080452710},"attack_type":"magical_water","attack_rate":1000,"attack_mod":100,"attack_range_bits":1098907648,"skills":[16724,16593,16699],"raw_sha256":"279beb03e5e55b8af062359aa424551dc3347061cdc082a20238090e55c883b2","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212835,"name":"ElementalEarth3NamedD_32_Ae","integers":{"level":32,"max_hp":5905,"hp_regen":2214,"max_mp":null,"mp_regen":null,"physical_defend":461,"magical_defend":0,"magical_resist":703,"min_damage":206,"max_damage":300,"hit_count":1,"attack_delay":2625,"hit_accuracy":864,"magical_hit_accuracy":643,"dodge":0,"parry":0,"block":722,"critical":9,"magical_critical":9,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":130,"dual":0},"speed_bits":{"move_speed_normal_walk":1069547520,"move_speed_normal_run":1090519040,"move_speed_combat_run":1086324736},"elements":{"elemental_defend_light":90,"elemental_defend_dark":90,"elemental_defend_earth":65,"elemental_defend_air":65,"elemental_defend_water":65,"elemental_defend_fire":65},"bounds_bits":{"front":1072064102,"side":1069547520,"upper":1076048691},"attack_type":"magical_Earth","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[16561,16595,16588],"raw_sha256":"e1c723e52067779b9d7487ecbfff5e4dd97966f4f64132a4bb992f02e5e08460","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"},{"id":212876,"name":"ElementalFire4NamedQ_33_Ae","integers":{"level":33,"max_hp":5954,"hp_regen":2233,"max_mp":null,"mp_regen":null,"physical_defend":474,"magical_defend":0,"magical_resist":723,"min_damage":216,"max_damage":315,"hit_count":1,"attack_delay":2625,"hit_accuracy":886,"magical_hit_accuracy":663,"dodge":0,"parry":0,"block":738,"critical":9,"magical_critical":9,"concentration":0,"magical_skill_boost":0,"magical_skill_boost_resist":0,"physical_critical_reduce_rate":0,"physical_critical_damage_reduce":0,"magical_critical_reduce_rate":0,"magical_critical_damage_reduce":0,"proc_reduce_rate":null,"stat_ratio":null,"scale":180,"dual":0},"speed_bits":{"move_speed_normal_walk":1069547520,"move_speed_normal_run":1090519040,"move_speed_combat_run":1086324736},"elements":{"elemental_defend_light":90,"elemental_defend_dark":90,"elemental_defend_earth":65,"elemental_defend_air":65,"elemental_defend_water":65,"elemental_defend_fire":65},"bounds_bits":{"front":1069547520,"side":1065353216,"upper":1078145843},"attack_type":"magical_Fire","attack_rate":1000,"attack_mod":100,"attack_range_bits":1073741824,"skills":[16723,16592,16522,16710],"raw_sha256":"91bb82ad05d86a836931490361ee974a1476434d2a73a8694ec6b1285078e7ca","source_file_sha256":"7b677ced7eb7df95d6dfb94842f022fcd5d82d1bd587cd8c72149637e11dee4f"}]
""";
}
