using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace Aion.GameServer.Dataholders.RetailNpc;

/// <summary>
/// Complete inactive ordinary scalar construction. Java NpcData/NpcGameStats were read first.
/// Approved native evidence: phase2/ordinary-effective-stats (1,553 new verified instructions,
/// immutable prior10,289). No actor, effect defaults, MP policy or legacy-path integration.
/// </summary>
public static class RetailNpcOrdinaryScalarProjection
{
    public const int OutputCount = 85;
    public static ImmutableArray<string> StatusNames { get; } = ["Poison", "Bleed", "Paralyze", "Sleep", "Root", "Blind", "Charm",
        "Disease", "Silence", "Fear", "Curse", "Confuse", "Stun", "Petrification", "Stumble", "Stagger", "OpenAerial", "Snare",
        "Slow", "Spin", "Bind", "Deform", "Pulled", "NoFly", "SimpleRoot", "BuffStun", "BuffSleep", "BuffSilence", "BuffBind",
        "Invisible", "InvulnerableWing", "Sanctuary"];
    // Canonical audit strings from PE table0x1410ea260, including native misspellings/order.
    public static ImmutableArray<string> ModifierNames { get; } = [
        "ATTR_INDEX_NULL", "allresist", "allpara", "strvit", "knowil", "agidex", "str", "vit",
        "agi", "dex", "kno", "wil", "edWater", "edAir", "edEarth", "edFire",
        "edLight", "edDark", "maxHp", "hpRegen", "maxMp", "mpRegen", "maxDp", "maxFp",
        "FpRegen", "phyAttack", "physicalDefend", "magicalAttack", "magicalResist", "attackDelay", "hitAccuracy", "dodge",
        "parry", "block", "critical", "hitCount", "speed", "FlySpeed", "attackRange", "maxCarryWeight",
        "magicalCritical", "concentration", "cancel_level", "arPoison", "arBleed", "arParalyze", "arSleep", "arRoot",
        "arBlind", "arCharm", "arDisease", "arSilence", "arFear", "arCurse", "arConfuse", "arStun",
        "arPerification", "arStumble", "arStagger", "arOpenAreial", "arSnare", "arSlow", "arSpin", "arBind",
        "arDeform", "arPulled", "arNoFly", "arSimpleRoot", "arAll", "Poison_Arp", "Bleed_Arp", "Paralyze_Arp",
        "Sleep_Arp", "Root_Arp", "Blind_Arp", "Charm_Arp", "Disease_Arp", "Silence_Arp", "Fear_Arp", "Curse_Arp",
        "Confuse_Arp", "Stun_Arp", "Perification_Arp", "Stumble_Arp", "Stagger_Arp", "OpenAreial_Arp", "Snare_Arp", "Slow_Arp",
        "Spin_Arp", "Bind_Arp", "Deform_Arp", "Pulled_Arp", "SimpleRoot_Arp", "NoFly_Arp", "All_Arp", "arStunLike",
        "arPhysicalAb", "arMentalAb", "ActiveDefend", "PMDefend", "PMAttack", "StrAgiKno", "VitDexWil", "AllSpeed",
        "MagicalSkillBoost", "MagicalHItAccuracy", "PvPAttackRatio", "PvPDefendRatio", "BoostCastingTime", "BoostHate", "HealSkillBoost", "PvPAttackRatio_Physical",
        "PvPDefendRatio_Physical", "PvPAttackRatio_Magical", "PvPDefendRatio_Magical", "PhysicalCriticalReduceRate", "MagicalCriticalReduceRate", "PhysicalCriticalDamageReduce", "MagicalCriticalDamageReduce", "PvEAttackRatio",
        "PvEDefendRatio", "PvEAttackRatio_Physical", "PvEDefendRatio_Physical", "PvEAttackRatio_Magical", "PvEDefendRatio_Magical", "magicalDefend", "MagicalSkillBoostResist", "PMDefend_New",
        "AllDefend", "BoostChargeTime", "HiddenPvPAttackRatio", "HiddenPvPDefendRatio", "HiddenPvPAttackRatio_Physical", "HiddenPvPDefendRatio_Physical", "HiddenPvPAttackRatio_Magical", "HiddenPvPDefendRatio_Magical",
        "ProcReduceRate", "HiddenProcReduceRate", "PvPHitAccuracy", "PvPDodge", "PvPParry", "PvPBlock", "PvPMagicalHitAccuracy", "PvPMagicalResist",
        "HiddenPvPHitAccuracy", "HiddenPvPDodge", "HiddenPvPParry", "HiddenPvPBlock", "HiddenPvPMagicalHitAccuracy", "HiddenPvPMagicalResist", "BlockPenetration", "HiddenBlockPenetration",
        "MpHealSkillBoost", "StatRatio", "hstr", "hvit", "hdex", "hagi", "hkno", "hwil",
        "flyboost_speed"
    ];
    public static ImmutableArray<RetailNpcScalarSpecification> Specifications { get; } = BuildSpecifications();

