using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Aion.GameServer.Dataholders.RetailNpc;

internal sealed record RetailNpcManifestSource(long Bytes, string Sha256);

internal static class RetailNpcDatasetValidation
{
    private static readonly string[] CoverageHeaders = ["npc_id", "devnames48", "retail_devname", "status", "family", "source_path", "source_line", "aggregate_crosscheck", "fixture_verified", "activation_ready"];
    private static readonly string[] SkillHeaders = ["npc_id", "index", "name", "status", "retail_id_candidates", "stack_id_candidates"];
    internal sealed record CoverageRow(int Id, string Devnames48, string RetailDevname, string Status, string SourcePath, string SourceLine, string AggregateStatus, string[] Values)
    { public bool Exported => Status == "exported_static_source_only"; }
    internal sealed record SkillIssue(int NpcId, int Index, string Name, string Status, string RetailIds, string StackIds);

    internal static void Require([DoesNotReturnIf(false)] bool condition, string message)
    { if (!condition) throw new InvalidDataException(message); }

    internal static string String(JsonElement element, string name)
    {
        var value = element.GetProperty(name);
        Require(value.ValueKind == JsonValueKind.String, "Expected string: " + name);
        return value.GetString()!;
    }

    internal static void False(JsonElement element, string name) => Require(element.GetProperty(name).ValueKind == JsonValueKind.False, "Static source evidence requires false " + name);
    internal static void Count(JsonElement element, string name, int count) => Require(element.GetProperty(name).GetInt32() == count, "Count mismatch: " + name);
    internal static void Hash(string hash) => Require(hash is { Length: 64 } && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'), "Expected lowercase SHA256");

    internal static void ValidateLimits(RetailNpcDatasetLimits x)
    {
        Require(x.MaxWholeInputBytes > 0 && x.MaxMetadataFileBytes is > 0 and <= 256 * 1024 * 1024
            && x.MaxProfileLineBytes is > 0 and <= 64 * 1024 * 1024 && x.MaxJsonDepth is >= 8 and <= 512
            && x.MaxCoverageRows is > 0 and <= 1_000_000 && x.MaxRetainedProfiles is > 0 and <= 4096
            && x.MaxRetainedProfileBytes is > 0 and <= 512L * 1024 * 1024
            && x.MaxCsvRecordCharacters is > 0 and <= 8 * 1024 * 1024, "Invalid resource limits");
    }

    internal static void UniqueJson(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                Require(keys.Add(property.Name), "Duplicate JSON property: " + property.Name);
                UniqueJson(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) UniqueJson(item);
    }

    private static void ExactKeys(JsonElement element, params string[] expected)
    {
        Require(element.ValueKind == JsonValueKind.Object, "Expected contract object");
        var actual = element.EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        Require(actual.SetEquals(expected), "Unknown or missing frozen schema1 contract key");
    }

    internal static void ValidateExtractionFlags(JsonElement flags)
    {
        foreach (string key in new[] { "activation_ready", "fixture_verified", "replacement_decoding_used" }) False(flags, key);
        foreach (string key in new[] { "duplicates_overwritten", "defaults_substituted" }) Count(flags, key, 0);
        foreach (string key in new[] { "every_target_has_coverage_row", "profile_ids_exactly_supported_and_identity_matched", "every_source_completed_strict_parse_or_was_hashed_input" })
            Require(flags.GetProperty(key).ValueKind == JsonValueKind.True, "Incomplete extraction evidence: " + key);
    }

    internal static Dictionary<string, RetailNpcManifestSource> SourceReceipts(JsonElement sources)
    {
        Require(sources.ValueKind == JsonValueKind.Array, "Source manifest must be an array");
        var receipts = new Dictionary<string, RetailNpcManifestSource>(StringComparer.Ordinal);
        foreach (var source in sources.EnumerateArray())
        {
            string path = String(source, "path");
            string sha = String(source, "sha256"); Hash(sha);
            long length = source.GetProperty("bytes").GetInt64();
            Require(path.Length > 0 && length >= 0 && receipts.TryAdd(path, new(length, sha)), "Duplicate or invalid source receipt");
        }
        return receipts; // Provenance paths deliberately remain opaque, never filesystem inputs.
    }

    internal static Dictionary<int, CoverageRow> Coverage(FileStream stream, RetailNpcDatasetLimits limits, CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, CoverageRow>();
        foreach (var values in Csv(stream, CoverageHeaders, limits, cancellationToken))
        {
            Require(result.Count < limits.MaxCoverageRows, "Coverage row bound exceeded");
            int id = Positive(values[0]);
            Require(values[3] is "exported_static_source_only" or "devname_changed_or_ambiguous_blocked" or "source_absent_blocked", "Unknown identity coverage status");
            Require(values[4].Length > 0, "Coverage family is missing");
            FalseCsv(values[8]); FalseCsv(values[9]);
            Require(result.TryAdd(id, new(id, values[1], values[2], values[3], values[5], values[6], values[7], values)), "Duplicate coverage NPC ID: " + id);
        }
        return result;
    }

    internal static void IdentityBlockers(FileStream stream, Dictionary<int, CoverageRow> coverage, JsonElement summary, RetailNpcDatasetLimits limits, CancellationToken cancellationToken)
    {
        var seen = new HashSet<int>();
        foreach (var values in Csv(stream, CoverageHeaders, limits, cancellationToken))
        {
            int id = Positive(values[0]);
            Require(seen.Count < limits.MaxCoverageRows && seen.Add(id) && coverage.TryGetValue(id, out var original)
                && !original.Exported && values.SequenceEqual(original.Values, StringComparer.Ordinal), "Invalid, duplicate or mismatched identity blocker: " + id);
        }
        Require(seen.SetEquals(coverage.Values.Where(x => !x.Exported).Select(x => x.Id)), "Identity blockers do not cover every blocked identity");
        Count(summary, "identity_blockers", seen.Count);
        var counts = coverage.Values.GroupBy(x => x.Status).ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
        var declared = summary.GetProperty("coverage_status_counts").EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetInt32(), StringComparer.Ordinal);
        Require(counts.Count == declared.Count && counts.All(x => declared.GetValueOrDefault(x.Key) == x.Value), "Coverage status counts disagree");
    }

