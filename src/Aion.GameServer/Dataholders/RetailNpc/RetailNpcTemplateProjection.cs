using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aion.GameServer.Dataholders.RetailNpc;

/// <summary>
/// Inactive schema1 source projection, after RetailNpcDatasetReader admission. Java NpcData/
/// NpcTemplate/StatsTemplate were read first: their zero refill and generic stats are deliberately
/// not invoked. Field/source evidence and unresolved initialization contracts are recorded in
/// implementation/phase2/effective-profile-consumers/. No file, actor, effect or clock access.
/// </summary>
public static class RetailNpcTemplateProjection
{
    public const string MainBinarySha256 = "da1ee5633d65a7ebb436b29d15ca24e46ab4f267449384d177bf20d2d26eb8f1";
    public const string NpcBinarySha256 = "c24e600107352268d377c9a7361e52d848e96af8deff61259e031b98b7130350";

    // This is a supported strict lexical domain, not a claim to emulate native _wtoi on
    // malformed/overflow input. Native defaults, unsigned views and narrowing remain separate.
    public static ImmutableArray<string> IntegerFields { get; } = [
        "level", "max_hp", "hp_regen", "max_mp", "mp_regen", "physical_defend", "magical_defend",
        "magical_resist", "min_damage", "max_damage", "hit_count", "attack_delay", "hit_accuracy",
        "magical_hit_accuracy", "dodge", "parry", "block", "critical", "magical_critical", "concentration",
        "magical_skill_boost", "magical_skill_boost_resist", "physical_critical_reduce_rate",
        "physical_critical_damage_reduce", "magical_critical_reduce_rate", "magical_critical_damage_reduce",
        "proc_reduce_rate", "stat_ratio", "scale", "dual"
    ];
    public static ImmutableArray<string> SpeedFields { get; } = ["move_speed_normal_walk", "move_speed_normal_run", "move_speed_combat_run"];
    public static ImmutableArray<string> PrimaryFields { get; } = ["str", "vit", "agi", "dex", "kno", "will"];
    public static ImmutableArray<string> ElementFields { get; } = [
        "elemental_defend_water", "elemental_defend_air", "elemental_defend_earth",
        "elemental_defend_fire", "elemental_defend_light", "elemental_defend_dark"
    ];
    private static readonly ImmutableArray<string> DetailFiles = ["npcs_npcs.xml", "npcs_monsters.xml", "npcs_std_monsters.xml",
        "npcs_abyss_monsters.xml", "npcs_abyss_std_monsters.xml", "npcs_test.xml", "npcs_housing.xml"];
    private static readonly Regex IntegerLexeme = new(@"^[+-]?[0-9]+$", RegexOptions.CultureInvariant);
    private static readonly Regex FloatLexeme = new(@"^[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant);

    public static RetailNpcTemplateProjectionResult Project(RetailNpcStaticDataset dataset, int npcId,
        RetailNpcTemplateProjectionPolicy policy)
        => ProjectCore(dataset, npcId, policy, includeSkillBindings: true);

    /// <summary>Source-stat projection only. Complete raw skill records remain in
    /// Source/RawRecord, but no skill binding or behavior is admitted. Missing 4.8
    /// skill identities cannot invalidate unrelated HP/defense/normal bases.</summary>
    public static RetailNpcTemplateProjectionResult ProjectStatistics(RetailNpcStaticDataset dataset, int npcId,
        RetailNpcTemplateProjectionPolicy policy)
        => ProjectCore(dataset, npcId, policy, includeSkillBindings: false);

    private static RetailNpcTemplateProjectionResult ProjectCore(RetailNpcStaticDataset dataset, int npcId,
        RetailNpcTemplateProjectionPolicy policy, bool includeSkillBindings)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(policy);
        try
        {
            var receipt = Receipt(dataset, npcId, policy);
            var source = dataset.Profiles[npcId];
            var context = new Context(policy.MaxOrderedEntries);
            var root = source.RawRecord;
            Require(root[0].GetString() == "npc", "source_identity", "/npc", "Expected original npc node");
            var sourceId = context.Int(root, "id", "/npc/id");
            var sourceName = context.Token(root, "name", "/npc/name");
            Require(sourceId.ParsedValue == npcId && sourceName.Value == source.Devname,
                "source_identity", "/npc", "Raw identity differs from admitted NPC identity");

            var integers = IntegerFields.ToDictionary(x => x, x => context.Int(root, x, "/npc/" + x), StringComparer.Ordinal);
            var speeds = SpeedFields.ToDictionary(x => x, x => context.Float(root, x, "/npc/" + x), StringComparer.Ordinal);
            var first = context.Group(root, "first_level_attr");
            var primaries = PrimaryFields.ToDictionary(x => x, x => context.Word(first, x, "/npc/first_level_attr/" + x), StringComparer.Ordinal);
            var elements = ElementFields.ToDictionary(x => x, x => context.SignedWord(first, x, "/npc/first_level_attr/" + x), StringComparer.Ordinal);
            var bound = context.Group(root, "bound_radius");
            var axes = new[] { "front", "side", "upper" }.ToDictionary(x => x, x => context.Float(bound, x, "/npc/bound_radius/" + x), StringComparer.Ordinal);

            var attackGroup = context.Group(root, "attacks");
            var attacks = context.Entries(attackGroup, "attacks").Select((entry, index) =>
            {
                string path = "/npc/attacks/data[" + index + "]/";
                return new RetailNpcAttackTemplateInputs(index, context.Int(entry, "attack_rate", path + "attack_rate"),
                    context.Int(entry, "attack_mod", path + "attack_mod"), context.Float(entry, "attack_range", path + "attack_range"),
                    context.Token(entry, "attack_type", path + "attack_type"), entry);
            }).ToImmutableArray();

            var skillGroup = context.Group(root, "skills");
            var skillEntries = context.Entries(skillGroup, "skills").ToArray();
            var bindings = includeSkillBindings ? SkillBindings(source, skillEntries.Length) : null;
            var skills = (includeSkillBindings ? skillEntries : []).Select((entry, index) =>
            {
                var name = context.Token(entry, "skill_name", "/npc/skills/data[" + index + "]/skill_name");
                var binding = bindings![index];
                Require(name.Value == binding.GetProperty("name").GetString(), "skill_identity", name.RecordPath, "Skill binding and ordered source name differ");
                return new RetailNpcSkillTemplateInputs(index, name, binding.GetProperty("supported48_id").GetInt32(), binding, entry);
            }).ToImmutableArray();

            var resistGroup = context.Group(root, "abnormal_resists");
            var penetrationGroup = context.Group(root, "abnormal_penetrations");
            var resists = Abnormals(context, resistGroup, "abnormal_resists", "abnormal_resist_name", "abnormal_resist_value");
            var penetrations = Abnormals(context, penetrationGroup, "abnormal_penetrations", "abnormal_penetration_name", "abnormal_penetration_value");
            if (context.Issues.Count != 0) return new(null, context.Issues.ToImmutableArray());
            var inputs = new RetailNpcTemplateInputs(source, receipt, integers, speeds,
                new(Presence(first), primaries, elements, first), new(Presence(bound), axes, bound),
                new(Presence(attackGroup), attacks, attackGroup), new(Presence(skillGroup), skills, skillGroup),
                new(Presence(resistGroup), resists, resistGroup), new(Presence(penetrationGroup), penetrations, penetrationGroup));
            return new(inputs, []);
        }
        catch (ProjectionError error) { return new(null, [new(error.Code, error.RecordPath, error.Message)]); }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or KeyNotFoundException
            or FormatException or OverflowException or IndexOutOfRangeException or ArgumentException)
        { return new(null, [new("source_contract", "/npc", error.Message)]); }
    }

    private static RetailNpcSourcePresence Presence(JsonElement? group) => group.HasValue ? RetailNpcSourcePresence.ExplicitValue : RetailNpcSourcePresence.Absent;

    private static ImmutableArray<RetailNpcAbnormalTemplateInputs> Abnormals(Context context, JsonElement? group,
        string tag, string nameTag, string valueTag) => context.Entries(group, tag).Select((entry, index) =>
        {
            string path = "/npc/" + tag + "/data[" + index + "]/";
            return new RetailNpcAbnormalTemplateInputs(index, context.Token(entry, nameTag, path + nameTag),
                context.Word(entry, valueTag, path + valueTag), entry);
        }).ToImmutableArray();

    private static Dictionary<int, JsonElement> SkillBindings(RetailNpcSourceProfile source, int count)
    {
        var result = new Dictionary<int, JsonElement>();
        foreach (var binding in source.SkillBindings.EnumerateArray())
        {
            int index = binding.GetProperty("source_entry_index").GetInt32();
            Require(binding.GetProperty("source_group_index").GetInt32() == 0 && index >= 0 && index < count,
                "skill_identity", "/npc/skills", "Skill binding points outside the one admitted ordered source group");
            Require(result.TryAdd(index, binding), "duplicate_skill_binding", "/npc/skills", "Duplicate source-entry binding");
            Require(binding.GetProperty("status").GetString() == "dual_join_agrees_static_only"
                && binding.GetProperty("supported48_id").GetInt32() > 0
                && binding.GetProperty("effect_semantics_verified").ValueKind == JsonValueKind.False,
                "skill_identity", "/npc/skills", "A required binding is incompatible or claims unverified effect semantics");
            int id = binding.GetProperty("supported48_id").GetInt32();
            Require(SingleId(binding.GetProperty("retail_name_id_candidates"), id)
                && SingleId(binding.GetProperty("supported48_stack_id_candidates"), id),
                "skill_identity", "/npc/skills", "Required retail and supported skill identities disagree");
        }
        Require(result.Count == count, "missing_skill_binding", "/npc/skills", "Every source skill entry needs its own identity binding");
        return result;
    }

    private static bool SingleId(JsonElement value, int id) => value.ValueKind == JsonValueKind.Array
        && value.GetArrayLength() == 1 && value[0].GetInt32() == id;

    private static RetailNpcTemplateSourceReceipt Receipt(RetailNpcStaticDataset dataset, int npcId, RetailNpcTemplateProjectionPolicy policy)
    {
        RetailNpcDatasetValidation.Hash(policy.ExpectedDatasetId);
        RetailNpcDatasetValidation.Hash(policy.ExpectedOutputManifestSha256);
        Require(!string.IsNullOrWhiteSpace(policy.ExpectedRegion) && policy.MaxOrderedEntries is > 0 and <= 65_536,
            "projection_policy", "/", "Explicit region and bounded entry limit required");
        Require(dataset.DatasetId == policy.ExpectedDatasetId && dataset.OutputManifestSha256 == policy.ExpectedOutputManifestSha256,
            "dataset_identity", "/", "Caller-pinned dataset and manifest differ");
        Require(npcId > 0 && dataset.Profiles.ContainsKey(npcId), "missing_profile", "/npc", "Required NPC is not retained in the admitted cohort");
        var source = dataset.Profiles[npcId];
        Require(source.DatasetId == dataset.DatasetId && source.NpcId == npcId && !string.IsNullOrWhiteSpace(source.Devname),
            "source_identity", "/npc", "Source identity differs from dataset cohort");
        RetailNpcDatasetValidation.UniqueJson(dataset.Provenance);
        RetailNpcDatasetValidation.UniqueJson(dataset.SourceManifest);
        RetailNpcDatasetValidation.UniqueJson(source.CompleteSourceProfile);
        var builds = dataset.Provenance.GetProperty("consumer_registry").GetProperty("builds");
        Require(Text(builds.GetProperty("main"), "sha256") == MainBinarySha256
            && Text(builds.GetProperty("npc"), "sha256") == NpcBinarySha256,
            "unsupported_binary", "/provenance", "Typed source consumer revision requires the pinned Main/NPC identities");
        var raw = source.SourceProvenance;
        string path = Text(raw, "path");
        var selected = dataset.Provenance.GetProperty("selection").EnumerateArray()
            .Where(x => Text(x, "selected") == path).ToArray();
        Require(selected.Length == 1, "selected_detail_receipt", "/source", "Source must occur exactly once in the selected divided-file receipt");
        string region = Text(selected[0], "region");
        string filename = Text(selected[0], "filename");
        string reason = Text(selected[0], "reason");
        Require(region == policy.ExpectedRegion && DetailFiles.Contains(filename, StringComparer.Ordinal) && !string.IsNullOrWhiteSpace(reason),
            "selected_detail_receipt", "/source", "Selected region/file is incompatible with the caller's source context");
        var files = dataset.SourceManifest.EnumerateArray().Where(x => Text(x, "path") == path).ToArray();
        Require(files.Length == 1, "selected_detail_receipt", "/source", "Selected source needs one complete-file hash receipt");
        var file = files[0];
        Require(file.GetProperty("record_tag").GetString() == "npc" && file.GetProperty("strict_xml_complete").ValueKind == JsonValueKind.True,
            "selected_detail_receipt", "/source", "Source is not a completely parsed divided NPC input");
        long bytes = file.GetProperty("bytes").GetInt64();
        long start = raw.GetProperty("byte_start").GetInt64(), end = raw.GetProperty("byte_end_exclusive").GetInt64();
        int line = raw.GetProperty("line").GetInt32(), index = raw.GetProperty("record_index").GetInt32();
        Require(bytes > 0 && start >= 0 && end > start && end <= bytes && line > 0 && index >= 0
            && index < file.GetProperty("record_count").GetInt32(), "source_span", "/source", "Raw span/index exceeds the pinned source file");
        string rawHash = Text(raw, "raw_sha256"), semanticHash = Text(raw, "semantic_sha256");
        string fileHash = Text(file, "sha256");
        RetailNpcDatasetValidation.Hash(rawHash); RetailNpcDatasetValidation.Hash(semanticHash); RetailNpcDatasetValidation.Hash(fileHash);
        return new(dataset.DatasetId, dataset.OutputManifestSha256, npcId, source.Devname, region, reason, filename,
            path, bytes, fileHash, start, end, line, index, rawHash, semanticHash, MainBinarySha256, NpcBinarySha256);
    }

    private static void Require(bool condition, string code, string path, string message)
    { if (!condition) throw new ProjectionError(code, path, message); }

    private static string Text(JsonElement parent, string name)
    {
        var value = parent.GetProperty(name);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new ProjectionError("source_contract", "/metadata/" + name, "Expected one nonempty metadata string");
        return value.GetString() ?? throw new ProjectionError("source_contract", "/metadata/" + name, "Null metadata string");
    }

    private sealed class ProjectionError(string code, string recordPath, string message) : Exception(message)
    { internal string Code { get; } = code; internal string RecordPath { get; } = recordPath; }

    private sealed class Context(int maxEntries)
    {
        internal List<RetailNpcProjectionIssue> Issues { get; } = [];
        private int orderedEntries;

        internal JsonElement? Group(JsonElement root, string tag)
        {
            var groups = Children(root).Where(x => x[0].GetString() == tag).ToArray();
            if (groups.Length > 1) { Issues.Add(new("duplicate_group", "/npc/" + tag, "Multiple known containers cannot be merged or first-selected")); return null; }
            return groups.Length == 0 ? null : groups[0];
        }

        internal IEnumerable<JsonElement> Entries(JsonElement? group, string tag)
        {
            if (!group.HasValue) yield break;
            foreach (var node in Children(group.Value))
            {
                if (++orderedEntries > maxEntries) throw new ProjectionError("entry_limit", "/npc/" + tag, "Ordered source-entry retention limit exceeded");
                if (node[0].GetString() != "data")
                { Issues.Add(new("unsupported_ordered_entry", "/npc/" + tag, "An ordered entry is not a data node; the source was retained but projection is blocked")); continue; }
                yield return node;
            }
        }

        internal RetailNpcSourceScalar<int> Int(JsonElement? parent, string tag, string path) => Scalar(parent, tag, path,
            s => IntegerLexeme.IsMatch(s) && int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _),
            s => int.Parse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture), "int32_lexeme");
        internal RetailNpcSourceScalar<ushort> Word(JsonElement? parent, string tag, string path) => Scalar(parent, tag, path,
            s => IntegerLexeme.IsMatch(s) && ushort.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _),
            s => ushort.Parse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture), "uint16_lexeme");
        internal RetailNpcSourceScalar<short> SignedWord(JsonElement? parent, string tag, string path) => Scalar(parent, tag, path,
            s => IntegerLexeme.IsMatch(s) && short.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _),
            s => short.Parse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture), "int16_lexeme");
        internal RetailNpcSourceScalar<float> Float(JsonElement? parent, string tag, string path) => Scalar(parent, tag, path,
            s => FloatLexeme.IsMatch(s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                && double.IsFinite(value) && float.IsFinite((float)value),
            s => (float)double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture), "finite_binary32_lexeme");

        private RetailNpcSourceScalar<T> Scalar<T>(JsonElement? parent, string tag, string path,
            Func<string, bool> valid, Func<string, T> parse, string domain) where T : struct
        {
            var nodes = Find(parent, tag);
            var values = nodes.Select(RetailNpcSourceProfile.RawText).ToImmutableArray();
            var presence = RetailNpcSourceProfile.Presence(values, numeric: true);
            if (nodes.Length == 0) return new(presence, values, null, path);
            if (nodes.Length > 1) { Issues.Add(new("duplicate_field", path, "Multiple scalar definitions cannot overwrite one another")); return new(presence, values, null, path); }
            string input = values[0].Trim();
            if (Children(nodes[0]).Any() || !valid(input))
            { Issues.Add(new("invalid_" + domain, path, "Explicit source is outside the supported strict lexical width/shape")); return new(presence, values, null, path); }
            return new(presence, values, parse(input), path);
        }

        internal RetailNpcSourceToken Token(JsonElement? parent, string tag, string path)
        {
            var nodes = Find(parent, tag);
            var values = nodes.Select(RetailNpcSourceProfile.RawText).ToImmutableArray();
            var presence = RetailNpcSourceProfile.Presence(values, numeric: false);
            if (nodes.Length == 0) return new(presence, values, null, path);
            if (nodes.Length > 1 || Children(nodes[0]).Any() || string.IsNullOrWhiteSpace(values[0]))
            { Issues.Add(new(nodes.Length > 1 ? "duplicate_field" : "invalid_token", path, "An explicit token must have one nonempty scalar definition")); return new(presence, values, null, path); }
            return new(presence, values, values[0], path);
        }

        private static JsonElement[] Find(JsonElement? parent, string tag) => parent.HasValue
            ? Children(parent.Value).Where(x => x[0].GetString() == tag).ToArray() : [];
        private static IEnumerable<JsonElement> Children(JsonElement node) => RetailNpcSourceProfile.RawChildren(node);
    }
}
