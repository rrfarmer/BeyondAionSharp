using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aion.GameServer.Dataholders.RetailNpc;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcDatasetReaderTests
{
    [Fact]
    public async Task AdmitsOnlyStaticEvidenceAndRetainsRequestedClonedRawNodes()
    {
        using var fixture = new RetailNpcDatasetFixture();
        var data = await RetailNpcDatasetReader.LoadAsync(fixture.Request());
        Assert.True(data.StaticSourceAdmitted);
        Assert.False(data.FixtureVerified);
        Assert.False(data.ActivationReady);
        Assert.Equal(3, data.CoverageCount);
        Assert.Equal(1, data.IdentityBlockerCount);
        var profile = Assert.Single(data.Profiles).Value;
        Assert.Equal("One", profile.Devname);
        Assert.Equal(RetailNpcSourcePresence.ExplicitZero, profile.ScalarFields["parry"].Presence);
        Assert.Equal(RetailNpcSourcePresence.Absent, profile.ScalarFields["magical_resist"].Presence);
        Assert.Equal(RetailNpcSourcePresence.Absent, profile.GetRawRootField("stat_ratio").Presence);
        Assert.Equal("retained", profile.GetRawRootField("opaque").RawValues[0]);
        Assert.Equal(new[] { "physical", "magical_Fire" }, profile.OrderedLists.GetProperty("attacks").GetProperty("entries").EnumerateArray()
            .Select(x => x.GetProperty("projected_fields").GetProperty("attack_type").GetProperty("raw_values")[0].GetString()));
        Assert.Equal("C:\\untrusted-provenance\\never-open.xml", profile.SourceProvenance.GetProperty("path").GetString());
        Assert.Equal(JsonValueKind.Array, profile.RawRecord.ValueKind); // Original parsing documents/streams are already disposed.
    }

    [Fact]
    public async Task ExplicitRawStatRatioZeroIsNotAbsentAndIsNeverDefaulted()
    {
        using var fixture = new RetailNpcDatasetFixture();
        var data = await RetailNpcDatasetReader.LoadAsync(fixture.Request(new Dictionary<int, string> { [2] = "Two" }));
        var field = data.Profiles[2].GetRawRootField("stat_ratio");
        Assert.Equal(RetailNpcSourcePresence.ExplicitZero, field.Presence);
        Assert.Equal("0", Assert.Single(field.RawValues));
        Assert.False(field.DefaultApplied);
        Assert.Equal("raw_lexeme_unresolved", field.Projection);
    }

    [Fact]
    public async Task LateUnrequestedProfileFailureDoesNotPublishTheEarlierCohort()
    {
        using var fixture = new RetailNpcDatasetFixture();
        fixture.Profiles[1]["dataset_id"] = new string('b', 64);
        fixture.Publish();
        await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcDatasetReader.LoadAsync(fixture.Request()));
    }

    [Fact]
    public async Task DuplicateJsonPropertiesCannotSpoofContractFields()
    {
        using var fixture = new RetailNpcDatasetFixture();
        string path = System.IO.Path.Combine(fixture.Path, "profiles.jsonl");
        string content = File.ReadAllText(path);
        content = content.Replace("\"dataset_id\":", "\"dataset_id\":\"" + RetailNpcDatasetFixture.DatasetId + "\",\"dataset_id\":", StringComparison.Ordinal);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        fixture.Rehash();
        await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcDatasetReader.LoadAsync(fixture.Request()));
    }

    [Theory]
    [InlineData("../outside.json")]
    [InlineData("C:\\outside.json")]
    [InlineData("nested/profiles.jsonl")]
    public async Task ManifestCannotSelectExternalOrUnexpectedFiles(string badName)
    {
        using var fixture = new RetailNpcDatasetFixture();
        var manifest = JsonNode.Parse(File.ReadAllText(System.IO.Path.Combine(fixture.Path, "output-manifest.json")))!.AsArray();
        manifest[0]!["file"] = badName;
        fixture.Write("output-manifest.json", manifest);
        await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcDatasetReader.LoadAsync(fixture.Request()));
    }

    [Fact]
    public async Task ManifestAndWholeFilePinsAreIndependentAndRequired()
    {
        using var fixture = new RetailNpcDatasetFixture();
        await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcDatasetReader.LoadAsync(fixture.Request() with { ExpectedOutputManifestSha256 = new string('b', 64) }));
        File.AppendAllText(System.IO.Path.Combine(fixture.Path, "profiles.jsonl"), " ");
        await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcDatasetReader.LoadAsync(fixture.Request()));
    }

    [Fact]
    public async Task DuplicateOrMissingCoverageCannotHideUnrequestedRows()
    {
        using var fixture = new RetailNpcDatasetFixture();
        string path = System.IO.Path.Combine(fixture.Path, "coverage.csv");
        File.AppendAllText(path, File.ReadAllLines(path)[1] + "\n");
        fixture.Rehash();
        await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcDatasetReader.LoadAsync(fixture.Request()));
    }

    [Fact]
    public async Task EveryIdentityBlockerMustHaveItsOwnMatchingLedgerRow()
    {
        using var fixture = new RetailNpcDatasetFixture();
        string path = System.IO.Path.Combine(fixture.Path, "identity-blockers.csv");
        File.WriteAllText(path, File.ReadAllLines(path)[0] + "\n", new UTF8Encoding(false));
        fixture.Rehash();
        await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcDatasetReader.LoadAsync(fixture.Request()));
    }

    [Fact]
    public async Task RequiredBlockedMissingOrWrongIdentityIsVisible()
    {
        using var fixture = new RetailNpcDatasetFixture();
        foreach (var pair in new[] { KeyValuePair.Create(3, "Missing"), KeyValuePair.Create(99, "Absent"), KeyValuePair.Create(1, "Wrong") })
            await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcDatasetReader.LoadAsync(fixture.Request(new Dictionary<int, string> { [pair.Key] = pair.Value })));
    }

    [Fact]
    public async Task SourceFalseFlagsCannotBePromotedToFixtureApproval()
    {
        using var fixture = new RetailNpcDatasetFixture();
        fixture.Profiles[0]["verification"]!["fixture_verified"] = true;
        fixture.Publish();
        await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcDatasetReader.LoadAsync(fixture.Request()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownSchemaLevelKeysAreRejectedWhileOpaqueRawNodesRemainAllowed(bool verification)
    {
        using var fixture = new RetailNpcDatasetFixture();
        JsonNode target = verification ? fixture.Profiles[0]["verification"]! : fixture.Profiles[0];
        target["unexpected_schema_key"] = "unreviewed";
        fixture.Publish();
        await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcDatasetReader.LoadAsync(fixture.Request()));
    }

    [Fact]
    public async Task ReorderedAttackProjectionCannotDisagreeWithRawSource()
    {
        using var fixture = new RetailNpcDatasetFixture();
        fixture.Profiles[0]["ordered_lists"]!["attacks"]!["entries"]![0]!["source_entry_index"] = 1;
        fixture.Publish();
        await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcDatasetReader.LoadAsync(fixture.Request()));
    }

    [Fact]
    public async Task ZeroCannotBeReplacedByAnInferredProjectedValue()
    {
        using var fixture = new RetailNpcDatasetFixture();
        fixture.Profiles[0]["scalar_fields"]!["parry"]!["raw_values"]![0] = "100";
        fixture.Publish();
        await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcDatasetReader.LoadAsync(fixture.Request()));
    }

    [Fact]
    public async Task GlobalSkillIssuesAreCountedButRequiredIncompatibleSkillsFail()
    {
        using var fixture = new RetailNpcDatasetFixture();
        fixture.AddUnresolvedSkillToSecondProfile();
        fixture.Publish();
        Assert.Single((await RetailNpcDatasetReader.LoadAsync(fixture.Request())).Profiles);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcDatasetReader.LoadAsync(fixture.Request(new Dictionary<int, string> { [2] = "Two" })));
        Assert.Contains("unresolved skill identity", error.Message);
    }

    [Fact]
    public async Task DuplicateRawIdentityCannotSelectTheFirstOrLastValue()
    {
        using var fixture = new RetailNpcDatasetFixture();
        fixture.Profiles[0]["raw_record"]![2]!.AsArray().Add(RetailNpcDatasetFixture.Node("name", "Other"));
        fixture.Publish();
        await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcDatasetReader.LoadAsync(fixture.Request()));
    }

    [Fact]
    public async Task CsvQuotedCommaAndNewlineAreReadAsOneField()
    {
        using var fixture = new RetailNpcDatasetFixture();
        string path = System.IO.Path.Combine(fixture.Path, "coverage.csv");
        File.WriteAllText(path, File.ReadAllText(path).Replace("exported_static_source_only,fixture,", "exported_static_source_only,\"fixture,\nquoted\",", StringComparison.Ordinal), new UTF8Encoding(false));
        fixture.Rehash();
        Assert.Single((await RetailNpcDatasetReader.LoadAsync(fixture.Request())).Profiles);
    }

    [Fact]
    public async Task EmptyOrRepeatedRequiredDevnamesAreRejected()
    {
        using var fixture = new RetailNpcDatasetFixture();
        foreach (var wanted in new[]
        {
            new Dictionary<int, string>(),
            new Dictionary<int, string> { [1] = "" },
            new Dictionary<int, string> { [1] = "One", [2] = "One" }
        })
            await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcDatasetReader.LoadAsync(fixture.Request(wanted)));
    }

    [Theory]
    [InlineData("whole")]
    [InlineData("metadata")]
    [InlineData("line")]
    [InlineData("depth")]
    [InlineData("rows")]
    [InlineData("retained")]
    public async Task BoundsRejectOversizedInputsWithoutPartialAdmission(string bound)
    {
        using var fixture = new RetailNpcDatasetFixture();
        var limits = new RetailNpcDatasetLimits();
        limits = bound switch
        {
            "whole" => limits with { MaxWholeInputBytes = 16 },
            "metadata" => limits with { MaxMetadataFileBytes = 16 },
            "line" => limits with { MaxProfileLineBytes = 16 },
            "depth" => limits with { MaxJsonDepth = 8 },
            "rows" => limits with { MaxCoverageRows = 2 },
            _ => limits with { MaxRetainedProfileBytes = 1 }
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcDatasetReader.LoadAsync(fixture.Request() with { Limits = limits }));
    }
}

