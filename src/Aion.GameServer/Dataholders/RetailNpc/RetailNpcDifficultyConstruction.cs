using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aion.GameServer.Dataholders.RetailNpc;

/// <summary>
/// Inactive complete14-field instance-difficulty construction, pinned Main CalcSecondLevelAttr
/// 140359255..1403595ef, helpers140877bd0/140877c80 and Set140b1a34d..140b1a3b2.
/// Java NpcData/SummonGameStats read first. Separate source/owner/context, never ordinary-guard bypass.
/// </summary>
public static class RetailNpcDifficultyConstruction
{
    public static ImmutableArray<RetailNpcDifficultyFieldContract> Contracts { get; } = [
        new("max_hp", "0x1c4", "0x774", "0x180c", 1, 2_000_000_000, "hp_unsigned32"),
        new("physical_defend", "0x1dc", "0x1494", "0x1824", 0, 500000, "signed32"),
        new("magical_defend", "0x1e0", "0x1498", "0x1828", 0, 500000, "signed32"),
        new("magical_resist", "0x1e4", "0x149c", "0x1834", 0, 9999, "signed32"),
        new("hit_accuracy", "0x1f8", "0x14aa", "0x14f4", 0, 9999, "signed32"),
        new("magical_hit_accuracy", "0x1fc", "0x14c4", "0x1500", 0, 9999, "signed32"),
        new("critical", "0x20c", "0x14bc", "0x181c", 0, 9999, "signed32"),
        new("magical_critical", "0x210", "0x14c0", "0x1836", 0, 9999, "signed32"),
        new("physical_critical_reduce_rate", "0x224", "0x14c6", "0x183a", 0, 9999, "signed32"),
        new("magical_critical_reduce_rate", "0x22c", "0x14c8", "0x183c", 0, 9999, "signed32"),
        new("physical_critical_damage_reduce", "0x228", "0x14ca", "0x183e", 0, 700, "signed32"),
        new("magical_critical_damage_reduce", "0x230", "0x14cc", "0x1840", 0, 700, "signed32"),
        new("min_damage", "0x1e8", "0x14dc", null, 0, 65000, "unsigned16"),
        new("max_damage", "0x1ec", "0x14e0", null, 0, 65000, "unsigned16")];