    private static ImmutableArray<RetailNpcScalarSpecification> BuildSpecifications()
    {
        var fields = ImmutableArray.CreateBuilder<RetailNpcScalarSpecification>();
        void Add(string tag, string offset, string actor, RetailNpcScalarView view, int index, long min, long max) =>
            fields.Add(new(tag, offset, actor, view, RetailNpcScalarOperation.PercentAndFlat, index, min, max));
        Add("max_hp", "0x1c4", "0x774", RetailNpcScalarView.Signed32, 18, 1, 2_000_000_000);
        Add("hp_regen", "0x1cc", "0x77c", RetailNpcScalarView.Signed32, 19, 0, 2_000_000_000);
        Add("stat_ratio", "0x238", "0x1514", RetailNpcScalarView.Unsigned32, 153, 1000, 65000);
        Add("physical_defend", "0x1dc", "0x1494", RetailNpcScalarView.Unsigned32, 26, 0, 500000);
        Add("magical_defend", "0x1e0", "0x1498", RetailNpcScalarView.Unsigned32, 125, 0, 500000);
        Add("magical_resist", "0x1e4", "0x149c", RetailNpcScalarView.Unsigned16, 28, 0, 9999);
        Add("hit_accuracy", "0x1f8", "0x14aa", RetailNpcScalarView.Unsigned16, 30, 0, 9999);
        Add("magical_hit_accuracy", "0x1fc", "0x14c4", RetailNpcScalarView.Unsigned16, 105, 0, 9999);
        Add("dodge", "0x200", "0x14ae", RetailNpcScalarView.Unsigned16, 31, 0, 9999);
        Add("parry", "0x204", "0x14b0", RetailNpcScalarView.Unsigned16, 32, 0, 9999);
        Add("block", "0x208", "0x14b6", RetailNpcScalarView.Unsigned16, 33, 0, 9999);
        Add("critical", "0x20c", "0x14bc", RetailNpcScalarView.Unsigned16, 34, 0, 9999);
        Add("magical_critical", "0x210", "0x14c0", RetailNpcScalarView.Unsigned16, 40, 0, 9999);
        Add("concentration", "0x214", "0x14e4", RetailNpcScalarView.Unsigned16, 41, 0, 9999);
        Add("magical_skill_boost", "0x21c", "0x1502", RetailNpcScalarView.Signed16, 104, 0, 9999);
        Add("magical_skill_boost_resist", "0x220", "0x1504", RetailNpcScalarView.Signed16, 126, 0, 9999);
        Add("physical_critical_reduce_rate", "0x224", "0x14c6", RetailNpcScalarView.Unsigned16, 115, 0, 9999);
        Add("magical_critical_reduce_rate", "0x22c", "0x14c8", RetailNpcScalarView.Unsigned16, 116, 0, 9999);
        Add("physical_critical_damage_reduce", "0x228", "0x14ca", RetailNpcScalarView.Unsigned16, 117, 0, 700);
        Add("magical_critical_damage_reduce", "0x230", "0x14cc", RetailNpcScalarView.Unsigned16, 118, 0, 700);
        Add("proc_reduce_rate", "0x234", "0x14ce", RetailNpcScalarView.Unsigned16, 136, 0, 1000);
        for (int i = 0; i < 6; i++)
            Add(RetailNpcTemplateProjection.ElementFields[i], Hex(0x18 + i * 2), Hex(0x764 + i * 2), RetailNpcScalarView.Signed16, 12 + i, -1300, 1300);
        foreach (bool penetration in new[] { false, true })
            for (int i = 0; i < 25; i++)
                Add($"abnormal_{(penetration ? "penetration" : "resist")}[{i}]", Hex((penetration ? 0x3ba : 0x388) + i * 2),
                    Hex((penetration ? 0x862 : 0x7fe) + i * 2), RetailNpcScalarView.Unsigned16, (penetration ? 69 : 43) + i, 0, 9999);
        for (int i = 0; i < 6; i++)
            fields.Add(new(RetailNpcTemplateProjection.PrimaryFields[i], Hex(0xc + i * 2), Hex(0x758 + i * 2),
                RetailNpcScalarView.Unsigned16, RetailNpcScalarOperation.Copy, null, null, null));
        fields.Add(new("heal_skill_boost", null, "0x1506", RetailNpcScalarView.GeneratedZero, RetailNpcScalarOperation.FlatOnly, 110, -1000, 1000));
        fields.Add(new("mp_heal_skill_boost", null, "0x1508", RetailNpcScalarView.GeneratedZero, RetailNpcScalarOperation.FlatOnly, 152, -1000, 1000));
        return fields.ToImmutable();
    }