    internal static int Profile(JsonElement profile, string datasetId, Dictionary<int, CoverageRow> coverage, HashSet<string> selected,
        Dictionary<string, RetailNpcManifestSource> sources, Dictionary<int, string> differences, Dictionary<SkillIssue, int> issues,
        RetailNpcReviewedIdentityBindings? reviewedBindings, HashSet<int> reviewedIds, out RetailNpcReviewedIdentityReceipt? identityReceipt)
    {
        identityReceipt = null;
        UniqueJson(profile);
        ExactKeys(profile, "schema_version", "dataset_id", "npc_id", "devname", "binding48", "source", "raw_record",
            "scalar_fields", "ordered_lists", "skill_bindings", "aggregate_crosscheck", "blockers", "verification");
        Require(profile.GetProperty("schema_version").GetInt32() == 1 && String(profile, "dataset_id") == datasetId, "Incompatible profile schema/dataset");
        int id = profile.GetProperty("npc_id").GetInt32();
        Require(coverage.TryGetValue(id, out var row) && row.Exported, "Profile has no exported identity coverage: " + id);
        string name = String(profile, "devname");
        Require(name.Length > 0 && row.RetailDevname == name, "Profile retail identity differs from coverage: " + id);
        var verify = profile.GetProperty("verification");
        ExactKeys(verify, "stage", "fixture_verified", "activation_ready");
        Require(String(verify, "stage") == "static_source_extracted", "Profile is not source extraction evidence");
        False(verify, "fixture_verified"); False(verify, "activation_ready");
        var binding = profile.GetProperty("binding48");
        var names = binding.GetProperty("devnames48").EnumerateArray().Select(x => x.GetString()).ToArray();
        Require(names.Length == 1 && !string.IsNullOrEmpty(names[0]) && names[0] == row.Devnames48
            && Positive(String(binding.GetProperty("attrs48"), "npc_id")) == id, "Independent4.8 identity binding differs");
        string clientName = names[0]!;
        bool reviewed = clientName != name;
        bool hasReviewedMetadata = binding.TryGetProperty("reviewed_reference_drift", out var reviewedMetadata);
        Require(hasReviewedMetadata == reviewed, "Reviewed identity metadata is missing or attached to an exact-name row");
        Require(!reviewed || reviewedBindings is not null && reviewedBindings.Covers(id, clientName, name), "Unreviewed changed-name identity: " + id);
        var source = profile.GetProperty("source");
        string path = String(source, "path");
        sources.TryGetValue(path, out var sourceReceipt);
        Require(selected.Contains(path) && sourceReceipt is not null && row.SourcePath == path, "Profile source is not selected divided provenance");
        long length = sourceReceipt.Bytes;
        long start = source.GetProperty("byte_start").GetInt64(), end = source.GetProperty("byte_end_exclusive").GetInt64();
        Require(start >= 0 && end > start && end <= length && source.GetProperty("record_index").GetInt32() >= 0
            && source.GetProperty("line").GetInt32() > 0 && row.SourceLine == source.GetProperty("line").GetInt32().ToString(CultureInfo.InvariantCulture), "Invalid raw source span/index/line");
        Hash(String(source, "raw_sha256")); Hash(String(source, "semantic_sha256"));
        var raw = profile.GetProperty("raw_record");
        RawNode(raw);
        Require(raw[0].GetString() == "npc" && RawIdentity(raw, "id") == id.ToString(CultureInfo.InvariantCulture) && RawIdentity(raw, "name") == name, "Raw record identity differs");
        if (reviewed)
        {
            Require(reviewedBindings is not null, "Explicit admitted review catalog is required");
            identityReceipt = reviewedBindings.Bind(id, clientName, name, String(binding.GetProperty("attrs48"), "level"),
                reviewedMetadata, raw, source, sourceReceipt);
            Require(reviewedIds.Add(id), "Duplicate reviewed retained ID: " + id);
        }
        foreach (var field in profile.GetProperty("scalar_fields").EnumerateObject()) ValidateField(field.Name, field.Value, raw);
        foreach (var group in profile.GetProperty("ordered_lists").EnumerateObject())
        {
            var groups = RetailNpcSourceProfile.RawChildren(raw).Where(x => x[0].GetString() == group.Name).ToArray();
            string presence = groups.Length == 0 ? "absent" : groups.Length == 1 ? "explicit_value" : "ambiguous_duplicate";
            Require(String(group.Value, "presence") == presence, "Ordered group presence differs: " + group.Name);
            var entries = group.Value.GetProperty("entries").EnumerateArray().ToArray();
            var expected = groups.SelectMany((parent, gi) => RetailNpcSourceProfile.RawChildren(parent).Select((node, ei) => (Node: node, Group: gi, Entry: ei))).ToArray();
            Require(entries.Length == expected.Length, "Ordered group count differs: " + group.Name);
            for (int i = 0; i < entries.Length; i++)
            {
                Require(entries[i].GetProperty("source_group_index").GetInt32() == expected[i].Group
                    && entries[i].GetProperty("source_entry_index").GetInt32() == expected[i].Entry
                    && String(entries[i], "tag") == expected[i].Node[0].GetString(), "Ordered group index/tag differs: " + group.Name);
                foreach (var field in entries[i].GetProperty("projected_fields").EnumerateObject()) ValidateField(field.Name, field.Value, expected[i].Node);
            }
        }
        var blockers = profile.GetProperty("blockers").EnumerateArray().Select(x => x.GetString()!).ToArray();
        Require(blockers.All(x => !string.IsNullOrEmpty(x)) && blockers.Length == blockers.Distinct(StringComparer.Ordinal).Count()
            && blockers.Contains("effective_runtime_profile_not_verified", StringComparer.Ordinal), "Missing or duplicate source verification blockers");
        string aggregate = differences.GetValueOrDefault(id, "equal");
        Require(row.AggregateStatus == aggregate && String(profile.GetProperty("aggregate_crosscheck"), "status") == aggregate, "Aggregate status differs");
        Require(!differences.ContainsKey(id) || blockers.Contains("aggregate_semantic_conflict_requires_review", StringComparer.Ordinal), "Missing aggregate conflict blocker");
        var skillRows = profile.GetProperty("skill_bindings").EnumerateArray().ToArray();
        var rawSkills = RetailNpcSourceProfile.RawChildren(raw).Where(x => x[0].GetString() == "skills")
            .SelectMany((parent, gi) => RetailNpcSourceProfile.RawChildren(parent).Select((node, ei) => (Node: node, Group: gi, Entry: ei))).ToArray();
        Require(skillRows.Length == rawSkills.Length, "Skill binding count differs");
        for (int i = 0; i < skillRows.Length; i++)
        {
            var skill = skillRows[i];
            False(skill, "effect_semantics_verified");
            Require(skill.GetProperty("source_group_index").GetInt32() == rawSkills[i].Group && skill.GetProperty("source_entry_index").GetInt32() == rawSkills[i].Entry, "Skill source order differs");
            string status = String(skill, "status");
            string skillName = skill.TryGetProperty("name", out var n) ? n.GetString()! : "";
            var rawNames = RetailNpcSourceProfile.RawChildren(rawSkills[i].Node).Where(x => x[0].GetString() == "skill_name").ToArray();
            Require(rawNames.Length != 1 || skillName == RetailNpcSourceProfile.RawText(rawNames[0]).Trim(), "Skill binding name differs from source");
            string retailIds = IdList(skill, "retail_name_id_candidates"), stackIds = IdList(skill, "supported48_stack_id_candidates");
            if (status == "dual_join_agrees_static_only")
            {
                string chosen = skill.GetProperty("supported48_id").GetInt32().ToString(CultureInfo.InvariantCulture);
                Require(retailIds == chosen && stackIds == chosen, "Agreed skill binding candidates disagree");
            }
            else
            {
                Require(skill.GetProperty("supported48_id").ValueKind == JsonValueKind.Null && blockers.Contains("unresolved_skill_identity_binding", StringComparer.Ordinal), "Unresolved skill binding lost its blocker");
                var issue = new SkillIssue(id, rawSkills[i].Entry, skillName, status, retailIds, stackIds);
                issues[issue] = issues.GetValueOrDefault(issue) + 1;
            }
        }
        return id;
    }