    public static RetailNpcDifficultyConstructionResult Construct(RetailNpcTemplateInputs source,
        RetailNpcLevelRatioTableInputs levelTable, RetailNpcDifficultyTableInputs difficultyTable,
        int recordId, RetailNpcDifficultyContext context)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(levelTable);
        ArgumentNullException.ThrowIfNull(difficultyTable); ArgumentNullException.ThrowIfNull(context);
        try
        {
            Validate(source, levelTable, difficultyTable, context);
            var records = difficultyTable.Records.Where(x => x.Id == recordId).ToArray();
            Need(records.Length == 1, "difficulty_record", "/context/record", "Exactly one admitted selected record required");
            var record = records[0];
            Need(recordId == context.ExpectedRecordId && record.NormalizedXmlSha256 == context.ExpectedRecordNormalizedXmlSha256
                && record.WorldId == context.NativeBaseWorldId && record.StatScalingRatio.Value == context.NativeStatScalingRatio,
                "difficulty_record_pin", "/context/record", "Record identity, selected world and actual ratio differ from caller pins");
            Need(context.NativeCounter.HasValue, "difficulty_counter", "/context/counter", "Signed native counter is required; meaning is not inferred");
            int counter = context.NativeCounter.Value, ratio = record.StatScalingRatio.Value;
            var predicate = new RetailNpcDifficultyPredicate(true, counter, ratio);
            var fields = new Dictionary<string, RetailNpcDifficultyFieldPlan>(StringComparer.Ordinal);
            foreach (var contract in Contracts)
            {
                var operand = Operand(source, contract.Field, contract.OperandView);
                long prior = contract.Field switch
                {
                    "max_hp" => operand.ConstructorValue,
                    "physical_defend" or "magical_defend" => unchecked((uint)operand.ParsedSigned32),
                    _ => unchecked((ushort)operand.ParsedSigned32)
                };
                fields.Add(contract.Field, Evaluate(contract, operand, prior, ratio, counter, predicate.ApplySecondLevel));
            }
            var hpContract = Contracts[0] with { ActorOffset = "0x778", CommonTailCopyOffset = null };
            // Set's chosen-record branch has no ratio>0/counter!=0 gate. Keep it separate.
            var hpOperand = Operand(source, "max_hp", "hp_unsigned32");
            var initialHp = Evaluate(hpContract, hpOperand, hpOperand.ConstructorValue, ratio, counter, true);
            var writes = new List<RetailNpcDifficultyWrite>
            { new("OrdinarySet", "initial_hp", "0x778", "Main:140b1a3b2", 32, initialHp.Value) };
            // Actual persistent write order: min,max,HP,defenses,accuracy/critical, reductions.
            foreach (string name in new[] { "min_damage", "max_damage", "max_hp", "physical_defend", "magical_defend", "magical_resist" }) AddPersistent(name);
            var boosts = new Dictionary<string, RetailNpcDifficultySourceOperand>(StringComparer.Ordinal);
            foreach (var (name, actor) in new[] { ("magical_skill_boost", "0x1502"), ("magical_skill_boost_resist", "0x1504") })
            {
                boosts.Add(name, Operand(source, name, "signed16"));
                if (predicate.ApplySecondLevel) writes.Add(new("SecondLevelDifficulty", name, actor, "Main:140359578..586.transient-zero", 16, 0));
            }
            foreach (string name in new[] { "hit_accuracy", "magical_hit_accuracy", "critical", "magical_critical", "physical_critical_reduce_rate",
                "magical_critical_reduce_rate", "physical_critical_damage_reduce", "magical_critical_damage_reduce" }) AddPersistent(name);
            // These copies belong to the common pre-effect tail, irrespective of predicate.
            AddCopy("hit_accuracy"); AddCopy("magical_hit_accuracy");
            foreach (var (name, actor) in new[] { ("magical_skill_boost", "0x1502"), ("magical_skill_boost_resist", "0x1504") })
                writes.Add(new("CommonPreEffectTail", name, actor, "Main:14035964e..66c.template-signed16-overwrite", 16, boosts[name].NativeOperand));
            foreach (string name in new[] { "physical_critical_reduce_rate", "magical_critical_reduce_rate" }) AddCopy(name);
            writes.Add(new("CommonPreEffectTail", "magical_skill_boost", "0x182c", "Main:1403596d3", 16, boosts["magical_skill_boost"].NativeOperand));
            AddCopy("physical_critical_damage_reduce");
            writes.Add(new("CommonPreEffectTail", "magical_skill_boost_resist", "0x182e", "Main:1403596eb", 16, boosts["magical_skill_boost_resist"].NativeOperand));
            foreach (string name in new[] { "max_hp", "magical_critical_damage_reduce", "magical_critical", "physical_defend", "critical", "magical_defend", "magical_resist" }) AddCopy(name);
            Need(fields.Count == 14, "difficulty_coverage", "/fields", "Atomic complete14-field plan required");
            return new(new(source, levelTable.Receipt, difficultyTable.Receipt, record, context, predicate, initialHp, fields, writes, boosts), []);

            void AddPersistent(string name)
            {
                var field = fields[name];
                if (field.Applied) writes.Add(new("SecondLevelDifficulty", name, field.Contract.ActorOffset, "Main:140359525..5e1",
                    name is "max_hp" or "physical_defend" or "magical_defend" ? 32 : 16, field.Value));
            }
            void AddCopy(string name)
            {
                var field = fields[name];
                writes.Add(new("CommonPreEffectTail", name, field.Contract.CommonTailCopyOffset!, "Main:14035960d..742.shadow-copy",
                    name is "max_hp" or "physical_defend" or "magical_defend" ? 32 : 16, field.Value));
            }
        }
        catch (DifficultyError e) { return new(null, [new(e.Code, e.Path, e.Message)]); }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or InvalidOperationException or KeyNotFoundException
            or FormatException or OverflowException or IndexOutOfRangeException)
        { return new(null, [new("difficulty_construction_contract", "/source", e.Message)]); }
    }

    private static void Validate(RetailNpcTemplateInputs source, RetailNpcLevelRatioTableInputs level,
        RetailNpcDifficultyTableInputs difficulty, RetailNpcDifficultyContext c)
    {
        Need(c.ExpectedSource == source.Receipt && source.Source.NpcId == source.NpcId && source.Source.Devname == source.Devname
            && source.Source.DatasetId == source.Receipt.DatasetId,
            "difficulty_source_pin", "/source", "Full admitted source receipt must match");
        var s = source.Receipt; var d = difficulty.Receipt; var l = level.Receipt;
        Need(s.MainSha256 == RetailNpcTemplateProjection.MainBinarySha256 && s.NpcSha256 == RetailNpcTemplateProjection.NpcBinarySha256,
            "difficulty_binary", "/source", "Pinned native build required");
        Need(c.ExpectedLevelRatioTable == l && c.ExpectedDifficultyTable == d, "difficulty_table_pin", "/context/table", "Complete table receipts must match");
        RetailNpcDatasetValidation.Hash(c.ExpectedPackageContextSha256);
        RetailNpcDatasetValidation.Hash(c.ExpectedRecordNormalizedXmlSha256);
        Need(d.PackageContextSha256 == c.ExpectedPackageContextSha256 && d.DatasetId == s.DatasetId && d.OutputManifestSha256 == s.OutputManifestSha256
            && d.Region == s.Region && d.MainSha256 == s.MainSha256 && d.NpcSha256 == s.NpcSha256
            && l.DatasetId == s.DatasetId && l.OutputManifestSha256 == s.OutputManifestSha256 && l.Region == s.Region && l.MainBinarySha256 == s.MainSha256,
            "difficulty_package", "/context", "Coherent source/region/build/package and level-table pins required");
        Need(c.Origin is RetailNpcDifficultyInputOrigin.FixedMathematicalFixture or RetailNpcDifficultyInputOrigin.ExternalSourceAssertion,
            "difficulty_context_origin", "/context", "Explicit supported context origin required");
        Need(RetailNpcDifficultyTableInputs.Text(c.Epoch, 256) && RetailNpcDifficultyTableInputs.Text(c.EvidenceReference, 4096)
            && (c.Origin == RetailNpcDifficultyInputOrigin.FixedMathematicalFixture ? c.ActorObjectIdentity is null
                : RetailNpcDifficultyTableInputs.Text(c.ActorObjectIdentity, 256) && d.Origin == RetailNpcDifficultyInputOrigin.ExternalSourceAssertion),
            "difficulty_epoch", "/context", "Explicit epoch/evidence and origin-compatible actor identity required");
        Need(c.FreshUniqueTemplate == true && c.FreshActor == true && c.NativeKind == 1,
            "difficulty_kind", "/context", "Only explicit fresh kind1 direct-template construction is admitted");
        Need(c.MasterObjectId == 0u && c.SourceRelatedObjectId == 0u, "difficulty_owner", "/context", "Independent master188c/related1894 must both be explicitly zero");
        Need(c.VirtualProfileRecordPresent == false && c.DualWeaponFlagPresent == false && c.DifficultyRecordPresent == true,
            "difficulty_domain", "/context", "Absent virtual profile/nondual and present difficulty record must be independent assertions");
        Need(c.DynamicWorldResolved == true && c.NativeDynamicWorldObjectId is >= 0x80000000u and < 0xc0000000u,
            "difficulty_world_context", "/context/world", "Explicit native dynamic-world selection required");
        Need(c.FloatContract == RetailNpcDifficultyFloatContract.Binary32NearestEvenMaskedInvalid,
            "difficulty_float_contract", "/context/float", "Only explicit binary32 RNE/masked-invalid mathematical contract is supported");
        var dual = Operand(source, "dual", "signed32");
        Need(dual.ParsedSigned32 == 0, "difficulty_source_dual", "/npc/dual", "Nonzero source dual cannot enter nondual construction");
    }

    private static RetailNpcDifficultySourceOperand Operand(RetailNpcTemplateInputs source, string name, string view)
    {
        var aliases = RetailNpcSourceProfile.RawChildren(source.RawRecord).Where(x => string.Equals(x[0].GetString(), name, StringComparison.OrdinalIgnoreCase)).ToArray();
        Need(aliases.Length <= 1 && (aliases.Length == 0 || aliases[0][0].GetString() == name),
            "difficulty_source_alias", "/npc/" + name, "Known native aliases/repeated roots cannot be first-selected");
        var field = source.Integers[name]; bool absent = field.Presence == RetailNpcSourcePresence.Absent;
        Need(absent ? aliases.Length == 0 && field.Lexemes.IsEmpty && !field.ParsedValue.HasValue
            : aliases.Length == 1 && field.Lexemes.Length == 1 && field.ParsedValue.HasValue
                && field.Presence is RetailNpcSourcePresence.ExplicitZero or RetailNpcSourcePresence.ExplicitValue,
            "difficulty_source_presence", field.RecordPath, "Canonical raw/typed scalar presence required");
        int parsed = field.ParsedValue.GetValueOrDefault();
        if (!absent)
        {
            var raw = aliases[0];
            Need(raw[1].GetArrayLength() == 0 && raw[2].GetArrayLength() == 1 && raw[2][0].ValueKind == JsonValueKind.String,
                "difficulty_source_leaf", field.RecordPath, "One canonical scalar lexeme required");
            string lexeme = raw[2][0].GetString()!;
            Need(lexeme == field.Lexemes[0] && Regex.IsMatch(lexeme.Trim(), @"^[+-]?[0-9]+$", RegexOptions.CultureInvariant)
                && int.TryParse(lexeme.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value) && value == parsed
                && (field.Presence == RetailNpcSourcePresence.ExplicitZero) == (parsed == 0),
                "difficulty_source_value", field.RecordPath, "Raw lexeme, signed32 bits and presence differ");
        }
        long constructed = name == "max_hp" ? Math.Clamp(absent ? 1 : parsed, 1, 2_000_000_000) : parsed;
        long native = view switch { "hp_unsigned32" => constructed, "unsigned16" => unchecked((ushort)parsed), "signed16" => unchecked((short)parsed), _ => parsed };
        return new(field.Presence, field.Lexemes, field.RecordPath, parsed, constructed, native, view);
    }

    private static RetailNpcDifficultyFieldPlan Evaluate(RetailNpcDifficultyFieldContract contract,
        RetailNpcDifficultySourceOperand operand, long prior, int ratio, int counter, bool applied)
    {
        if (!applied) return new(contract, operand, prior, false, [], null, prior);
        var steps = new List<RetailNpcDifficultyFloatStep>();
        uint f = Float32.Integer(ratio); steps.Add(new("ratio.signed32-to-binary32", f));
        f = Float32.Divide1000(f); steps.Add(new("ratio-divide-1000", f));
        if (contract.Field == "max_hp") { f = Float32.Multiply(f, 0x3fb33333u); steps.Add(new("hp-multiply-1.4", f)); }
        f = Float32.Multiply(f, Float32.Integer(counter)); steps.Add(new("multiply-signed-counter", f));
        f = Float32.AddOne(f); steps.Add(new("add-one", f));
        f = Float32.Multiply(f, Float32.Integer(operand.NativeOperand)); steps.Add(new("multiply-base", f));
        int converted = Float32.TruncateSigned32(f);
        return new(contract, operand, prior, true, steps, converted, Math.Clamp(converted, contract.Minimum, contract.Maximum));
    }

    // Private bounded machine: signed/unsigned32->binary32, /1000, multiplication and +1 only.
    // Exact rationals round each instruction to nearest-even. No process FP state or general FP API.
    private static class Float32
    {
        internal static uint Integer(long value) => Round(value, BigInteger.One, false);
        internal static uint Divide1000(uint bits) { var (n, d) = Decode(bits); return Round(n, d * 1000, (bits & 0x80000000u) != 0); }
        internal static uint Multiply(uint a, uint b)
        { var (an, ad) = Decode(a); var (bn, bd) = Decode(b); return Round(an * bn, ad * bd, ((a ^ b) & 0x80000000u) != 0); }
        internal static uint AddOne(uint bits) { var (n, d) = Decode(bits); return Round(n + d, d, false); }
        internal static int TruncateSigned32(uint bits)
        { var (n, d) = Decode(bits); BigInteger q = n / d; return q < int.MinValue || q > int.MaxValue ? int.MinValue : (int)q; }
        private static (BigInteger N, BigInteger D) Decode(uint bits)
        {
            int exponent = (int)((bits >> 23) & 255);
            Need(exponent < 255, "difficulty_float_bound", "/float", "Only finite proved operands are admitted");
            BigInteger n = exponent == 0 ? bits & 0x7fffffu : (bits & 0x7fffffu) | 0x800000u;
            if ((bits & 0x80000000u) != 0) n = -n;
            int power = exponent == 0 ? -149 : exponent - 150;
            return power >= 0 ? (n << power, BigInteger.One) : (n, BigInteger.One << -power);
        }
        private static uint Round(BigInteger signedNumerator, BigInteger denominator, bool negativeZero)
        {
            if (signedNumerator.IsZero) return negativeZero ? 0x80000000u : 0u;
            uint sign = signedNumerator.Sign < 0 ? 0x80000000u : 0;
            BigInteger n = BigInteger.Abs(signedNumerator);
            int e = checked((int)(n.GetBitLength() - denominator.GetBitLength()));
            if (e >= 0 ? n < (denominator << e) : (n << -e) < denominator) e--;
            // All proven int32/word stages are normal or zero; no general underflow/NaN library.
            Need(e is >= -126 and <= 127, "difficulty_float_bound", "/float", "Outside bounded native finite stage");
            int shift = 23 - e;
            BigInteger scaledN = shift >= 0 ? n << shift : n;
            BigInteger scaledD = shift >= 0 ? denominator : denominator << -shift;
            BigInteger q = BigInteger.DivRem(scaledN, scaledD, out BigInteger remainder);
            int tie = (remainder * 2).CompareTo(scaledD);
            if (tie > 0 || tie == 0 && !q.IsEven) q++;
            if (q == (BigInteger.One << 24)) { q >>= 1; e++; }
            Need(e <= 127, "difficulty_float_bound", "/float", "Outside bounded finite result");
            return sign | ((uint)(e + 127) << 23) | ((uint)q & 0x7fffffu);
        }
    }
    private static void Need([DoesNotReturnIf(false)] bool value, string code, string path, string message)
    { if (!value) throw new DifficultyError(code, path, message); }
    private sealed class DifficultyError(string code, string path, string message) : Exception(message)
    { internal string Code { get; } = code; internal string Path { get; } = path; }
}
