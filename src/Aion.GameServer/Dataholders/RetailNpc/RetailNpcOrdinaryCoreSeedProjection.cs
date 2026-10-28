using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Aion.GameServer.Dataholders.RetailNpc;

/// <summary>
/// Inactive fresh-template and ordinary kind 1 core seed projection. Java NpcData/StatsTemplate
/// were read first; approved retail rules replace none of their active paths here. Instruction
/// evidence: phase2/ordinary-seed-evidence, 10,289 PE-checked instructions in 30 cached functions.
/// The field domain ends before effects, damage helpers, live eligibility and MP lifecycle.
/// </summary>
public static class RetailNpcOrdinaryCoreSeedProjection
{
    private static readonly ImmutableArray<string> CoreRoots =
        ["level", "max_hp", "hp_regen", "max_mp", "mp_regen", "min_damage", "max_damage", "stat_ratio", "dual", "first_level_attr"];

    public static RetailNpcOrdinarySeedProjectionResult ProjectOrdinaryCoreSeeds(
        RetailNpcTemplateInputs source, RetailNpcLevelRatioTableInputs ratioTable,
        RetailNpcOrdinarySeedDomain domain)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(ratioTable);
        ArgumentNullException.ThrowIfNull(domain);
        try
        {
            Validate(source, ratioTable, domain);
            var receipt = source.Receipt;
            var table = ratioTable.Receipt;
            RetailNpcCoreSeedValue<int> Read(string name, int initial = 0) => Construct(source.Integers[name], initial, receipt, "template." + name);
            var level = Read("level");
            var hp = Read("max_hp", 1);
            if (hp.Value > 2_000_000_000)
                hp = Step(hp, 2_000_000_000, RetailNpcCoreSeedStage.TemplateParser, RetailNpcCoreSeedOrigin.ParserClamp,
                    "Main:14027ff11.signed-upper-hp-cap");
            var initialHp = source.Integers["max_hp"].Presence == RetailNpcSourcePresence.Absent
                ? Construct(source.Integers["max_hp"], 1, receipt, "Main:1400a9598.initial-hp")
                : Step(hp, hp.Value, RetailNpcCoreSeedStage.TemplateParser, RetailNpcCoreSeedOrigin.TemplateCopy,
                    "Main:14027ff55.initial-hp-copy");
            if (hp.Value < 1)
                hp = Step(hp, 1, RetailNpcCoreSeedStage.TemplatePostparse, RetailNpcCoreSeedOrigin.PostparseClamp,
                    "Main:14027d44b.max-hp-lower-bound");
            if (initialHp.Value < 1)
                initialHp = Step(initialHp, 1, RetailNpcCoreSeedStage.TemplatePostparse, RetailNpcCoreSeedOrigin.PostparseClamp,
                    "Main:14027d45e.initial-hp-lower-bound");
            var hpRegen = Read("hp_regen");
            var mp = Read("max_mp");
            var initialMp = source.Integers["max_mp"].Presence == RetailNpcSourcePresence.Absent
                ? Construct(source.Integers["max_mp"], 0, receipt, "Main:1400a95a3.initial-mp")
                : Step(mp, mp.Value, RetailNpcCoreSeedStage.TemplateParser, RetailNpcCoreSeedOrigin.TemplateCopy,
                    "Main:14027ff7c.initial-mp-copy");
            var mpRegen = Read("mp_regen");
            var minimum = Read("min_damage");
            var maximum = Read("max_damage");
            var ratio = Read("stat_ratio");
            if (source.StatRatio.Presence == RetailNpcSourcePresence.Absent)
            {
                Need(unchecked((uint)level.Value) < 101, "ratio_template_level", "/npc/level",
                    "Absent ratio requires a template level 0..100; spawn level is a separate input");
                ratio = Step(ratio, (int)ratioTable.ForTemplateLevel(level.Value), RetailNpcCoreSeedStage.TemplatePostparse,
                    RetailNpcCoreSeedOrigin.LevelTableDefault, "Main:14027d471.loaded-level-ratio", table);
            }
            else
            {
                uint bits = unchecked((uint)ratio.Value);
                int clamped = (int)Math.Clamp(bits, 1000u, 65000u);
                // This records the unsigned parser comparison, including the negative signed
                // source bit pattern, without losing its original strict signed32 lexeme.
                var operation = new RetailNpcCoreSeedStep(RetailNpcCoreSeedStage.TemplateParser,
                    bits == (uint)clamped ? RetailNpcCoreSeedOrigin.SourceValue : RetailNpcCoreSeedOrigin.ParserClamp,
                    "Main:140281e64.unsigned-ratio-clamp", bits, clamped);
                ratio = new(clamped, ratio.SourcePresence, ratio.SourceLexemes, ratio.SourceRecordPath,
                    receipt, ratio.Steps.Add(operation), ratio.SourceValueUsed);
            }
            var primaries = source.FirstLevel.Primaries.ToDictionary(x => x.Key,
                x => Construct(x.Value, (ushort)0, receipt, "template.first-level." + x.Key), StringComparer.Ordinal);
            var elements = source.FirstLevel.Elements.ToDictionary(x => x.Key,
                x => Construct(x.Value, (short)0, receipt, "template.first-level." + x.Key), StringComparer.Ordinal);
            var template = new RetailNpcTemplateConstructionValues(level, hp, initialHp, hpRegen, mp, initialMp,
                mpRegen, minimum, maximum, ratio, primaries, elements);
            var primarySeeds = primaries.ToDictionary(x => x.Key,
                x => Step(x.Value, x.Value.Value, RetailNpcCoreSeedStage.OrdinarySet, RetailNpcCoreSeedOrigin.TemplateCopy,
                    "Main:140b1a228.first-level-copy"), StringComparer.Ordinal);
            var elementSeeds = elements.ToDictionary(x => x.Key,
                x => Step(x.Value, x.Value.Value, RetailNpcCoreSeedStage.OrdinarySet, RetailNpcCoreSeedOrigin.TemplateCopy,
                    "Main:140b1a228.first-level-copy"), StringComparer.Ordinal);
            var diagnostics = new RetailNpcOrdinaryMpSeedDiagnostics(
                Step(initialMp, initialMp.Value, RetailNpcCoreSeedStage.OrdinarySet, RetailNpcCoreSeedOrigin.TemplateCopy,
                    "Main:140b1a271.current-mp-on-set"),
                Zero(source.Integers["max_mp"], receipt, RetailNpcCoreSeedStage.OrdinarySecondLevelSeed,
                    "Main:140359624.common-max-mp-zero"),
                Zero(source.Integers["mp_regen"], receipt, RetailNpcCoreSeedStage.FreshCreatureInitialization,
                    "Main:14034f227.fresh-mp-regeneration-zero"));
            var seeds = new RetailNpcOrdinaryCoreSeeds(source, table, domain, template,
                Step(initialHp, initialHp.Value, RetailNpcCoreSeedStage.OrdinarySet, RetailNpcCoreSeedOrigin.TemplateCopy,
                    "Main:140b1a265.current-hp-on-set"),
                Step(hp, hp.Value, RetailNpcCoreSeedStage.OrdinarySecondLevelSeed, RetailNpcCoreSeedOrigin.TemplateCopy,
                    "Main:140358875.max-hp-seed"),
                Step(hpRegen, hpRegen.Value, RetailNpcCoreSeedStage.OrdinarySecondLevelSeed, RetailNpcCoreSeedOrigin.TemplateCopy,
                    "Main:140358887.hp-regeneration-seed"),
                Step(ratio, ratio.Value, RetailNpcCoreSeedStage.OrdinarySecondLevelSeed, RetailNpcCoreSeedOrigin.TemplateCopy,
                    "Main:14035967c.ratio-seed"),
                Narrow(minimum, "Main:140358a15.main-hand-minimum"), Narrow(maximum, "Main:140358a2a.main-hand-maximum"),
                ZeroWord(source.Integers["min_damage"], receipt, "Main:140358a31.off-hand-minimum-zero"),
                ZeroWord(source.Integers["max_damage"], receipt, "Main:140358a38.off-hand-maximum-zero"),
                diagnostics, primarySeeds, elementSeeds);
            return new(seeds, []);
        }
        catch (SeedError error) { return new(null, [new(error.Code, error.Path, error.Message)]); }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or KeyNotFoundException
            or FormatException or OverflowException or IndexOutOfRangeException or ArgumentException)
        { return new(null, [new("seed_source_contract", "/npc", error.Message)]); }
    }

    private static void Validate(RetailNpcTemplateInputs source, RetailNpcLevelRatioTableInputs table,
        RetailNpcOrdinarySeedDomain domain)
    {
        var receipt = source.Receipt;
        Need(domain.ExpectedSource == receipt, "seed_source_identity", "/source", "Caller-pinned complete source receipt differs");
        Need(receipt.MainSha256 == RetailNpcTemplateProjection.MainBinarySha256
            && receipt.NpcSha256 == RetailNpcTemplateProjection.NpcBinarySha256,
            "seed_binary_identity", "/source", "Unsupported native consumer build");
        RetailNpcDatasetValidation.Hash(domain.ExpectedRatioTableSha256);
        Need(table.Receipt.SourceSha256 == domain.ExpectedRatioTableSha256
            && table.Receipt.DatasetId == receipt.DatasetId && table.Receipt.OutputManifestSha256 == receipt.OutputManifestSha256
            && table.Receipt.Region == receipt.Region && table.Receipt.MainBinarySha256 == receipt.MainSha256,
            "seed_table_context", "/pc_stat_ratios", "Table receipt differs from the pinned dataset, region, manifest or binary context");
        Need(domain.FreshUniqueTemplate == true, "seed_template_context", "/context/template", "Fresh unique template is required; null is unresolved");
        Need(domain.FreshActor == true, "seed_actor_context", "/context/actor", "Fresh actor is required; null is unresolved");
        Need(domain.NativeKind == 1, "seed_kind_context", "/context/kind", "Only verified native kind 1 is admitted");
        Need(domain.MasterObjectId == 0u, "seed_master_context", "/context/master_object_id", "Native master ID +188c must be explicitly zero; null is unresolved");
        Need(domain.SourceRelatedObjectId == 0u, "seed_related_context", "/context/source_related_object_id", "Native source-related ID +1894 must be explicitly zero; nonzero semantics are outside this domain");
        Need(domain.VirtualProfileRecordPresent == false, "seed_virtual_profile_context", "/context/virtual_profile", "Virtual/profile scaling pointer +1850 must independently be absent");
        Need(domain.DifficultyRecordPresent == false, "seed_difficulty_context", "/context/difficulty", "Difficulty pointer must be absent");
        Need(domain.DualWeaponFlagPresent == false || domain.StatisticsBeforeDualSplit && domain.DualWeaponFlagPresent == true,
            "seed_dual_context", "/context/dual", "Dual flag must be clear unless only the proved pre-split statistics are requested");
        Need(source.Source.NpcId == receipt.NpcId && source.Source.Devname == receipt.Devname
            && source.Source.DatasetId == receipt.DatasetId, "seed_source_identity", "/source", "Source and typed receipt disagree");
        var rootNodes = RetailNpcSourceProfile.RawChildren(source.RawRecord).ToArray();
        foreach (string tag in CoreRoots)
        {
            var matching = rootNodes.Where(x => string.Equals(x[0].GetString(), tag, StringComparison.OrdinalIgnoreCase)).ToArray();
            Need(matching.Length <= 1, "seed_duplicate_root", "/npc/" + tag, "Root aliases/repeats cannot be first-selected");
            Need(matching.Length == 0 || matching[0][0].GetString() == tag,
                "seed_root_alias", "/npc/" + tag, "Case-insensitive native root aliases are outside this strict canonical source domain");
        }
        foreach (string tag in CoreRoots.Where(x => x != "first_level_attr")) CheckScalar(source.Integers[tag]);
        Need((source.Integers["dual"].ParsedValue is not (null or 0)) == (domain.DualWeaponFlagPresent == true),
            "seed_dual_source", "/npc/dual", "The declared actual dual flag must match the selected source");
        Need(source.Integers["dual"].ParsedValue is null or 0 || domain.StatisticsBeforeDualSplit && domain.DualWeaponFlagPresent == true,
            "seed_dual_source", "/npc/dual", "A nonzero source dual value cannot enter the ordinary non-dual seed domain");
        var groups = source.Source.GetRawRootChildren("first_level_attr");
        Need(groups.Length <= 1, "seed_duplicate_first_level", "/npc/first_level_attr", "Repeated first-level groups cannot merge");
        if (groups.Length == 1)
        {
            foreach (var node in RetailNpcSourceProfile.RawChildren(groups[0]))
            {
                string name = node[0].GetString() ?? "";
                Need(RetailNpcTemplateProjection.PrimaryFields.Contains(name, StringComparer.Ordinal)
                    || RetailNpcTemplateProjection.ElementFields.Contains(name, StringComparer.Ordinal),
                    "seed_unknown_first_level", "/npc/first_level_attr/" + name,
                    "Native unknown tokens terminate the nested parse; unresolved elemental aliases cannot be ignored");
                Need(RetailNpcSourceProfile.RawChildren(groups[0]).Count(x => x[0].GetString() == name) == 1,
                    "seed_duplicate_first_level", "/npc/first_level_attr/" + name, "Repeated native writes require ordered resolution");
            }
        }
        foreach (string name in RetailNpcTemplateProjection.PrimaryFields) CheckScalar(source.FirstLevel.Primaries[name]);
        foreach (string name in RetailNpcTemplateProjection.ElementFields) CheckScalar(source.FirstLevel.Elements[name]);
    }

    private static void CheckScalar<T>(RetailNpcSourceScalar<T> field) where T : struct
    {
        bool absent = field.Presence == RetailNpcSourcePresence.Absent;
        Need(absent ? field.Lexemes.IsEmpty && !field.ParsedValue.HasValue
            : (field.Presence is RetailNpcSourcePresence.ExplicitValue or RetailNpcSourcePresence.ExplicitZero)
                && field.Lexemes.Length == 1 && field.ParsedValue.HasValue,
            "seed_scalar_contract", field.RecordPath, "Strict projected presence, one lexeme and numeric value are required");
        if (!absent)
        {
            long value = Convert.ToInt64(field.ParsedValue.GetValueOrDefault(), CultureInfo.InvariantCulture);
            Need((field.Presence == RetailNpcSourcePresence.ExplicitZero) == (value == 0),
                "seed_scalar_contract", field.RecordPath, "Source zero presence and typed value disagree");
        }
    }

    private static RetailNpcCoreSeedValue<T> Construct<T>(RetailNpcSourceScalar<T> field, T initial,
        RetailNpcTemplateSourceReceipt receipt, string rule) where T : struct
    {
        var steps = ImmutableArray.Create(new RetailNpcCoreSeedStep(RetailNpcCoreSeedStage.TemplateConstructor,
            RetailNpcCoreSeedOrigin.ConstructorDefault, rule + ".constructor", null, Number(initial)));
        T value = initial;
        bool used = field.Presence != RetailNpcSourcePresence.Absent;
        if (used)
        {
            value = field.ParsedValue ?? throw new SeedError("seed_scalar_contract", field.RecordPath, "Missing strict numeric source");
            steps = steps.Add(new(RetailNpcCoreSeedStage.TemplateParser, RetailNpcCoreSeedOrigin.SourceValue,
                rule + ".parser", Number(initial), Number(value)));
        }
        return new(value, field.Presence, field.Lexemes, field.RecordPath, receipt, steps, used);
    }

    private static RetailNpcCoreSeedValue<T> Step<T>(RetailNpcCoreSeedValue<T> source, T value,
        RetailNpcCoreSeedStage stage, RetailNpcCoreSeedOrigin origin, string rule,
        RetailNpcLevelRatioTableReceipt? table = null) where T : struct =>
        new(value, source.SourcePresence, source.SourceLexemes, source.SourceRecordPath, source.SourceReceipt,
            source.Steps.Add(new(stage, origin, rule, Number(source.Value), Number(value))),
            source.SourceValueUsed, table ?? source.RatioTableReceipt);

    private static RetailNpcCoreSeedValue<ushort> Narrow(RetailNpcCoreSeedValue<int> value, string rule)
    {
        ushort narrowed = unchecked((ushort)value.Value);
        return new(narrowed, value.SourcePresence, value.SourceLexemes, value.SourceRecordPath, value.SourceReceipt,
            value.Steps.Add(new(RetailNpcCoreSeedStage.OrdinarySecondLevelSeed, RetailNpcCoreSeedOrigin.RuntimeNarrowing,
                rule, value.Value, narrowed)), value.SourceValueUsed);
    }

    private static RetailNpcCoreSeedValue<int> Zero(RetailNpcSourceScalar<int> field,
        RetailNpcTemplateSourceReceipt receipt, RetailNpcCoreSeedStage stage, string rule) =>
        new(0, field.Presence, field.Lexemes, field.RecordPath, receipt,
            [new(stage, RetailNpcCoreSeedOrigin.NativeZeroInitialization, rule, null, 0)], false);

    private static RetailNpcCoreSeedValue<ushort> ZeroWord(RetailNpcSourceScalar<int> field,
        RetailNpcTemplateSourceReceipt receipt, string rule) => new(0, field.Presence, field.Lexemes, field.RecordPath,
            receipt, [new(RetailNpcCoreSeedStage.OrdinarySecondLevelSeed, RetailNpcCoreSeedOrigin.NativeZeroInitialization, rule, null, 0)], false);

    private static long Number<T>(T value) where T : struct => Convert.ToInt64(value, CultureInfo.InvariantCulture);
    private static void Need([DoesNotReturnIf(false)] bool value, string code, string path, string message)
    { if (!value) throw new SeedError(code, path, message); }
    private sealed class SeedError(string code, string path, string message) : Exception(message)
    { internal string Code { get; } = code; internal string Path { get; } = path; }
}
