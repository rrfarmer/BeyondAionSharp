using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using static Aion.GameServer.Dataholders.RetailNpc.RetailNpcVirtualGuard;

namespace Aion.GameServer.Dataholders.RetailNpc;

/// <summary>
/// Pure complete selected-profile construction. Java NpcData/NpcGameStats were read first.
/// Evidence: phase2/virtual-profile-construction (17 helpers and complete Set/Calc bodies).
/// No ordinary guard is bypassed; no effect collection, current HP mutation, movement or activation.
/// </summary>
public static class RetailNpcVirtualProfileConstruction
{
    public static ImmutableArray<(string Field, int Offset, int Width)> HelperContracts { get; } =
    [ ("max_hp",0x30,32),("hp_regen",0x38,32),("physical_defend",0x58,32),("magical_defend",0x60,32),
      ("magical_resist",0x68,16),("attack_delay",0x78,16),("hit_accuracy",0x80,16),("magical_hit_accuracy",0x108,16),
      ("dodge",0x98,16),("block",0xa8,16),("parry",0xa0,16),("critical",0x90,16),("magical_critical",0xf8,16),
      ("hit_count",0x88,8),("min_damage",0xe8,16),("max_damage",0xf0,16),("concentration",0x100,16) ];

    public static RetailNpcVirtualResult<RetailNpcVirtualConstructionPlan> Construct(RetailNpcTemplateInputs source,
        RetailNpcLevelRatioTableInputs ratio, RetailNpcVirtualSelection selection,
        RetailNpcModifierAggregate effects, RetailNpcVirtualContext context)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(ratio);
        ArgumentNullException.ThrowIfNull(selection); ArgumentNullException.ThrowIfNull(effects); ArgumentNullException.ThrowIfNull(context);
        try
        {
            Validate(source, ratio, selection, effects, context);
            var templates = new Dictionary<string, RetailNpcVirtualSourceValue>(StringComparer.Ordinal);
            var roots = RetailNpcSourceProfile.RawChildren(source.RawRecord).ToArray();
            foreach (var entry in source.Integers)
                templates.Add(entry.Key, Typed(entry.Key, entry.Value, roots, entry.Key == "max_hp" ? 1 : 0));
            Need(templates["dual"].TemplateValue == 0, "virtual_source_dual", "Nonzero source dual cannot enter a nondual context");
            foreach (string name in new[] { "max_hp" })
            {
                var v = templates[name]; long parsedUpper = Math.Min(v.TemplateValue, 2_000_000_000);
                templates[name] = v with { TemplateValue = Math.Max(1, parsedUpper), History = v.History
                    .Add(new("TemplateParser", "Main:14027ff11.signed-upper-cap", v.TemplateValue, parsedUpper))
                    .Add(new("TemplatePostparse", "Main:14027d44b.lower-one", parsedUpper, Math.Max(1, parsedUpper))) };
            }
            var ratioSource = templates["stat_ratio"];
            long resolved;
            if (ratioSource.Presence == RetailNpcSourcePresence.Absent)
            {
                long level = templates["level"].TemplateValue;
                Need(level is >= 0 and <= 100, "virtual_ratio_level", "Absent stat_ratio requires admitted template level0..100");
                resolved = ratio.ForTemplateLevel((int)level);
            }
            else resolved = Math.Clamp(unchecked((uint)ratioSource.TemplateValue), 1000u, 65000u);
            templates["stat_ratio"] = ratioSource with { TemplateValue = resolved, History = ratioSource.History.Add(new("TemplatePostparse",
                ratioSource.Presence == RetailNpcSourcePresence.Absent ? "Main:14027d471.loaded-level-table" : "Main:140281e64.unsigned-explicit-clamp", ratioSource.TemplateValue, resolved)) };
            var firstGroups = Canonical(roots, "first_level_attr"); Need(firstGroups.Length <= 1, "virtual_first_level", "Repeated first-level groups unsupported");
            JsonElement[] first = firstGroups.Length == 0 ? [] : RetailNpcSourceProfile.RawChildren(firstGroups[0]).ToArray();
            Need(firstGroups.Length == 0 || firstGroups[0][1].GetArrayLength() == 0, "virtual_first_level", "First-level attributes unsupported");
            Need(first.All(n => n[0].GetString() is string tag && (RetailNpcTemplateProjection.PrimaryFields.Contains(tag, StringComparer.Ordinal)
                || RetailNpcTemplateProjection.ElementFields.Contains(tag, StringComparer.Ordinal))), "virtual_first_level", "Unknown/aliased first-level token blocks construction");
            foreach (string name in RetailNpcTemplateProjection.PrimaryFields) templates.Add(name, Typed(name, source.FirstLevel.Primaries[name], first, 0));
            foreach (string name in RetailNpcTemplateProjection.ElementFields) templates.Add(name, Typed(name, source.FirstLevel.Elements[name], first, 0));
            templates.Add("cancel_level", RawAuxiliary(roots, "cancel_level", 100));
            templates.Add("dp_reduce", RawAuxiliary(roots, "dp_reduce", 0));

            var descriptor = selection.Descriptor; var helpers = new Dictionary<string, RetailNpcVirtualHelperValue>(StringComparer.Ordinal);
            foreach (var h in HelperContracts) helpers.Add(h.Field, Helper(h.Field, h.Offset, h.Width, templates[h.Field].TemplateValue, descriptor.Pairs[h.Offset]));
            long initialHp = Helper("initial_hp", 0x30, 32, templates["max_hp"].TemplateValue, descriptor.Pairs[0x30]).FullResult;
            long initialMp = Helper("initial_mp", 0x40, 32, templates["max_mp"].TemplateValue, descriptor.Pairs[0x40]).FullResult;
            var baselines = new Dictionary<string, long>(StringComparer.Ordinal); var writes = new List<RetailNpcVirtualWrite>();
            var merges = new List<RetailNpcAbnormalSourceMerge>();
            writes.Add(new("Set", "initial_hp", "0x778", "int32", initialHp));
            writes.Add(new("Set", "initial_mp_diagnostic", "0x790", "int32", initialMp));
            for (int i = 0; i < 6; i++)
            {
                string primary = RetailNpcTemplateProjection.PrimaryFields[i], element = RetailNpcTemplateProjection.ElementFields[i];
                baselines[primary] = templates[primary].TemplateValue;
                var pair = descriptor.Pairs[i * 8]; long input = templates[element].TemplateValue;
                long full = ElementFull(unchecked((short)input), pair);
                baselines[element] = unchecked((short)full);
                writes.Add(new("Set", primary, Hex(0x740 + i * 2), "uint16", baselines[primary]));
                writes.Add(new("Set", primary, Hex(0x758 + i * 2), "uint16", baselines[primary]));
                writes.Add(new("Set", element, Hex(0x74c + i * 2), "int16", baselines[element]));
                writes.Add(new("Set", element, Hex(0x764 + i * 2), "int16", baselines[element]));
                templates[element] = templates[element] with { History = templates[element].History
                    .Add(new("SetElement", "percent-divided-before-signed16-base;int32-clamp", input, full))
                    .Add(new("SetElement", "low16-signed-view", full, baselines[element])) };
            }
            Abnormals(source, roots, "abnormal_resists", false, baselines, merges);
            Abnormals(source, roots, "abnormal_penetrations", true, baselines, merges);
            foreach (var spec in RetailNpcOrdinaryScalarProjection.Specifications.Take(21))
            {
                long raw = templates[spec.Field].TemplateValue;
                baselines[spec.Field] = helpers.TryGetValue(spec.Field, out var helper) ? helper.ActorBaseline : View(raw, spec.View);
            }
            baselines["heal_skill_boost"] = baselines["mp_heal_skill_boost"] = 0;
            Need(baselines.Count == 85 && helpers.Count == 17, "virtual_coverage", "Atomic complete85scalar/17helper coverage required");
            var scalars = new Dictionary<string, RetailNpcVirtualScalarValue>(StringComparer.Ordinal);
            foreach (var spec in RetailNpcOrdinaryScalarProjection.Specifications)
            {
                long b = baselines[spec.Field];
                long operand = spec.Field switch { "critical" => unchecked((ushort)templates["magical_critical"].TemplateValue),
                    "magical_critical" => 0, "concentration" => unchecked((ushort)templates["concentration"].TemplateValue), _ => b };
                int? pct = null, flat = null; long? product = null, contribution = null; long calculated = b;
                if (spec.ModifierIndex is int index)
                {
                    var pair = effects.Pairs[index]; flat = pair.Flat;
                    if (spec.Operation == RetailNpcScalarOperation.PercentAndFlat)
                    { pct = pair.Percent; long p = checked(operand * pair.Percent), q = p / 100; product = p; contribution = q; calculated = checked(b + q + pair.Flat); }
                    else calculated = b + pair.Flat;
                }
                Need(spec.Minimum.HasValue == spec.Maximum.HasValue, "virtual_scalar_contract", "Both native cap bounds or neither required");
                long value = spec.Minimum is long lower && spec.Maximum is long upper ? Math.Clamp(calculated, lower, upper) : calculated;
                scalars.Add(spec.Field, new(spec, b, operand, pct, flat, product, contribution, calculated, value));
            }
            var shadows = Shadows(baselines, templates);
            // First-level effects occur before all second-level seed/common/effect writes.
            foreach (var spec in RetailNpcOrdinaryScalarProjection.Specifications.Skip(21))
                if (spec.Field.StartsWith("elemental_", StringComparison.Ordinal) || spec.Field.StartsWith("abnormal_", StringComparison.Ordinal))
                    writes.Add(new("CalcFirstLevelEffects", spec.Field, spec.ActorOffset, "native16", scalars[spec.Field].Value));
            foreach (var h in HelperContracts)
            {
                string offset = h.Field switch { "attack_delay" => "0x14a6", "hit_count" => "0x1687", "min_damage" => "0x14dc", "max_damage" => "0x14e0",
                    _ => RetailNpcOrdinaryScalarProjection.Specifications.Single(x => x.Field == h.Field).ActorOffset };
                writes.Add(new("CalcSecondProfileSeed", h.Field, offset, "native" + h.Width, helpers[h.Field].ActorBaseline));
            }
            writes.Add(new("CalcSecondProfileSeed", "original_delay", "0x14a8", "uint16", helpers["attack_delay"].ActorBaseline));
            foreach (int off in new[] { 0x14ac, 0x14b2, 0x14be, 0x14c2, 0x14de, 0x14e2, 0x1688 })
                writes.Add(new("CalcSecondProfileSeed", "offhand_zero", Hex(off), off == 0x1688 ? "uint8" : "uint16", 0));
            foreach (var spec in RetailNpcOrdinaryScalarProjection.Specifications.Take(21).Where(s => !helpers.ContainsKey(s.Field)
                && s.Field != "stat_ratio" && s.Field != "magical_skill_boost" && s.Field != "magical_skill_boost_resist"))
                writes.Add(new("CalcSecondProfileSeed", spec.Field, spec.ActorOffset, "native16", baselines[spec.Field]));
            ushort cancel = unchecked((ushort)templates["cancel_level"].TemplateValue); byte dp = checked((byte)templates["dp_reduce"].TemplateValue);
            writes.Add(new("CalcSecondProfileSeed", "cancel_level", "0x14e6", "uint16", cancel));
            writes.Add(new("CommonBeforeEffects", "max_mp_diagnostic", "0x78c", "int32", 0));
            foreach (string tag in new[] { "stat_ratio", "magical_skill_boost", "magical_skill_boost_resist", "heal_skill_boost", "mp_heal_skill_boost" })
                writes.Add(new("CommonBeforeEffects", tag, scalars[tag].Specification.ActorOffset, "native", baselines[tag]));
            foreach (string off in new[] { "0x14f4","0x14f6","0x1500","0x1810","0x183a","0x181e","0x1822","0x183c","0x1838","0x182c","0x183e","0x182e","0x1830","0x180c","0x1840","0x1842","0x1836","0x1820","0x1824","0x181c","0x1828","0x1834" })
                writes.Add(new("CommonShadowBeforeEffects", "shadow", off, "native", shadows[off]));
            writes.Add(new("CommonBeforeEffects", "hit_modifier_pair", "0x14ec", "int64_bits", BinaryPrimitives.ReadInt64LittleEndian(effects.CanonicalBytes.AsSpan(30 * 8, 8))));
            writes.Add(new("CommonBeforeEffects", "magic_hit_modifier_pair", "0x14f8", "int64_bits", BinaryPrimitives.ReadInt64LittleEndian(effects.CanonicalBytes.AsSpan(105 * 8, 8))));
            writes.Add(new("CommonBeforeEffects", "generated_zero", "0x14e8", "uint16", 0));
            writes.Add(new("CommonBeforeEffects", "generated_zero", "0x14ea", "uint16", 0));
            writes.Add(new("CommonBeforeEffects", "dp_reduce", "0x1685", "uint8", dp));
            var delay = Delay(helpers["attack_delay"].ActorBaseline, effects.Pairs[29]);
            var cp = effects.Pairs[35]; long countOperand = unchecked((byte)templates["hit_count"].TemplateValue);
            long countBase = helpers["hit_count"].ActorBaseline, countProduct = countOperand * cp.Percent, countUnclamped = countBase + countProduct / 100 + cp.Flat;
            var count = new RetailNpcVirtualCount(countBase, countOperand, cp.Percent, cp.Flat, countProduct, countProduct / 100, countUnclamped, Math.Clamp(countUnclamped, 1, 16));
            foreach (string field in new[] { "stat_ratio","physical_defend","magical_defend","magical_resist","hit_count","attack_delay","hit_accuracy","magical_hit_accuracy",
                "critical","magical_critical","parry","dodge","block","concentration","magical_skill_boost","magical_skill_boost_resist","heal_skill_boost","mp_heal_skill_boost",
                "physical_critical_reduce_rate","magical_critical_reduce_rate","physical_critical_damage_reduce","magical_critical_damage_reduce","proc_reduce_rate","max_hp","hp_regen" })
            {
                if (field == "attack_delay") writes.Add(new("CalcSecondEffects", field, "0x14a6", "uint16", delay.Value));
                else if (field == "hit_count") writes.Add(new("CalcSecondEffects", field, "0x1687", "uint8", count.Value));
                else writes.Add(new("CalcSecondEffects", field, scalars[field].Specification.ActorOffset, "native", scalars[field].Value));
            }
            var tail = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (int off in new[] { 0x8ec,0x8f0,0x8f4,0x8f8,0x14d0,0x14d2,0x14d4,0x14d8,0x8dc,0x8e0,0x910 }) tail[Hex(off)] = 0;
            tail["0x8e4"] = tail["0x8e8"] = 1; tail["0x8d4"] = tail["0x8d8"] = 0x3f800000;
            tail["0x14b4"] = scalars["parry"].Value;
            foreach (var t in tail) writes.Add(new("NondualCommonTail", "tail", t.Key, "bits_or_native", t.Value));
            return new(new(source, ratio, selection, effects, context, templates, helpers, scalars, shadows, tail, writes, merges, initialHp, initialMp, delay, count, cancel, dp), []);
        }
        catch (Exception e) when (e is RetailNpcVirtualError or InvalidDataException or InvalidOperationException or ArgumentException or FormatException or OverflowException or KeyNotFoundException)
        { return Failure<RetailNpcVirtualConstructionPlan>(e); }
    }

    internal static void Validate(RetailNpcTemplateInputs source, RetailNpcLevelRatioTableInputs ratio, RetailNpcVirtualSelection selection,
        RetailNpcModifierAggregate effects, RetailNpcVirtualContext c)
    {
        Domain(c.SeedDomain); Domain(c.CurrentDomain); Need(c.SeedDomain == c.CurrentDomain, "virtual_domain_epoch", "Seed/current eligibility differs");
        Need(c.ExpectedSource == source.Receipt && effects.Receipt.Source == source.Receipt && source.Source.NpcId == source.NpcId
            && source.Source.Devname == source.Devname && source.Source.DatasetId == source.Receipt.DatasetId, "virtual_source", "Full source identity differs");
        Need(source.Receipt.MainSha256 == RetailNpcTemplateProjection.MainBinarySha256 && source.Receipt.NpcSha256 == RetailNpcTemplateProjection.NpcBinarySha256,
            "virtual_binary", "Unsupported native consumer build");
        Need(c.ExpectedRatioTable == ratio.Receipt && ratio.Receipt.SourceSha256 == effects.Receipt.RatioTableSha256
            && ratio.Receipt.DatasetId == source.Receipt.DatasetId && ratio.Receipt.OutputManifestSha256 == source.Receipt.OutputManifestSha256
            && ratio.Receipt.Region == source.Receipt.Region && ratio.Receipt.MainBinarySha256 == source.Receipt.MainSha256, "virtual_ratio_table", "Level table differs from source/context");
        var t = selection.Table.Receipt;
        Need(c.ExpectedTable == t && t.DatasetId == source.Receipt.DatasetId && t.OutputManifestSha256 == source.Receipt.OutputManifestSha256
            && t.Region == source.Receipt.Region && t.MainSha256 == source.Receipt.MainSha256, "virtual_table_context", "Profile table differs from source/context");
        foreach (string h in new[] { c.ExpectedDescriptorSha256, c.ExpectedRegistrySha256, c.ExpectedPackageContextSha256, c.ExpectedModifierSha256 }) Hash(h);
        Need(c.ExpectedDescriptorSha256 == selection.Descriptor.SemanticSha256 && c.ExpectedNativeNameId == selection.NativeNameId && c.ExpectedRank == selection.NativeRank
            && c.ExpectedRegistrySha256 == selection.Registry.ContentSha256, "virtual_selection", "Selected descriptor/key/registry differs");
        var e = effects.Receipt; var r = selection.Registry;
        Need(c.ExpectedPackageContextSha256 == t.PackageContextSha256 && e.PackageContextSha256 == t.PackageContextSha256
            && c.ExpectedModifierSha256 == e.ContentSha256 && effects.Pairs.Length == 161 && effects.CanonicalBytes.Length == 1288,
            "virtual_effect_context", "Complete modifier/source package pins required");
        Need(Text(c.ExpectedEpoch, 256) && c.ExpectedEpoch == e.Epoch && c.ExpectedEpoch == r.Epoch && c.ExpectedOrigin == e.Origin && c.ExpectedOrigin == r.Origin
            && c.ExpectedActorObjectIdentity == e.ActorObjectIdentity && c.ExpectedActorObjectIdentity == r.ActorObjectIdentity,
            "virtual_effect_epoch", "Registry/effect origin, actor and epoch must match explicitly");
    }
    internal static RetailNpcVirtualHelperValue Helper(string field, int offset, int width, long raw, RetailNpcVirtualPair p)
    {
        bool hp = field is "max_hp" or "initial_hp"; long operand = hp ? unchecked((int)raw) : unchecked((uint)raw);
        int divided = p.Percent / 100; long product = operand * divided, sum = product + p.Flat;
        long full = Math.Clamp(sum, hp ? int.MinValue : 0L, hp ? 2_000_000_000 : int.MaxValue);
        long view = width == 8 ? unchecked((byte)full) : width == 16 ? unchecked((ushort)full) : full;
        return new(field, offset, operand, p.Percent, p.Flat, divided, product, sum, full, width, view);
    }
    internal static RetailNpcVirtualDelay Delay(long word, RetailNpcModifierPairInput p)
    {
        long product = word * p.Percent, contribution = product / 100, sum = word + contribution + p.Flat;
        long absolute = Math.Clamp(sum, 500, 10000), half = Math.Max(absolute, word / 2), doubled = unchecked((ushort)(word * 2));
        return new(word, p.Percent, p.Flat, product, contribution, sum, absolute, half, doubled, Math.Min(half, doubled));
    }
    internal static long ElementFull(short nativeTemplate, RetailNpcVirtualPair pair) =>
        Math.Clamp((long)(pair.Percent / 100) * nativeTemplate + pair.Flat, int.MinValue, int.MaxValue);
    private static long View(long raw, RetailNpcScalarView view) => view switch
    { RetailNpcScalarView.Unsigned32 => unchecked((uint)raw), RetailNpcScalarView.Unsigned16 => unchecked((ushort)raw), RetailNpcScalarView.Signed16 => unchecked((short)raw), _ => raw };
    private static Dictionary<string, long> Shadows(Dictionary<string, long> b, Dictionary<string, RetailNpcVirtualSourceValue> t) => new(StringComparer.Ordinal)
    {
        ["0x180c"]=b["max_hp"],["0x1810"]=b["hp_regen"],["0x181c"]=unchecked((ushort)t["magical_critical"].TemplateValue),
        ["0x181e"]=b["dodge"],["0x1820"]=b["parry"],["0x1822"]=b["block"],["0x1824"]=b["physical_defend"],["0x1828"]=b["magical_defend"],
        ["0x182c"]=b["magical_skill_boost"],["0x182e"]=b["magical_skill_boost_resist"],["0x1830"]=0,["0x1834"]=b["magical_resist"],["0x1836"]=0,
        ["0x1838"]=unchecked((ushort)t["concentration"].TemplateValue),["0x183a"]=b["physical_critical_reduce_rate"],["0x183c"]=b["magical_critical_reduce_rate"],
        ["0x183e"]=b["physical_critical_damage_reduce"],["0x1840"]=b["magical_critical_damage_reduce"],["0x1842"]=b["proc_reduce_rate"],
        ["0x14f4"]=b["hit_accuracy"],["0x14f6"]=0,["0x1500"]=b["magical_hit_accuracy"]
    };
    private static JsonElement[] Canonical(JsonElement[] siblings, string tag)
    {
        var n = siblings.Where(x => string.Equals(x[0].GetString(), tag, StringComparison.OrdinalIgnoreCase)).ToArray();
        Need(n.Length <= 1 && (n.Length == 0 || n[0][0].GetString() == tag), "virtual_source_order", "Duplicate/aliased raw root requires unresolved ordering: " + tag); return n;
    }
    private static RetailNpcVirtualSourceValue Typed<T>(string name, RetailNpcSourceScalar<T> field, JsonElement[] siblings, long initial) where T : struct
    {
        var nodes = Canonical(siblings, name); bool absent = field.Presence == RetailNpcSourcePresence.Absent;
        Need(absent ? nodes.Length == 0 && field.Lexemes.IsEmpty && !field.ParsedValue.HasValue
            : nodes.Length == 1 && field.Lexemes.Length == 1 && field.ParsedValue.HasValue
                && field.Presence is RetailNpcSourcePresence.ExplicitValue or RetailNpcSourcePresence.ExplicitZero,
            "virtual_source_presence", "Strict raw/typed presence differs: " + name);
        long value = initial; var steps = ImmutableArray.Create(new RetailNpcVirtualStep("TemplateConstructor", "source-bound-default", initial, initial));
        if (!absent)
        {
            string text = Leaf(nodes[0]); Need(text == field.Lexemes[0], "virtual_source_lexeme", "Raw/typed lexeme differs");
            value = Convert.ToInt64(field.ParsedValue.GetValueOrDefault(), CultureInfo.InvariantCulture);
            Need((field.Presence == RetailNpcSourcePresence.ExplicitZero) == (value == 0), "virtual_source_zero", "Explicit zero differs from value");
            steps = steps.Add(new("TemplateParser", "source-value", initial, value));
        }
        return new(name, field.Presence, field.Lexemes, initial, value, steps);
    }
    private static RetailNpcVirtualSourceValue RawAuxiliary(JsonElement[] roots, string name, int initial)
    {
        var nodes = Canonical(roots, name); long value = initial; string? text = null;
        var steps = ImmutableArray.Create(new RetailNpcVirtualStep("TemplateConstructor", "byte-proved-auxiliary-default", initial, initial));
        if (nodes.Length != 0)
        {
            text = Leaf(nodes[0]); Need(Regex.IsMatch(text.Trim(), @"^[+-]?[0-9]+$", RegexOptions.CultureInvariant)
                && int.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _), "virtual_auxiliary_width", "Strict signed32 auxiliary source required");
            int parsed = int.Parse(text.Trim(), CultureInfo.InvariantCulture); value = parsed;
            steps = steps.Add(new("TemplateParser", "source-value", initial, value));
            if (name == "dp_reduce" && unchecked((uint)parsed) > 100) { value = 0; steps = steps.Add(new("TemplateParser", "unsigned-above100-logs-and-zeroes", parsed, 0)); }
        }
        return new(name, nodes.Length == 0 ? RetailNpcSourcePresence.Absent : int.Parse(text!.Trim(), CultureInfo.InvariantCulture) == 0 ? RetailNpcSourcePresence.ExplicitZero : RetailNpcSourcePresence.ExplicitValue,
            text is null ? [] : [text], initial, value, steps);
    }
    private static void Abnormals(RetailNpcTemplateInputs source, JsonElement[] roots, string groupTag, bool penetration,
        Dictionary<string, long> values, List<RetailNpcAbnormalSourceMerge> merges)
    {
        var groups = Canonical(roots, groupTag); var slots = new ushort[25];
        JsonElement[] entries = groups.Length == 0 ? [] : RetailNpcSourceProfile.RawChildren(groups[0]).ToArray();
        Need(entries.Length <= 4096 && (groups.Length == 0 || groups[0][1].GetArrayLength() == 0), "virtual_abnormal_bound", "Bounded canonical abnormal group required");
        string prefix = penetration ? "abnormal_penetration" : "abnormal_resist";
        for (int ordinal = 0; ordinal < entries.Length; ordinal++)
        {
            var entry = entries[ordinal]; Need(entry[0].GetString() == "data" && entry[1].GetArrayLength() == 0, "virtual_abnormal_shape", "Canonical data entry required");
            var children = RetailNpcSourceProfile.RawChildren(entry).ToArray();
            Need(children.All(n => n[0].GetString() == prefix + "_name" || n[0].GetString() == prefix + "_value"), "virtual_abnormal_child", "Unknown abnormal child");
            var names = children.Where(n => n[0].GetString() == prefix + "_name").ToArray(); var numbers = children.Where(n => n[0].GetString() == prefix + "_value").ToArray();
            Need(names.Length <= 1 && numbers.Length == 1 && ushort.TryParse(Leaf(numbers[0]).Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _), "virtual_abnormal_value", "One explicit unsigned16 value required");
            ushort v = ushort.Parse(Leaf(numbers[0]).Trim(), CultureInfo.InvariantCulture); string? name = names.Length == 0 ? null : Leaf(names[0]);
            Need(name is not null || v == 0, "virtual_abnormal_placeholder", "Nameless entry must be explicit zero"); uint mask = name is null ? 0 : Mask(name);
            merges.Add(new(groupTag, ordinal, name, v, mask, name is null, entry));
            for (int i = 0; i < 25; i++) if ((mask & (1u << i)) != 0) slots[i] = Math.Max(slots[i], v);
        }
        for (int i = 0; i < 25; i++) values[$"{prefix}[{i}]"] = slots[i];
    }
    private static uint Mask(string raw)
    {
        Need(raw.Length is > 0 and <= 2048 && raw.All(c => c <= 127), "virtual_abnormal_name", "Bounded ASCII status names required");
        var tokens = raw.Split(','); Need(tokens.Length <= 64, "virtual_abnormal_name", "Too many status tokens"); uint mask = 0;
        for (int j = 0; j < tokens.Length; j++)
        {
            string token = j == 0 ? tokens[j] : tokens[j].TrimStart(' '); bool comma = j < tokens.Length - 1;
            Need(token.Length > 0 && token.TrimEnd(' ') == token, "virtual_abnormal_name", "Empty/abbreviated/trailing-space status unsupported");
            int index = -1;
            for (int i = 0; i < (comma ? 25 : 32); i++) if (token.Equals(RetailNpcOrdinaryScalarProjection.StatusNames[i], StringComparison.OrdinalIgnoreCase)) { index = i; break; }
            if (index >= 0) { mask |= 1u << index; continue; }
            if (token.Equals("Stat_arAll", StringComparison.OrdinalIgnoreCase)) mask |= 0x1f62fbf;
            else if (token.Equals("Stat_arStunLike", StringComparison.OrdinalIgnoreCase)) mask |= 0x9d040;
            else if (token.Equals("Stat_arPhysicalAb", StringComparison.OrdinalIgnoreCase)) mask = comma ? 0x1620b7 : mask | 0x101620b7;
            else if (token.Equals("Stat_arMentalAb", StringComparison.OrdinalIgnoreCase)) mask = comma ? 0x800f48 : mask | 0xc800f48;
            else throw new RetailNpcVirtualError("virtual_abnormal_name", "Unknown status token: " + token);
        }
        return mask;
    }
    private static string Leaf(JsonElement node)
    {
        Need(node[1].GetArrayLength() == 0 && node[2].EnumerateArray().All(x => x.ValueKind == JsonValueKind.String), "virtual_source_leaf", "Plain raw scalar required");
        return RetailNpcSourceProfile.RawText(node);
    }
    private static string Hex(int n) => "0x" + n.ToString("x", CultureInfo.InvariantCulture);
}