    public static RetailNpcOrdinaryScalarProjectionResult Project(RetailNpcTemplateInputs source,
        RetailNpcOrdinaryCoreSeeds admittedCore, RetailNpcOrdinaryScalarProjectionContext context)
        => ProjectCore(source,admittedCore,context,false);

    public static RetailNpcOrdinaryScalarProjectionResult ProjectStatistics(RetailNpcTemplateInputs source,
        RetailNpcOrdinaryCoreSeeds admittedCore, RetailNpcOrdinaryScalarProjectionContext context)
        => ProjectCore(source,admittedCore,context,true);

    private static RetailNpcOrdinaryScalarProjectionResult ProjectCore(RetailNpcTemplateInputs source,
        RetailNpcOrdinaryCoreSeeds admittedCore, RetailNpcOrdinaryScalarProjectionContext context, bool statisticsOnly)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(admittedCore); ArgumentNullException.ThrowIfNull(context);
        try
        {
            var domain = context.ExpectedDomain;
            Need(domain is not null && domain == admittedCore.Domain && domain.ExpectedSource == source.Receipt
                && admittedCore.SourceReceipt == source.Receipt, "scalar_source_identity", "/source", "Full source/core/domain receipt differs");
            Need(source.RawRecord.GetRawText() == admittedCore.Source.RawRecord.GetRawText(),
                "scalar_source_identity", "/source", "Source record differs from the admitted core record");
            Need(domain.FreshUniqueTemplate == true && domain.FreshActor == true && domain.NativeKind == 1
                && domain.MasterObjectId == 0u && domain.SourceRelatedObjectId == 0u
                && domain.VirtualProfileRecordPresent == false && domain.DifficultyRecordPresent == false
                && (domain.DualWeaponFlagPresent == false || domain.StatisticsBeforeDualSplit && domain.DualWeaponFlagPresent == true),
                "scalar_seed_domain", "/context", "Only the admitted fresh ordinary kind1 core is supported");
            Need(domain.ExpectedRatioTableSha256 == admittedCore.RatioTableReceipt.SourceSha256
                && admittedCore.RatioTableReceipt.DatasetId == source.Receipt.DatasetId
                && admittedCore.RatioTableReceipt.OutputManifestSha256 == source.Receipt.OutputManifestSha256
                && admittedCore.RatioTableReceipt.Region == source.Receipt.Region
                && admittedCore.RatioTableReceipt.MainBinarySha256 == source.Receipt.MainSha256,
                "scalar_table_context", "/pc_stat_ratios", "Core table/source context differs");
            var fields = new Dictionary<string, RetailNpcOrdinaryScalarBase>(StringComparer.Ordinal);
            var merges = new List<RetailNpcAbnormalSourceMerge>();
            var semanticRoots = Specifications.Take(21).Select(x => x.Field).Concat(["abnormal_resists", "abnormal_penetrations"]);
            foreach (string tag in semanticRoots)
            {
                var matching = RetailNpcSourceProfile.RawChildren(source.RawRecord)
                    .Where(n => string.Equals(n[0].GetString(), tag, StringComparison.OrdinalIgnoreCase)).ToArray();
                Need(matching.Length <= 1 && (matching.Length == 0 || matching[0][0].GetString() == tag),
                    "scalar_root_order", "/npc/" + tag, "Repeated/case-aliased native writes need ordered resolution");
            }
            foreach (var specification in Specifications.Take(21))
            {
                string tag = specification.Field;
                RetailNpcOrdinaryScalarBase value = tag switch
                {
                    "max_hp" => FromCore(specification, admittedCore.MaxHpBeforeEffects),
                    "hp_regen" => FromCore(specification, admittedCore.HpRegenerationBeforeEffects),
                    "stat_ratio" => FromCore(specification, admittedCore.StatRatioBeforeEffects),
                    _ => Construct(specification, source.Integers[tag], source)
                };
                fields.Add(tag, value);
            }
            foreach (var specification in Specifications.Skip(21).Take(6))
            {
                fields.Add(specification.Field, FromCore(specification, admittedCore.FirstLevelBaseElements[specification.Field]));
            }
            bool zeroWrite = statisticsOnly && HasReviewedZeroWriteAbnormals(source);
            Abnormals(source, "abnormal_resists", false, fields, merges,zeroWrite);
            Abnormals(source, "abnormal_penetrations", true, fields, merges,zeroWrite);
            foreach (var specification in Specifications.Where(x => x.Operation == RetailNpcScalarOperation.Copy))
                fields.Add(specification.Field, FromCore(specification, admittedCore.FirstLevelBasePrimaries[specification.Field]));
            foreach (var specification in Specifications.Where(x => x.Operation == RetailNpcScalarOperation.FlatOnly))
                fields.Add(specification.Field, new(specification, 0, RetailNpcSourcePresence.Absent, [], "/generated/" + specification.Field,
                    source.Receipt, [new("OrdinarySeed", "NativeZeroInitialization", "Main:140359664.zero-generated-heal-boost", null, 0)]));
            Need(fields.Count == OutputCount, "scalar_coverage", "/fields", "Complete85-field projection is required");
            return new(new(admittedCore, fields, merges), []);
        }
        catch (ScalarError e) { return new(null, [new(e.Code, e.Path, e.Message)]); }
        catch (Exception e) when (e is InvalidOperationException or InvalidDataException or KeyNotFoundException or FormatException or OverflowException or ArgumentException)
        { return new(null, [new("scalar_source_contract", "/npc", e.Message)]); }
    }

    private static RetailNpcOrdinaryScalarBase Construct(RetailNpcScalarSpecification specification,
        RetailNpcSourceScalar<int> field, RetailNpcTemplateInputs source)
    {
        bool absent = field.Presence == RetailNpcSourcePresence.Absent;
        Need(absent ? field.Lexemes.IsEmpty && !field.ParsedValue.HasValue
            : (field.Presence is RetailNpcSourcePresence.ExplicitValue or RetailNpcSourcePresence.ExplicitZero)
                && field.Lexemes.Length == 1 && field.ParsedValue.HasValue,
            "scalar_presence", field.RecordPath, "Strict scalar presence/value is required");
        var raw = source.Source.GetRawRootChildren(specification.Field);
        Need(raw.Length == (absent ? 0 : 1), "scalar_presence", field.RecordPath, "Typed presence differs from raw source");
        int parsed = field.ParsedValue.GetValueOrDefault();
        if (!absent)
        {
            Need((field.Presence == RetailNpcSourcePresence.ExplicitZero) == (parsed == 0),
                "scalar_presence", field.RecordPath, "Zero presence differs from the source value");
            Need(Leaf(raw[0], field.RecordPath) == field.Lexemes[0], "scalar_presence", field.RecordPath, "Typed lexeme differs from raw source");
        }
        long value = specification.View switch
        {
            RetailNpcScalarView.Unsigned32 => unchecked((uint)parsed),
            RetailNpcScalarView.Unsigned16 => unchecked((ushort)parsed),
            RetailNpcScalarView.Signed16 => unchecked((short)parsed),
            _ => parsed
        };
        var steps = ImmutableArray.CreateBuilder<RetailNpcScalarSeedStep>();
        steps.Add(new("TemplateConstructor", "ConstructorDefault", "Main:NpcClassAttr.zero-" + specification.TemplateOffset, null, 0));
        if (!absent) steps.Add(new("TemplateParser", "SourceValue", "Main:NPCDB." + specification.Field, 0, parsed));
        steps.Add(new("OrdinarySeed", "NativeView", "Main:CalcSecondLevelAttr." + specification.View, parsed, value));
        return new(specification, value, field.Presence, field.Lexemes, field.RecordPath, source.Receipt, steps.ToImmutable());
    }

    private static RetailNpcOrdinaryScalarBase FromCore<T>(RetailNpcScalarSpecification specification,
        RetailNpcCoreSeedValue<T> core) where T : struct => new(specification, Convert.ToInt64(core.Value, CultureInfo.InvariantCulture),
        core.SourcePresence, core.SourceLexemes, core.SourceRecordPath, core.SourceReceipt,
        core.Steps.Select(x => new RetailNpcScalarSeedStep(x.Stage.ToString(), x.Origin.ToString(), x.Rule, x.Input, x.Result)).ToImmutableArray());

    private static void Abnormals(RetailNpcTemplateInputs source, string groupTag, bool penetration,
        IDictionary<string, RetailNpcOrdinaryScalarBase> fields, ICollection<RetailNpcAbnormalSourceMerge> merges, bool reviewedZeroWrite = false)
    {
        var groups = source.Source.GetRawRootChildren(groupTag);
        JsonElement[] entries = groups.Length == 0 ? [] : RetailNpcSourceProfile.RawChildren(groups[0]).ToArray();
        Need(entries.Length <= 4096, "scalar_abnormal_bound", "/npc/" + groupTag, "Too many abnormal entries");
        if (groups.Length == 1) Need(groups[0][1].GetArrayLength() == 0, "scalar_abnormal_shape", "/npc/" + groupTag, "Group attributes are unresolved");
        var values = new ushort[25];
        var lexemes = Enumerable.Range(0, 25).Select(_ => ImmutableArray.CreateBuilder<string>()).ToArray();
        var steps = Enumerable.Range(0, 25).Select(_ => ImmutableArray.CreateBuilder<RetailNpcScalarSeedStep>()).ToArray();
        foreach (var history in steps) history.Add(new("TemplateConstructor", "ConstructorDefault", "Main:NpcClassAttr.zero-abnormal-words", null, 0));
        if (reviewedZeroWrite)
        {
            // Complete pinned records have only invalid/nameless masks. Native
            // parsers log invalid names but write no slot. Missing unused values
            // are not invented, merged or relabeled as explicit source zeros.
            foreach (var history in steps) history.Add(new("TemplateParser", "ReviewedInvalidStatusNoWrites",
                "Main:140db04e0/140db0850/140db0a10;complete-record:" + source.Receipt.RawRecordSha256,null,0));
            entries = [];
        }
        string prefix = penetration ? "abnormal_penetration" : "abnormal_resist";
        for (int ordinal = 0; ordinal < entries.Length; ordinal++)
        {
            var entry = entries[ordinal]; string path = $"/npc/{groupTag}/data[{ordinal}]";
            Need(entry[0].GetString() == "data" && entry[1].GetArrayLength() == 0, "scalar_abnormal_shape", path, "Canonical data entry required");
            var children = RetailNpcSourceProfile.RawChildren(entry).ToArray();
            Need(children.All(n => n[0].GetString() == prefix + "_name" || n[0].GetString() == prefix + "_value"),
                "scalar_abnormal_child", path, "Unknown abnormal child/alias cannot be interpreted");
            var names = children.Where(n => n[0].GetString() == prefix + "_name").ToArray();
            var numbers = children.Where(n => n[0].GetString() == prefix + "_value").ToArray();
            Need(names.Length <= 1 && numbers.Length == 1, "scalar_abnormal_presence", path, "One explicit value and at most one name required; native carryover rejected");
            string lexeme = Leaf(numbers[0], path);
            Need(ushort.TryParse(lexeme.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out ushort value),
                "scalar_abnormal_value", path, "Native unsigned16 range0..65535 required");
            string? name = names.Length == 0 ? null : Leaf(names[0], path);
            Need(name is not null || value == 0, "scalar_abnormal_placeholder", path, "Only nameless explicit-zero entries are admitted as inert");
            uint mask = name is null ? 0 : Mask(name, path);
            merges.Add(new(groupTag, ordinal, name, value, mask, name is null, entry));
            if (name is null) continue;
            for (int i = 0; i < 25; i++) if ((mask & (1u << i)) != 0)
            {
                ushort before = values[i]; values[i] = Math.Max(before, value); lexemes[i].Add(lexeme);
                steps[i].Add(new("TemplateParser", "OrderedSourceMaskMaximum", $"Main:abnormal.data[{ordinal}].mask-{mask:x8}.max-with-{value}", before, values[i]));
            }
        }
        for (int i = 0; i < 25; i++)
        {
            string key = $"{prefix}[{i}]"; var specification = Specifications.Single(x => x.Field == key);
            steps[i].Add(new("OrdinarySet", "TemplateCopy", penetration ? "Main:140b19dbf.penetration-word" : "Main:140b19d7f.resist-word", values[i], values[i]));
            var presence = lexemes[i].Count == 0 ? RetailNpcSourcePresence.Absent : values[i] == 0 ? RetailNpcSourcePresence.ExplicitZero : RetailNpcSourcePresence.ExplicitValue;
            fields.Add(key, new(specification, values[i], presence, lexemes[i].ToImmutable(), $"/npc/{groupTag}/slot[{i}]", source.Receipt, steps[i].ToImmutable()));
        }
    }

    private static uint Mask(string raw, string path)
    {
        Need(raw.Length is > 0 and <= 2048 && raw.All(c => c <= 127), "scalar_abnormal_name", path, "Canonical bounded ASCII status names required");
        var tokens = raw.Split(','); Need(tokens.Length <= 64, "scalar_abnormal_name", path, "Too many status tokens");
        uint mask = 0;
        for (int j = 0; j < tokens.Length; j++)
        {
            string token = j == 0 ? tokens[j] : tokens[j].TrimStart(' ');
            bool commaBranch = j < tokens.Length - 1;
            Need(token.Length > 0 && token.TrimEnd(' ') == token, "scalar_abnormal_name", path, "Empty, abbreviated or trailing-space status is unresolved");
            int index = -1;
            for (int i = 0; i < (commaBranch ? 25 : 32); i++)
                if (string.Equals(token, StatusNames[i], StringComparison.OrdinalIgnoreCase)) { index = i; break; }
            if (index >= 0) { mask |= 1u << index; continue; }
            if (token.Equals("Stat_arAll", StringComparison.OrdinalIgnoreCase)) mask |= 0x1f62fbf;
            else if (token.Equals("Stat_arStunLike", StringComparison.OrdinalIgnoreCase)) mask |= 0x9d040;
            else if (token.Equals("Stat_arPhysicalAb", StringComparison.OrdinalIgnoreCase)) mask = commaBranch ? 0x1620b7 : mask | 0x101620b7;
            else if (token.Equals("Stat_arMentalAb", StringComparison.OrdinalIgnoreCase)) mask = commaBranch ? 0x800f48 : mask | 0xc800f48;
            else throw new ScalarError("scalar_abnormal_name", path, "Unknown/ambiguous status or unsupported comma token: " + token);
        }
        return mask;
    }

    private static string Leaf(JsonElement node, string path)
    {
        Need(node[1].GetArrayLength() == 0 && node[2].EnumerateArray().All(x => x.ValueKind == JsonValueKind.String),
            "scalar_leaf", path, "Scalar text without attributes/nested nodes required");
        return RetailNpcSourceProfile.RawText(node);
    }
    // Source-only parser reviews belong with the projection, independent of
    // runtime NPC/AI admission. Exact records; never a malformed-data fallback.
    private static bool HasReviewedZeroWriteAbnormals(RetailNpcTemplateInputs source) =>
        (source.NpcId, source.Receipt.RawRecordSha256) is
        (217360, "232b1f24c7d03e424f2eb6fea862a819deb1d1c805eb910c79056205d2066d9c") or
        (277229, "0cf727fbe4b8996450ca9c100307727f08c0efb24bcb16ce0315b9f91985ae4e");

    private static string Hex(int value) => "0x" + value.ToString("x", CultureInfo.InvariantCulture);
    private static void Need([DoesNotReturnIf(false)] bool condition, string code, string path, string message)
    { if (!condition) throw new ScalarError(code, path, message); }
    private sealed class ScalarError(string code, string path, string message) : Exception(message)
    { internal string Code => code; internal string Path => path; }
}