/// <summary>Synthetic source evidence only; hashes pin the generated fixture, not a native server.</summary>
internal sealed class RetailNpcDatasetFixture : IDisposable
{
    internal const string DatasetId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Source = "C:\\untrusted-provenance\\never-open.xml";
    private const string CoverageHeader = "npc_id,devnames48,retail_devname,status,family,source_path,source_line,aggregate_crosscheck,fixture_verified,activation_ready\n";
    private const string SkillHeader = "npc_id,index,name,status,retail_id_candidates,stack_id_candidates\n";
    internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "retail-npc-static-" + Guid.NewGuid().ToString("N"));
    internal List<JsonObject> Profiles { get; } = [Profile(1, "One", false), Profile(2, "Two", true)];

    internal RetailNpcDatasetFixture() { Directory.CreateDirectory(Path); Publish(); }
    internal RetailNpcDatasetRequest Request(IReadOnlyDictionary<int, string>? wanted = null) => new(Path, DatasetId,
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(System.IO.Path.Combine(Path, "output-manifest.json")))), wanted ?? new Dictionary<int, string> { [1] = "One" });
    internal void Write(string name, JsonNode value) => File.WriteAllText(System.IO.Path.Combine(Path, name), value.ToJsonString(), new UTF8Encoding(false));
    internal static JsonArray Node(string tag, string? text = null, params JsonArray[] children)
    {
        var content = new JsonArray();
        if (text is not null) content.Add(text);
        foreach (var child in children) content.Add(child);
        return new JsonArray(JsonValue.Create(tag), new JsonArray(), content);
    }
    private static JsonObject Field(string? raw, string projection = "integer_lexeme") => new()
    {
        ["presence"] = raw is null ? "absent" : raw == "0" && projection != "string" ? "explicit_zero" : "explicit_value",
        ["raw_values"] = raw is null ? new JsonArray() : new JsonArray(JsonValue.Create(raw)), ["projection"] = projection, ["default_applied"] = false
    };
    private static JsonObject Profile(int id, string name, bool statZero)
    {
        var attack0 = Node("data", null, Node("attack_type", "physical"), Node("attack_rate", "1000"));
        var attack1 = Node("data", null, Node("attack_type", "magical_Fire"), Node("attack_rate", "0"));
        var raw = Node("npc", null, Node("id", id.ToString()), Node("name", name), Node("max_hp", "10"), Node("parry", "0"), Node("opaque", "retained"), Node("attacks", null, attack0, attack1));
        if (statZero) raw[2]!.AsArray().Add(Node("stat_ratio", "0"));
        JsonObject Entry(int index, string type, string rate) => new() { ["source_group_index"] = 0, ["source_entry_index"] = index, ["tag"] = "data", ["projected_fields"] = new JsonObject { ["attack_type"] = Field(type, "string"), ["attack_rate"] = Field(rate) } };
        return new JsonObject
        {
            ["schema_version"] = 1, ["dataset_id"] = DatasetId, ["npc_id"] = id, ["devname"] = name,
            ["binding48"] = new JsonObject { ["attrs48"] = new JsonObject { ["npc_id"] = id.ToString() }, ["devnames48"] = new JsonArray(JsonValue.Create(name)) },
            ["source"] = new JsonObject { ["path"] = Source, ["record_index"] = id - 1, ["line"] = id + 10, ["byte_start"] = 0, ["byte_end_exclusive"] = 64, ["raw_sha256"] = new string('0', 64), ["semantic_sha256"] = new string('1', 64) },
            ["raw_record"] = raw,
            ["scalar_fields"] = new JsonObject { ["max_hp"] = Field("10"), ["parry"] = Field("0"), ["magical_resist"] = Field(null) },
            ["ordered_lists"] = new JsonObject { ["attacks"] = new JsonObject { ["presence"] = "explicit_value", ["entries"] = new JsonArray(Entry(0, "physical", "1000"), Entry(1, "magical_Fire", "0")) }, ["skills"] = new JsonObject { ["presence"] = "absent", ["entries"] = new JsonArray() } },
            ["skill_bindings"] = new JsonArray(), ["aggregate_crosscheck"] = new JsonObject { ["status"] = "equal", ["authority"] = "crosscheck_only;selected_divided_record_retained" },
            ["blockers"] = new JsonArray("effective_runtime_profile_not_verified", "field_units_defaults_and_scaling_not_fully_recovered", "skill_effect_semantics_not_verified"),
            ["verification"] = new JsonObject { ["stage"] = "static_source_extracted", ["fixture_verified"] = false, ["activation_ready"] = false }
        };
    }
    internal void AddUnresolvedSkillToSecondProfile()
    {
        var profile = Profiles[1];
        profile["raw_record"]![2]!.AsArray().Add(Node("skills", null, Node("data", null, Node("skill_name", "UnknownSpell"))));
        profile["ordered_lists"]!["skills"] = new JsonObject { ["presence"] = "explicit_value", ["entries"] = new JsonArray(new JsonObject { ["source_group_index"] = 0, ["source_entry_index"] = 0, ["tag"] = "data", ["projected_fields"] = new JsonObject() }) };
        profile["skill_bindings"]!.AsArray().Add(new JsonObject { ["source_group_index"] = 0, ["source_entry_index"] = 0, ["name"] = "UnknownSpell", ["retail_name_id_candidates"] = new JsonArray(), ["supported48_stack_id_candidates"] = new JsonArray(), ["supported48_id"] = null, ["status"] = "retail_skill_name_absent_blocked", ["effect_semantics_verified"] = false });
        profile["blockers"]!.AsArray().Add("unresolved_skill_identity_binding");
    }
    internal void Publish()
    {
        File.WriteAllText(System.IO.Path.Combine(Path, "profiles.jsonl"), string.Join("\n", Profiles.Select(x => x.ToJsonString())) + "\n", new UTF8Encoding(false));
        File.WriteAllText(System.IO.Path.Combine(Path, "pilot-profiles.jsonl"), Profiles[0].ToJsonString() + "\n", new UTF8Encoding(false));
        string first = $"1,One,One,exported_static_source_only,fixture,{Source},11,equal,False,False\n";
        string second = $"2,Two,Two,exported_static_source_only,fixture,{Source},12,equal,False,False\n";
        string blocked = "3,Missing,,source_absent_blocked,fixture,,,not_applicable,False,False\n";
        File.WriteAllText(System.IO.Path.Combine(Path, "coverage.csv"), CoverageHeader + first + second + blocked, new UTF8Encoding(false));
        File.WriteAllText(System.IO.Path.Combine(Path, "identity-blockers.csv"), CoverageHeader + blocked, new UTF8Encoding(false));
        int issues = 0;
        var skillCsv = new StringBuilder(SkillHeader);
        foreach (var profile in Profiles)
            foreach (var binding in profile["skill_bindings"]!.AsArray())
                if ((string?)binding!["status"] != "dual_join_agrees_static_only")
                { issues++; skillCsv.Append($"{profile["npc_id"]},0,{binding["name"]},{binding["status"]},,\n"); }
        File.WriteAllText(System.IO.Path.Combine(Path, "unsupported-skill-bindings.csv"), skillCsv.ToString(), new UTF8Encoding(false));
        Write("summary.json", new JsonObject { ["schema_version"] = 1, ["dataset_id"] = DatasetId, ["target_count"] = 3, ["selected_retail_records"] = 2, ["aggregate_records"] = 2, ["exported_profiles"] = 2, ["identity_blockers"] = 1, ["pilot_profiles"] = 1, ["coverage_status_counts"] = new JsonObject { ["exported_static_source_only"] = 2, ["source_absent_blocked"] = 1 }, ["semantic_differences"] = 0, ["unsupported_skill_binding_rows"] = issues, ["fixture_verified"] = false, ["activation_ready"] = false });
        Write("provenance.json", new JsonObject { ["schema_version"] = 1, ["dataset_id"] = DatasetId, ["selection"] = new JsonArray(new JsonObject { ["selected"] = Source }), ["activation_ready"] = false });
        Write("source-manifest.json", new JsonArray(new JsonObject { ["path"] = Source, ["bytes"] = 1000, ["sha256"] = new string('0', 64) }));
        Write("semantic-diff.json", new JsonObject { ["compared_selected_records"] = 2, ["aggregate_records"] = 2, ["differences"] = new JsonArray() });
        Write("validation.json", new JsonObject { ["activation_ready"] = false, ["fixture_verified"] = false, ["replacement_decoding_used"] = false, ["duplicates_overwritten"] = 0, ["defaults_substituted"] = 0, ["every_target_has_coverage_row"] = true, ["profile_ids_exactly_supported_and_identity_matched"] = true, ["every_source_completed_strict_parse_or_was_hashed_input"] = true });
        Rehash();
    }
    internal void Rehash()
    {
        var manifest = new JsonArray();
        foreach (string file in Directory.GetFiles(Path).Select(System.IO.Path.GetFileName).Where(x => x != "output-manifest.json").Order(StringComparer.Ordinal)!)
        {
            byte[] bytes = File.ReadAllBytes(System.IO.Path.Combine(Path, file));
            manifest.Add(new JsonObject { ["file"] = file, ["bytes"] = bytes.Length, ["sha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes)) });
        }
        Write("output-manifest.json", manifest);
    }
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