    private static void ValidateField(string tag, JsonElement field, JsonElement rawParent)
    {
        False(field, "default_applied");
        Require(String(field, "projection").Length > 0, "Projection name is missing");
        Require(field.GetProperty("raw_values").EnumerateArray().All(x => x.ValueKind == JsonValueKind.String), "Raw field values must be strings");
        var parsed = RetailNpcSourceProfile.Field(field);
        var expected = RetailNpcSourceProfile.RawChildren(rawParent).Where(x => x[0].GetString() == tag).Select(RetailNpcSourceProfile.RawText).ToImmutableArray();
        Require(expected.SequenceEqual(parsed.RawValues, StringComparer.Ordinal) && parsed.Presence == RetailNpcSourceProfile.Presence(expected, parsed.Projection != "string"), "Projected field presence/value differs: " + tag);
    }

    private static void RawNode(JsonElement node)
    {
        Require(node.ValueKind == JsonValueKind.Array && node.GetArrayLength() == 3 && node[0].ValueKind == JsonValueKind.String
            && !string.IsNullOrEmpty(node[0].GetString()) && node[1].ValueKind == JsonValueKind.Array && node[2].ValueKind == JsonValueKind.Array, "Malformed ordered raw node");
        var attrs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attr in node[1].EnumerateArray())
            Require(attr.ValueKind == JsonValueKind.Array && attr.GetArrayLength() == 2 && attr[0].ValueKind == JsonValueKind.String
                && attr[1].ValueKind == JsonValueKind.String && attrs.Add(attr[0].GetString()!), "Malformed/duplicate raw XML attribute");
        foreach (var child in node[2].EnumerateArray())
        { if (child.ValueKind != JsonValueKind.String) RawNode(child); }
    }

    private static string RawIdentity(JsonElement raw, string tag)
    {
        var fields = RetailNpcSourceProfile.RawChildren(raw).Where(x => x[0].GetString() == tag).ToArray();
        Require(fields.Length == 1 && !RetailNpcSourceProfile.RawChildren(fields[0]).Any(), "Missing/duplicate/nested raw identity: " + tag);
        return RetailNpcSourceProfile.RawText(fields[0]).Trim();
    }

    private static string IdList(JsonElement binding, string key) => !binding.TryGetProperty(key, out var ids) ? "" : string.Join(";", ids.EnumerateArray().Select(x =>
    { int id = x.GetInt32(); Require(id > 0, "Invalid skill ID candidate"); return id.ToString(CultureInfo.InvariantCulture); }));

    internal static void SkillIssues(FileStream stream, Dictionary<SkillIssue, int> issues, JsonElement summary, RetailNpcDatasetLimits limits, CancellationToken cancellationToken)
    {
        int count = 0;
        foreach (var row in Csv(stream, SkillHeaders, limits, cancellationToken))
        {
            var key = new SkillIssue(Positive(row[0]), Nonnegative(row[1]), row[2], row[3], row[4], row[5]);
            Require(issues.TryGetValue(key, out int remaining) && remaining > 0, "Unknown/duplicate skill blocker row");
            issues[key] = remaining - 1;
            Require(++count <= limits.MaxCoverageRows, "Skill blocker row bound exceeded");
        }
        Require(issues.Values.All(x => x == 0), "Skill blockers do not cover every unresolved binding");
        Count(summary, "unsupported_skill_binding_rows", count);
    }

    private static int Positive(string text) { int result = Nonnegative(text); Require(result > 0, "Expected positive ID"); return result; }
    private static int Nonnegative(string text)
    { Require(int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value >= 0, "Expected nonnegative integer"); return value; }
    private static void FalseCsv(string value) => Require(bool.TryParse(value, out bool flag) && !flag, "CSV verification flag must be false");

    private static IEnumerable<string[]> Csv(FileStream stream, string[] headers, RetailNpcDatasetLimits limits, CancellationToken cancellationToken)
    {
        Require(stream.Length <= limits.MaxMetadataFileBytes, "CSV artifact exceeds metadata bounds");
        stream.Position = 0;
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false, bufferSize: 64 * 1024, leaveOpen: true);
        bool first = true;
        foreach (var row in CsvRows(reader, limits.MaxCsvRecordCharacters))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (first) { Require(row.SequenceEqual(headers, StringComparer.Ordinal), "Unexpected CSV header"); first = false; continue; }
            Require(row.Length == headers.Length, "Malformed CSV row width");
            yield return row;
        }
        Require(!first, "Missing CSV header");
    }

    private static IEnumerable<string[]> CsvRows(TextReader reader, int limit)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        bool quoted = false, closed = false, started = false;
        int characters = 0, value;
        while ((value = reader.Read()) >= 0)
        {
            Require(++characters <= limit, "CSV record exceeds character bounds");
            char c = (char)value;
            if (quoted)
            {
                if (c == '"')
                {
                    if (reader.Peek() == '"') { reader.Read(); field.Append('"'); Require(++characters <= limit, "CSV record exceeds bounds"); }
                    else { quoted = false; closed = true; }
                }
                else field.Append(c);
                continue;
            }
            Require(!closed || c is ',' or '\r' or '\n', "Invalid text after CSV quote");
            if (c == '"') { Require(!started, "Quote inside unquoted CSV field"); quoted = true; started = true; }
            else if (c == ',') { fields.Add(field.ToString()); field.Clear(); closed = started = false; }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && reader.Peek() == '\n') reader.Read();
                fields.Add(field.ToString()); yield return fields.ToArray();
                fields.Clear(); field.Clear(); quoted = closed = started = false; characters = 0;
            }
            else { field.Append(c); started = true; }
        }
        Require(!quoted, "Unterminated CSV quote");
        if (started || closed || fields.Count > 0) { fields.Add(field.ToString()); yield return fields.ToArray(); }
    }
}
