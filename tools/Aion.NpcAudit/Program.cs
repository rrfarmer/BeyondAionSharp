using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.RetailNpc;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Retail;
using Aion.GameServer.Model;
using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.NpcAudit;

/// <summary>Offline calculator bridge. The Python front end admits/indexes extractor artifacts.
/// Only detached templates and immutable calculations are used: no bootstrap, actors or catalog activation.</summary>
internal static partial class Program
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static object Issue(string code, string message) => new { code, message };

    static partial void AdditionalCommand(string[] args, ref int? result);

    private static int Main(string[] args)
    {
        try
        {
            int? additionalResult = null;
            AdditionalCommand(args, ref additionalResult);
            if (additionalResult.HasValue) return additionalResult.Value;
            if(args.Length==5&&args[0]=="--stats-package")
                return StatsPackage(args[1],args[2],args[3],args[4]);
            if (args.Length != 2 || args[0] != "--request")
                throw new ArgumentException("Use the npc_audit.py front end; calculator syntax: --request <admitted-request.json>");
            if (new FileInfo(args[1]).Length > 64 * 1024 * 1024) throw new InvalidDataException("Request exceeds64MB");
            using var doc = JsonDocument.Parse(File.ReadAllBytes(args[1]), new() { MaxDepth = 384 });
            var request = doc.RootElement;
            if (request.GetProperty("protocol_version").GetInt32() != 1 ||
                request.GetProperty("context").GetString() != "fresh-unowned-ordinary-kind1")
                throw new InvalidDataException("Explicit supported calculation context required");
            var ids = request.GetProperty("npcs").EnumerateArray().Select(x => x.GetProperty("npc_id").GetInt32()).ToHashSet();
            if (ids.Count is < 1 or > 1024) throw new InvalidDataException("Select1..1024 unique NPCs per comparison");
            string templates = request.GetProperty("templates48").GetString()!;
            if (Hash(File.ReadAllBytes(templates)) != request.GetProperty("templates48_sha256").GetString())
                throw new InvalidDataException("4.8 template bytes differ from request pin");
            var legacy = Load48(templates, ids);
            var provenance = request.GetProperty("provenance");
            string datasetId = request.GetProperty("dataset_id").GetString()!;
            string manifestSha = request.GetProperty("manifest_sha256").GetString()!;
            string region = request.GetProperty("region").GetString()!;
            byte[] ratioBytes = Convert.FromBase64String(request.GetProperty("ratio_base64").GetString()!);
            string ratioSha = request.GetProperty("ratio_sha256").GetString()!;
            var ratio = RetailNpcLevelRatioTableInputs.Admit(ratioBytes, new(datasetId, manifestSha, region,
                ratioSha, request.GetProperty("ratio_path").GetString()!, request.GetProperty("ratio_selected_relative").GetString()!,
                request.GetProperty("ratio_selection_reason").GetString()!));
            if (!ratio.Success) throw new InvalidDataException("Ratio table: " + string.Join("; ", ratio.Issues.Select(x => x.Message)));
            RetailNpcReviewedIdentityBindings? reviewed = null;
            if (request.TryGetProperty("identity_review_base64", out var reviewBytes) && reviewBytes.ValueKind == JsonValueKind.String)
            {
                reviewed = RetailNpcReviewedIdentityBindings.Admit(Convert.FromBase64String(reviewBytes.GetString()!),
                    request.GetProperty("identity_review_sha256").GetString()!);
                reviewed.ValidateSourceContext(request.GetProperty("source_manifest"), provenance);
            }
            var rows = new List<object>();
            foreach (var row in request.GetProperty("npcs").EnumerateArray())
                rows.Add(Compare(row, legacy.GetValueOrDefault(row.GetProperty("npc_id").GetInt32()), request, ratio.Table!, reviewed));
            if (RetailNpcStatCatalog.Enabled) throw new InvalidOperationException("Offline audit activated a gameplay catalog");
            Console.Write(JsonSerializer.Serialize(new
            {
                protocol_version = 1, context = "fresh-unowned-ordinary-kind1", activation_ready = false,
                statistics_only = request.TryGetProperty("statistics_only", out var statsOnly) && statsOnly.GetBoolean(),
                scope = "Loaded 4.8 template defaults versus calculated fresh/unowned/non-dual 5.8 base profile; no effects, AI modifiers, owner/difficulty scaling, target or final HP-loss prediction. Not a live-server snapshot.",
                calculator = new { assembly_sha256 = Hash(File.ReadAllBytes(typeof(Program).Assembly.Location)),
                    game_server_assembly_sha256 = Hash(File.ReadAllBytes(typeof(NpcData).Assembly.Location)),
                    retail_main_sha256 = RetailNpcTemplateProjection.MainBinarySha256,
                    retail_npc_sha256 = RetailNpcTemplateProjection.NpcBinarySha256 },
                rows
            }, Json));
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e.GetType().Name + ": " + e.Message); return 2; }
    }

    private static Dictionary<int, NpcTemplate> Load48(string path, HashSet<int> wanted)
    {
        var serializer = new XmlSerializer(typeof(NpcTemplate), new XmlRootAttribute("npc_template"));
        var selected = new Dictionary<int, NpcTemplate>();
        using var reader = XmlReader.Create(path, new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.Name != "npc_template") continue;
            int id = int.Parse(reader.GetAttribute("npc_id")!, CultureInfo.InvariantCulture);
            if (!wanted.Contains(id)) continue;
            using var subtree = reader.ReadSubtree();
            var npc = (NpcTemplate)serializer.Deserialize(subtree)!;
            if (!selected.TryAdd(id, npc)) throw new InvalidDataException("Duplicate4.8 NPC template " + id);
        }
        // Exact production zero-fill/rank/level calculations; no reimplementation in Python.
        var holder = new NpcData { npcs = selected.Values.ToList() };
        holder.AfterUnmarshal(null!);
        return selected;
    }

    private static object Compare(JsonElement row, NpcTemplate? old, JsonElement request, RetailNpcLevelRatioTableInputs ratio,
        RetailNpcReviewedIdentityBindings? reviewed)
    {
        int id = row.GetProperty("npc_id").GetInt32();
        var issues = new List<object>(); var fields = new List<object>(); var emitted = new HashSet<string>();
        var baseline = old is null ? new Dictionary<string, object?>() : Baseline(old);
        void Field(string name, object? reference, string? presence = null, string? rule = null)
        {
            emitted.Add(name);
            baseline.TryGetValue(name, out var original);
            bool equal = original is not null && reference is not null && JsonSerializer.Serialize(original) == JsonSerializer.Serialize(reference);
            fields.Add(new { field = name, baseline48 = original, reference58 = reference,
                status = original is null ? "baseline_not_comparable" : reference is null ? "reference_unavailable" : equal ? "matches" : "differs",
                source_presence = presence, rule });
        }
        object? attacks = null, skills = null, sourceReceipt = null, identityBinding = null;
        var metadataLimits = new List<string>();
        bool? normalCorrection = null;
        string calculation = "blocked";
        if (old is null) issues.Add(Issue("unsupported48", "NPC has no supported4.8 template"));
        if (!row.TryGetProperty("profile", out var raw) || raw.ValueKind == JsonValueKind.Null)
            issues.Add(Issue("retail_identity_blocked", row.GetProperty("coverage_status").GetString()!));
        else if (old is not null && old.GetTribe() is TribeClass.PET or TribeClass.PET_DARK
            && !(request.TryGetProperty("statistics_only",out var statPurpose) && statPurpose.GetBoolean()
                && row.TryGetProperty("profile",out var petSource) && petSource.ValueKind==JsonValueKind.Object
                && RetailNpcStatSourceExceptions.IsUnownedRiftGenerator(old,petSource.GetProperty("source").GetProperty("raw_sha256").GetString()!)))
            issues.Add(Issue("owned_family_context_required", "PET/PET_DARK cannot be treated as an unowned ordinary mob"));
        else if (old is not null)
        {
            try
            {
                var names = raw.GetProperty("binding48").GetProperty("devnames48").EnumerateArray().Select(x => x.GetString()).ToArray();
                if (names.Length != 1 || string.IsNullOrEmpty(names[0])) throw new InvalidDataException("Ambiguous/absent4.8 identity");
                RetailNpcReviewedIdentityReceipt? identity = null;
                string retailName = raw.GetProperty("devname").GetString()!;
                if (names[0] != retailName)
                {
                    if (reviewed is null) throw new InvalidDataException("Changed identity requires separately admitted reviewed binding; no alias guessing");
                    var span = raw.GetProperty("source");
                    var file = request.GetProperty("source_manifest").EnumerateArray()
                        .Single(x => x.GetProperty("path").GetString() == span.GetProperty("path").GetString());
                    identity = reviewed.Bind(id, names[0]!, retailName, old.GetLevel().ToString(CultureInfo.InvariantCulture),
                        raw.GetProperty("binding48").GetProperty("reviewed_reference_drift"), raw.GetProperty("raw_record"), span,
                        new(file.GetProperty("bytes").GetInt64(), file.GetProperty("sha256").GetString()!));
                }
                var source = new RetailNpcSourceProfile(raw, identity);
                bool statisticsOnly = request.TryGetProperty("statistics_only", out var stats) && stats.GetBoolean();
                identityBinding = new { client48_devname = source.Client48Devname, retail58_devname = source.Devname,
                    reviewed_same_id = identity is not null };
                if (source.NpcId != id || source.DatasetId != request.GetProperty("dataset_id").GetString())
                    throw new InvalidDataException("Selected profile identity differs");
                var historical = new[] { "effective_runtime_profile_not_verified", "field_units_defaults_and_scaling_not_fully_recovered", "skill_effect_semantics_not_verified" };
                if (statisticsOnly) RetailNpcStatCatalog.ValidateSourceAnnotations(source);
                else if (source.Blockers.Except(historical).Any()) throw new InvalidDataException(string.Join(";", source.Blockers.Except(historical)));
                var dataset = new RetailNpcStaticDataset(source.DatasetId, request.GetProperty("manifest_sha256").GetString()!,
                    new Dictionary<int, RetailNpcSourceProfile> { [id] = source }, 1, 0, raw.GetRawText().Length, [],
                    request.GetProperty("provenance"), request.GetProperty("source_manifest"));
                var policy = new RetailNpcTemplateProjectionPolicy(dataset.DatasetId, dataset.OutputManifestSha256, request.GetProperty("region").GetString()!);
                var projection = statisticsOnly ? RetailNpcTemplateProjection.ProjectStatistics(dataset, id, policy)
                    : RetailNpcTemplateProjection.Project(dataset, id, policy);
                if (!projection.Success) { issues.AddRange(projection.Issues); }
                else
                {
                    var input = projection.Inputs!; sourceReceipt = input.Receipt;
                    bool dual = statisticsOnly && input.Integers["dual"].ParsedValue is not (null or 0);
                    var domain = new RetailNpcOrdinarySeedDomain(input.Receipt, ratio.Receipt.SourceSha256, true, true, 1, 0, 0, false, false, dual)
                        { StatisticsBeforeDualSplit = dual };
                    var core = RetailNpcOrdinaryCoreSeedProjection.ProjectOrdinaryCoreSeeds(input, ratio, domain);
                    if (!core.Success) issues.AddRange(core.Issues);
                    else
                    {
                        var scalar = statisticsOnly ? RetailNpcOrdinaryScalarProjection.ProjectStatistics(input, core.Seeds!, new(domain))
                            : RetailNpcOrdinaryScalarProjection.Project(input, core.Seeds!, new(domain));
                        var bounds = RetailNpcBoundsConstruction.Construct(input, new(input.Receipt, true, true, "offline explicit fresh/unowned ordinary comparison; not a runtime observation"));
                        if (!scalar.Success) issues.AddRange(scalar.Issues);
                        if (!bounds.Success) issues.AddRange(bounds.Issues);
                        if (scalar.Success && bounds.Success && bounds.Plan!.CanSupplySourceDerivedBounds)
                        {
                            var profile = new SkyTempleNpcProfile(input, core.Seeds!, scalar.Inputs!, bounds.Plan, "offline-comparison", statisticsOnly: statisticsOnly);
                            normalCorrection = profile.HasNormalAttackCorrection;
                            attacks = profile.Attacks; skills = profile.Skills;
                            foreach (var (name, value) in scalar.Inputs!.Fields.OrderBy(x => x.Key, StringComparer.Ordinal))
                            {
                                long v = value.Specification.Minimum.HasValue ? Math.Clamp(value.Value, value.Specification.Minimum.Value, value.Specification.Maximum!.Value) : value.Value;
                                Field(name, v, value.SourcePresence.ToString(), "RetailNpcOrdinaryScalarProjection; native view/clamp; before effects");
                            }
                            Field("level", profile.Level, input.Integers["level"].Presence.ToString());
                            Field("normal_min_damage", profile.MinDamage, input.Integers["min_damage"].Presence.ToString(), "RetailNpcOrdinaryCoreSeedProjection; NOT raw physical_attack/magical_attack");
                            Field("normal_max_damage", profile.MaxDamage, input.Integers["max_damage"].Presence.ToString());
                            if(statisticsOnly && input.Integers["attack_delay"].ParsedValue is null)
                            {
                                Field("attack_delay_ms",null,input.Integers["attack_delay"].Presence.ToString(),
                                    "Source delay absent; numeric damage is available, native cadence unverified; statistics integration retains host delay");
                                metadataLimits.Add("attack_delay_absent_native_cadence_unverified");
                            }
                            else Field("attack_delay_ms", profile.AttackDelay, input.Integers["attack_delay"].Presence.ToString());
                            Field("normal_walk_speed", statisticsOnly ? input.Speeds["move_speed_normal_walk"].ParsedValue : profile.NormalWalkSpeed);
                            Field("normal_run_speed", statisticsOnly ? input.Speeds["move_speed_normal_run"].ParsedValue : profile.NormalRunSpeed);
                            Field("combat_run_speed", statisticsOnly ? input.Speeds["move_speed_combat_run"].ParsedValue : profile.CombatRunSpeed);
                            Field("bound_front", profile.Bounds.Front); Field("bound_side", profile.Bounds.Side); Field("bound_upper", profile.Bounds.Upper);
                            Field("horizontal_clearance_radius", profile.Bounds.HorizontalRadius,
                                rule: "RetailNpcBoundsConstruction; native radius is not the4.8 max-axis measurement");
                            Field("normal_attack_range", profile.Attacks.Length == 1 ? profile.Attacks[0].Range : null,
                                rule: "Multiple ordered attacks remain distinct; no first-entry collapse");
                            calculation = "calculated_reference_under_explicit_context";
                        }
                    }
                }
            }
            catch (Exception e) when (e is InvalidDataException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
            { issues.Add(Issue("profile_context_blocked", e.Message)); }
        }
        foreach (var name in baseline.Keys.Except(emitted).Order(StringComparer.Ordinal))
            Field(name, null, rule: "No directly equivalent reference field exported; legacy attack stats are not normal-damage min/max");
        return new { npc_id = id, name48 = old?.GetName(), host_template_sha256 = old is null ? null : RetailNpcHostIdentity.Template(old),
            host_ai = old?.GetAiName(), calculation, normal_attack_correction_supported = normalCorrection,
            baseline48 = baseline, fields, attacks58 = attacks, skills58 = skills,
            source = sourceReceipt, identity_binding = identityBinding, blockers = issues, metadata_limits = metadataLimits, activation_ready = false,
            raw58 = raw.ValueKind == JsonValueKind.Object ? new { scalar_fields = raw.GetProperty("scalar_fields"), ordered_lists = raw.GetProperty("ordered_lists"), skill_bindings = raw.GetProperty("skill_bindings") } : null };
    }

    private static Dictionary<string, object?> Baseline(NpcTemplate npc)
    {
        var s = npc.GetStatsTemplate(); var b = npc.GetBoundRadius();
        return new()
        {
            ["level"] = (int)npc.GetLevel(), ["max_hp"] = s.GetMaxHp(),
            ["physical_defend"] = s.GetPdef(), ["magical_defend"] = s.GetMdef(), ["magical_resist"] = s.GetMresist(),
            ["hit_accuracy"] = s.GetAccuracy(), ["magical_hit_accuracy"] = s.GetMacc(), ["dodge"] = s.GetEvasion(),
            ["parry"] = s.GetParry(), ["block"] = s.GetBlock(), ["critical"] = s.GetPcrit(), ["magical_critical"] = s.GetMcrit(),
            ["magical_skill_boost"] = s.GetMagicBoost(), ["magical_skill_boost_resist"] = s.GetMsup(),
            ["physical_critical_reduce_rate"] = s.GetStrikeResist(), ["magical_critical_reduce_rate"] = s.GetSpellResist(),
            ["str"] = s.GetPower(), ["vit"] = s.GetHealth(), ["agi"] = s.GetAgility(), ["dex"] = s.GetBaseAccuracy(), ["kno"] = s.GetKnowledge(), ["will"] = s.GetWill(),
            ["attack_delay_ms"] = npc.GetAttackSpeed(), ["normal_attack_range"] = (float)npc.GetAttackRange(),
            ["normal_walk_speed"] = s.GetWalkSpeed(), ["normal_run_speed"] = s.GetRunSpeed(), ["combat_run_speed"] = s.GetRunSpeedFight(),
            ["bound_front"] = b.GetFront(), ["bound_side"] = b.GetSide(), ["bound_upper"] = b.GetUpper(),
            ["legacy_physical_attack_stat"] = s.GetAttack(), ["legacy_magical_attack_stat"] = s.GetMagicalAttack(),
            ["sensory_range"] = npc.GetAggroRange(), ["sensory_angle"] = npc.GetAggroAngle()
        };
    }
}
