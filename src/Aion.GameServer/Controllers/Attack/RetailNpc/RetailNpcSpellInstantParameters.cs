using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Aion.GameServer.Retail;
using Aion.GameServer.Dataholders.RetailNpc;
using static Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcMagicalCalculationInputs;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>
/// Complete pinned USA/base source records, not effective actors. Java DamageEffect/AttackUtil
/// were read first; the separately authorized native construction contract is documented in
/// phase3/pilot-magical-instant/independent-calculator-review/finite-domain-closure.
/// No source path is opened. Raw UTF16LE bytes, absence, zero, ordering and repeated metadata survive.
/// </summary>
internal static class RetailNpcSpellInstantParameters
{
    internal const string MainSha256 = "da1ee5633d65a7ebb436b29d15ca24e46ab4f267449384d177bf20d2d26eb8f1";
    internal const string PackageContext = "frozen-USA-base-spell13-pilot25";
    internal const string NativeClosureSha256 = "40fbe629b474def335fac75b3680c2067763675521271b786715b15f4853a916";
    internal const string SourceReviewSha256 = "8b9f7626712b39dce845e0626dca9231f9f87358d4738747af485db856fb1707";
    internal sealed record Pins(string MainSha256, string PackageContext, string NativeClosureSha256, string SourceReviewSha256);
    internal static Pins RequiredPins => new(MainSha256, PackageContext, NativeClosureSha256, SourceReviewSha256);
    internal sealed record SourceField(string Name, string Text, string NormalizedXml);
    internal sealed record BindingField(string Name, string Text);
    internal sealed record NpcSkillBinding(int NpcId, string NpcDevname, int ListOrdinal, int SkillLevel, string SkillName);
    internal sealed record PinnedBinding(int NpcId, string Devname, int Ordinal, int Level, ImmutableArray<BindingField> Fields);
    internal sealed record PinnedSkill(int Id, string Name, string Sha256, int R2, int Subtype, int Element,
        ImmutableArray<KeyValuePair<string, long>> Constructed, ImmutableArray<PinnedBinding> Bindings);
    internal sealed record PinnedNpc(int Id, string Name, string Sha256, int Level, int Minimum, int Maximum);
    internal sealed class SelectedSpell
    {
        internal int Id { get; }
        internal string Name { get; }
        internal string RawSha256 { get; }
        internal NpcSkillBinding Binding { get; }
        internal ImmutableArray<BindingField> OrderedBindingFields { get; }
        internal ImmutableArray<byte> RawBytes { get; }
        internal ImmutableArray<SourceField> OrderedFields { get; }
        internal ImmutableDictionary<string, long> Constructed { get; }
        internal int R2 { get; }
        internal int Subtype { get; }
        internal int Element { get; }
        internal bool ActivationReady => false;
        internal SelectedSpell(PinnedSkill p, NpcSkillBinding binding, PinnedBinding b, byte[] raw, ImmutableArray<SourceField> fields)
        {
            Id = p.Id; Name = p.Name; RawSha256 = p.Sha256; Binding = binding;
            R2 = p.R2; Subtype = p.Subtype; Element = p.Element;
            OrderedBindingFields = b.Fields; RawBytes = raw.ToImmutableArray(); OrderedFields = fields;
            Constructed = p.Constructed.ToImmutableDictionary(StringComparer.Ordinal);
        }
        internal ImmutableArray<SourceField> Occurrences(string name) => OrderedFields.Where(x => x.Name == name).ToImmutableArray();
    }
    internal sealed class NpcRecipe
    {
        internal int Id { get; }
        internal string Devname { get; }
        internal string RawSha256 { get; }
        internal int SourceLevel { get; }
        internal int SourceMinimum { get; }
        internal int SourceMaximum { get; }
        internal ImmutableArray<byte> RawBytes { get; }
        internal ImmutableArray<SourceField> OrderedFields { get; }
        internal int ConstructedDualMask => 0;
        internal int ConstructedExclusive450 => 0;
        internal int ConstructedMagicalTrim3f8 => 0;
        internal bool IsCurrentActorObservation => false;
        internal NpcRecipe(PinnedNpc p, byte[] raw, ImmutableArray<SourceField> fields)
        {
            Id = p.Id; Devname = p.Name; RawSha256 = p.Sha256; SourceLevel = p.Level;
            SourceMinimum = p.Minimum; SourceMaximum = p.Maximum;
            RawBytes = raw.ToImmutableArray(); OrderedFields = fields;
        }
    }

    internal static SelectedSpell ProjectSelectedSpell(ReadOnlySpan<byte> completeUtf16LeRecord,
        NpcSkillBinding binding, Pins pins)
    {
        Need(pins == RequiredPins, "spell_source_pins", "/source", "Pinned native/source/package context required");
        Need(binding is not null, "spell_binding", "/binding", "Exact original ordered binding is required");
        Need(completeUtf16LeRecord.Length is > 0 and <= 131072 && completeUtf16LeRecord.Length % 2 == 0,
            "spell_xml_bound", "/source", "Bounded complete UTF16LE record required before allocation");
        byte[] raw = completeUtf16LeRecord.ToArray(); var fields = Parse(raw, "skill_base");
        string hash = Convert.ToHexStringLower(SHA256.HashData(raw));
        PinnedSkill? p = Skills.FirstOrDefault(x => x.Sha256 == hash);
        Need(p is not null, "spell_source_hash", "/source", "Only the thirteen complete original record spans are admitted");
        Need(One(fields, "id") == p.Id.ToString(CultureInfo.InvariantCulture) && One(fields, "name") == p.Name,
            "spell_source_identity", "/source", "Original identity differs");
        PinnedBinding? b = p.Bindings.FirstOrDefault(x => x.NpcId == binding.NpcId && x.Devname == binding.NpcDevname
            && x.Ordinal == binding.ListOrdinal && x.Level == binding.SkillLevel && binding.SkillName == p.Name);
        Need(b is not null, "spell_source_binding", "/binding", "Exact ID/devname/list ordinal/skill level/name binding required");
        Need(fields.Where(x => x.Name.StartsWith("effect1_", StringComparison.Ordinal)).GroupBy(x => x.Name).All(x => x.Count() == 1),
            "spell_duplicate_contract", "/effect1", "Repeated contract fields cannot be first-selected");
        Need(One(fields, "effect1_type") == "SpellATK_Instant" && One(fields, "effect1_reserved1") == "0"
            && One(fields, "effect1_reserved2") == p.R2.ToString(CultureInfo.InvariantCulture)
            && One(fields, "effect1_reserved6") == "1" && One(fields, "effect1_critical_prob_mod2") == "100",
            "spell_source_contract", "/effect1", "Complete selected source recipe differs");
        return new(p, binding, b, raw, fields);
    }

    /// <summary>A new source binding for an unchanged globally proved skill record. The
    /// original pilot factory above deliberately retains its narrower rejection contract.</summary>
    internal static SelectedSpell ProjectCohortSpell(ReadOnlySpan<byte> completeUtf16LeRecord,
        SkyTempleNpcProfile profile, SkyTempleNpcSkill selected, Pins pins)
    {
        Need(pins == RequiredPins && profile is not null && selected is not null,
            "cohort_spell_inputs", "/source", "Known source pins and an actual cohort profile/row are required");
        var cohort = profile.Cohort;
        Need(cohort is not null && selected.Ordinal >= 0 && selected.Ordinal < profile.Skills.Length
            && profile.Skills[selected.Ordinal] == selected && selected.Level is > 0 and <= short.MaxValue,
            "cohort_spell_binding", "/binding", "Current profile-owned ordered skill row required");
        Need(completeUtf16LeRecord.Length is > 0 and <= 131072 && completeUtf16LeRecord.Length % 2 == 0,
            "spell_xml_bound", "/source", "Complete bounded UTF16LE source record required");
        byte[] raw = completeUtf16LeRecord.ToArray();
        string hash = Convert.ToHexStringLower(SHA256.HashData(raw));
        PinnedSkill? p = Skills.FirstOrDefault(x => x.Id == selected.Id48 && x.Sha256 == hash);
        Need(p is not null && cohort.SpellSources.TryGetValue(selected.Id48, out var held)
            && !held.IsDefault && held.AsSpan().SequenceEqual(raw),
            "cohort_spell_source", "/source", "One of the thirteen complete globally pinned records is required");
        var fields = Parse(raw, "skill_base");
        Need(One(fields, "id") == p.Id.ToString(CultureInfo.InvariantCulture)
            && One(fields, "name") == p.Name && selected.Name == p.Name,
            "cohort_spell_identity", "/source", "Raw skill identity and actual NPC binding differ");
        var entries = profile.Source.Skills.Entries;
        Need(selected.Ordinal < entries.Length, "cohort_spell_ordinal", "/binding", "Source row is missing");
        var source = entries[selected.Ordinal];
        Need(source.Ordinal == selected.Ordinal && source.Supported48Id == p.Id && source.Name.Value == p.Name,
            "cohort_spell_order", "/binding", "Source ID/name/ordinal differs");
        var ordered = RetailNpcSourceProfile.RawChildren(source.RawNode).Select(node =>
        {
            Need(node[2].EnumerateArray().All(x => x.ValueKind == System.Text.Json.JsonValueKind.String),
                "cohort_spell_row_shape", "/binding", "Only the admitted original leaf binding fields are supported");
            return new BindingField(node[0].GetString()!, RetailNpcSourceProfile.RawText(node));
        }).ToImmutableArray();
        string Row(string name)
        {
            var values = ordered.Where(x => x.Name == name).ToArray();
            Need(values.Length == 1, "cohort_spell_row_singleton", "/binding/" + name, "Exact original field required");
            return values[0].Text;
        }
        Need(Row("skill_name") == p.Name && Row("skill_level") == selected.Level.ToString(CultureInfo.InvariantCulture),
            "cohort_spell_row_value", "/binding", "Original ordered binding values differ");
        var binding = new NpcSkillBinding(profile.NpcId, profile.Devname, selected.Ordinal, selected.Level, p.Name);
        var bound = new PinnedBinding(profile.NpcId, profile.Devname, selected.Ordinal, selected.Level, ordered);
        return new(p, binding, bound, raw, fields);
    }

    internal static NpcRecipe ProjectNpcRecipe(ReadOnlySpan<byte> completeUtf16LeRecord, Pins pins)
    {
        Need(pins == RequiredPins, "npc_source_pins", "/source", "Pinned native/source/package context required");
        Need(completeUtf16LeRecord.Length is > 0 and <= 131072 && completeUtf16LeRecord.Length % 2 == 0,
            "spell_xml_bound", "/source", "Bounded complete UTF16LE record required before allocation");
        byte[] raw = completeUtf16LeRecord.ToArray(); var fields = Parse(raw, "npc");
        string hash = Convert.ToHexStringLower(SHA256.HashData(raw));
        PinnedNpc? p = Npcs.FirstOrDefault(x => x.Sha256 == hash);
        Need(p is not null, "npc_source_hash", "/source", "Only the twenty-five complete original pilot records are admitted");
        Need(One(fields, "id") == p.Id.ToString(CultureInfo.InvariantCulture) && One(fields, "name") == p.Name
            && One(fields, "dual") == "0", "npc_source_identity", "/source", "NPC identity/dual source differs");
        Need(!fields.Any(x => x.Name == "exclusive_attribute") && fields.Where(x => x.Name == "magical_damage_trim").All(x => x.Text == "0")
            && fields.Count(x => x.Name == "magical_damage_trim") <= 1, "npc_optional_source", "/source", "Optional source defaults differ");
        return new(p, raw, fields);
    }

    private static ImmutableArray<SourceField> Parse(byte[] raw, string rootName)
    {
        Need(raw.Length is > 0 and <= 131072 && raw.Length % 2 == 0,
            "spell_xml_bound", "/source", "Bounded complete UTF16LE record required");
        try
        {
            string xml = new UnicodeEncoding(false, false, true).GetString(raw);
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536,
                MaxCharactersFromEntities = 0, IgnoreComments = false, IgnoreProcessingInstructions = false
            });
            var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            XElement? root = document.Root;
            Need(root is not null && root.Name == rootName && !root.HasAttributes && !root.Descendants().Any(x => x.Ancestors().Count() > 24),
                "spell_xml_shape", "/source", "Canonical bounded original root required");
            return root.Elements().Select(x => new SourceField(x.Name.ToString(), x.Value, x.ToString(SaveOptions.DisableFormatting))).ToImmutableArray();
        }
        catch (Exception e) when (e is XmlException or DecoderFallbackException)
        { throw new ContractError("spell_xml", "/source", e.Message); }
    }
    private static string One(ImmutableArray<SourceField> fields, string name)
    {
        var matching = fields.Where(x => x.Name == name).ToArray();
        Need(matching.Length == 1, "spell_source_singleton", "/source/" + name, "Exactly one original field is required");
        return matching[0].Text;
    }
    private static readonly ImmutableArray<PinnedSkill> Skills =
    [
        new(17039, "GNMA_SpellLongTAKD_Air", "2c92704247e3f1fae19760bc6aa261e97b50074a106bd2f4704fdaabd8597f33", 23, 1, 1,
            [new("mask_d72", 0L), new("calculation_mask", 31L), new("noresist438", 0L), new("accuracy43c", 0L), new("accuracy440", 0L), new("critical_damage444_bits", 0L), new("critical_damage448_bits", 0L), new("critical_probability44c_bits", 0L), new("critical_probability450_bits", 1065353216L), new("skill168", 1L), new("skill16c", 1L), new("raw_R1_2d8", 0L), new("raw_R2_2dc", 23L), new("raw_R5_2e8", 0L), new("raw_R6_2ec", 1L), new("element458", 1L), new("additional45c", 0L), new("additional460", 0L), new("percent_flag464", 0L), new("amplitude4c4", 0L), new("amplitude_flags4c8", 1L), new("sharing4cc", 0L), new("wrapper_sharing_word", 1L), new("skill_exclusive_ec4", 0L), new("limit_attribute434", 1L), new("attenuation158", 0L), new("installed_attenuation_e10", 5380640448L), new("subtype154", 1L)],
            [new(212770, "Elementalair2D_30_An", 0, 21, [new("skill_name", "GNMA_SpellLongTAKD_Air"), new("skill_level", "21"), new("skill_rate", "0"), new("skill_count", "1")]), new(212771, "Elementalair2D_31_An", 0, 22, [new("skill_name", "GNMA_SpellLongTAKD_Air"), new("skill_level", "22"), new("skill_rate", "0"), new("skill_count", "1")]), new(212772, "Elementalair3D_32_An", 2, 23, [new("skill_name", "GNMA_SpellLongTAKD_Air"), new("skill_level", "23"), new("skill_rate", "0"), new("skill_count", "1")])]),
        new(16561, "NCL_Spell_Nr", "6b6fc9aa69957c68ac6bb3499d58c8f262d7151670e386d83dc1ca6da4293bf3", 135, 1, 3,
            [new("mask_d72", 0L), new("calculation_mask", 31L), new("noresist438", 0L), new("accuracy43c", 0L), new("accuracy440", 0L), new("critical_damage444_bits", 0L), new("critical_damage448_bits", 0L), new("critical_probability44c_bits", 0L), new("critical_probability450_bits", 1065353216L), new("skill168", 1L), new("skill16c", 1L), new("raw_R1_2d8", 0L), new("raw_R2_2dc", 135L), new("raw_R5_2e8", 0L), new("raw_R6_2ec", 1L), new("element458", 3L), new("additional45c", 0L), new("additional460", 0L), new("percent_flag464", 0L), new("amplitude4c4", 0L), new("amplitude_flags4c8", 1L), new("sharing4cc", 0L), new("wrapper_sharing_word", 1L), new("skill_exclusive_ec4", 0L), new("limit_attribute434", 1L), new("attenuation158", 0L), new("installed_attenuation_e10", 5380640448L), new("subtype154", 1L)],
            [new(212765, "Elementalearth1D_30_An", 0, 21, [new("skill_name", "NCL_Spell_Nr"), new("skill_level", "21"), new("skill_rate", "200"), new("skill_delay_time", "15000"), new("skill_count", "2")]), new(212766, "Elementalearth2D_30_An", 1, 21, [new("skill_name", "NCL_Spell_Nr"), new("skill_level", "21"), new("skill_rate", "200"), new("skill_delay_time", "15000"), new("skill_count", "2")]), new(212767, "Elementalearth2D_31_An", 1, 22, [new("skill_name", "NCL_Spell_Nr"), new("skill_level", "22"), new("skill_rate", "200"), new("skill_delay_time", "15000"), new("skill_count", "2")]), new(212768, "Elementalearth3D_31_An", 0, 22, [new("skill_name", "NCL_Spell_Nr"), new("skill_level", "22"), new("skill_rate", "0"), new("skill_count", "1")]), new(212835, "ElementalEarth3NamedD_32_Ae", 0, 23, [new("skill_name", "NCL_Spell_Nr"), new("skill_level", "23"), new("skill_rate", "0"), new("skill_count", "1")])]),
        new(16710, "NFI_SpellLong_Fire_Longtime", "01b5f3eeb0feaff4c1f083e628e217a4b2b5e62393705fdf0e27aa196da229c4", 77, 5, 3,
            [new("mask_d72", 0L), new("calculation_mask", 31L), new("noresist438", 0L), new("accuracy43c", 0L), new("accuracy440", 0L), new("critical_damage444_bits", 0L), new("critical_damage448_bits", 0L), new("critical_probability44c_bits", 0L), new("critical_probability450_bits", 1065353216L), new("skill168", 1L), new("skill16c", 1L), new("raw_R1_2d8", 0L), new("raw_R2_2dc", 77L), new("raw_R5_2e8", 0L), new("raw_R6_2ec", 1L), new("element458", 3L), new("additional45c", 0L), new("additional460", 0L), new("percent_flag464", 0L), new("amplitude4c4", 0L), new("amplitude_flags4c8", 1L), new("sharing4cc", 0L), new("wrapper_sharing_word", 1L), new("skill_exclusive_ec4", 0L), new("limit_attribute434", 1L), new("attenuation158", 0L), new("installed_attenuation_e10", 5380640448L), new("subtype154", 5L)],
            [new(212780, "ElementalFire4D_33_Ae", 3, 23, [new("skill_name", "NFI_SpellLong_Fire_Longtime"), new("skill_level", "23"), new("skill_rate", "0"), new("skill_count", "1")]), new(212876, "ElementalFire4NamedQ_33_Ae", 3, 24, [new("skill_name", "NFI_SpellLong_Fire_Longtime"), new("skill_level", "24"), new("skill_rate", "0"), new("skill_count", "1")])]),
        new(16576, "NMA_SpellLongSTA_Nr", "32ce3e1c1cf91fd7097be7c0e58c477a49e4ab3f554d2e6ee613b590267b4837", 99, 5, 3,
            [new("mask_d72", 0L), new("calculation_mask", 31L), new("noresist438", 0L), new("accuracy43c", 0L), new("accuracy440", 0L), new("critical_damage444_bits", 0L), new("critical_damage448_bits", 0L), new("critical_probability44c_bits", 0L), new("critical_probability450_bits", 1065353216L), new("skill168", 1L), new("skill16c", 1L), new("raw_R1_2d8", 0L), new("raw_R2_2dc", 99L), new("raw_R5_2e8", 0L), new("raw_R6_2ec", 1L), new("element458", 3L), new("additional45c", 0L), new("additional460", 0L), new("percent_flag464", 0L), new("amplitude4c4", 0L), new("amplitude_flags4c8", 1L), new("sharing4cc", 0L), new("wrapper_sharing_word", 1L), new("skill_exclusive_ec4", 0L), new("limit_attribute434", 1L), new("attenuation158", 0L), new("installed_attenuation_e10", 5380640448L), new("subtype154", 5L)],
            [new(212779, "SouledstoneAReD_31_Ae", 0, 22, [new("skill_name", "NMA_SpellLongSTA_Nr"), new("skill_level", "22"), new("skill_rate", "0"), new("skill_count", "2")]), new(212782, "SouledstoneAReBigS_32_Ae", 0, 24, [new("skill_name", "NMA_SpellLongSTA_Nr"), new("skill_level", "24"), new("skill_rate", "0"), new("skill_count", "2")])]),
        new(16590, "NPEL_SpellELLong_Water", "dda9218bf678c4b5dbc6598e83c7fab74817f85280ea205bf600cd3cb5ee543d", 92, 5, 0,
            [new("mask_d72", 0L), new("calculation_mask", 31L), new("noresist438", 0L), new("accuracy43c", 0L), new("accuracy440", 0L), new("critical_damage444_bits", 0L), new("critical_damage448_bits", 0L), new("critical_probability44c_bits", 0L), new("critical_probability450_bits", 1065353216L), new("skill168", 1L), new("skill16c", 1L), new("raw_R1_2d8", 0L), new("raw_R2_2dc", 92L), new("raw_R5_2e8", 0L), new("raw_R6_2ec", 1L), new("element458", 0L), new("additional45c", 0L), new("additional460", 0L), new("percent_flag464", 0L), new("amplitude4c4", 0L), new("amplitude_flags4c8", 1L), new("sharing4cc", 0L), new("wrapper_sharing_word", 1L), new("skill_exclusive_ec4", 0L), new("limit_attribute434", 1L), new("attenuation158", 0L), new("installed_attenuation_e10", 5380640448L), new("subtype154", 5L)],
            [new(212762, "Elementalwater2D_30_An", 0, 21, [new("skill_name", "NPEL_SpellELLong_Water"), new("skill_level", "21"), new("skill_rate", "0"), new("skill_count", "1")]), new(212763, "Elementalwater2D_31_An", 0, 22, [new("skill_name", "NPEL_SpellELLong_Water"), new("skill_level", "22"), new("skill_rate", "0"), new("skill_count", "1")])]),
        new(16594, "NPEL_SpellELSA_Air", "91fc8630304347466e4e0d094cae39f4e1621d2faa04d3d50b19408376c46ba4", 34, 5, 1,
            [new("mask_d72", 0L), new("calculation_mask", 31L), new("noresist438", 0L), new("accuracy43c", 0L), new("accuracy440", 0L), new("critical_damage444_bits", 0L), new("critical_damage448_bits", 0L), new("critical_probability44c_bits", 0L), new("critical_probability450_bits", 1065353216L), new("skill168", 1L), new("skill16c", 1L), new("raw_R1_2d8", 0L), new("raw_R2_2dc", 34L), new("raw_R5_2e8", 0L), new("raw_R6_2ec", 1L), new("element458", 1L), new("additional45c", 0L), new("additional460", 0L), new("percent_flag464", 0L), new("amplitude4c4", 0L), new("amplitude_flags4c8", 1L), new("sharing4cc", 0L), new("wrapper_sharing_word", 1L), new("skill_exclusive_ec4", 0L), new("limit_attribute434", 1L), new("attenuation158", 0L), new("installed_attenuation_e10", 5380640448L), new("subtype154", 5L)],
            [new(212772, "Elementalair3D_32_An", 1, 23, [new("skill_name", "NPEL_SpellELSA_Air"), new("skill_level", "23"), new("skill_rate", "0"), new("skill_count", "1")])]),
        new(16595, "NPEL_SpellELSA_Earth", "eacf1020e853915fe10ed5b8640ff0e142820e9b83521e41649def606f34d286", 34, 1, 2,
            [new("mask_d72", 0L), new("calculation_mask", 31L), new("noresist438", 0L), new("accuracy43c", 0L), new("accuracy440", 0L), new("critical_damage444_bits", 0L), new("critical_damage448_bits", 0L), new("critical_probability44c_bits", 0L), new("critical_probability450_bits", 1065353216L), new("skill168", 1L), new("skill16c", 1L), new("raw_R1_2d8", 0L), new("raw_R2_2dc", 34L), new("raw_R5_2e8", 0L), new("raw_R6_2ec", 1L), new("element458", 2L), new("additional45c", 0L), new("additional460", 0L), new("percent_flag464", 0L), new("amplitude4c4", 0L), new("amplitude_flags4c8", 1L), new("sharing4cc", 0L), new("wrapper_sharing_word", 1L), new("skill_exclusive_ec4", 0L), new("limit_attribute434", 1L), new("attenuation158", 0L), new("installed_attenuation_e10", 5380640448L), new("subtype154", 1L)],
            [new(212768, "Elementalearth3D_31_An", 1, 22, [new("skill_name", "NPEL_SpellELSA_Earth"), new("skill_level", "22"), new("skill_rate", "0"), new("skill_count", "1")]), new(212835, "ElementalEarth3NamedD_32_Ae", 1, 23, [new("skill_name", "NPEL_SpellELSA_Earth"), new("skill_level", "23"), new("skill_rate", "0"), new("skill_count", "1")])]),
        new(16592, "NPEL_SpellELSA_Fire", "4cf1a259d28220334c3b9720be21e568da1dfcdfc3fc70735685f3808ba2e1c6", 99, 5, 3,
            [new("mask_d72", 0L), new("calculation_mask", 31L), new("noresist438", 0L), new("accuracy43c", 0L), new("accuracy440", 0L), new("critical_damage444_bits", 0L), new("critical_damage448_bits", 0L), new("critical_probability44c_bits", 0L), new("critical_probability450_bits", 1065353216L), new("skill168", 1L), new("skill16c", 1L), new("raw_R1_2d8", 0L), new("raw_R2_2dc", 99L), new("raw_R5_2e8", 0L), new("raw_R6_2ec", 1L), new("element458", 3L), new("additional45c", 0L), new("additional460", 0L), new("percent_flag464", 0L), new("amplitude4c4", 0L), new("amplitude_flags4c8", 1L), new("sharing4cc", 0L), new("wrapper_sharing_word", 1L), new("skill_exclusive_ec4", 0L), new("limit_attribute434", 1L), new("attenuation158", 0L), new("installed_attenuation_e10", 5380640448L), new("subtype154", 5L)],
            [new(212777, "ElementalFire3D_31_Ae", 1, 22, [new("skill_name", "NPEL_SpellELSA_Fire"), new("skill_level", "22"), new("skill_rate", "0"), new("skill_count", "1")]), new(212778, "ElementalFire3D_32_Ae", 1, 23, [new("skill_name", "NPEL_SpellELSA_Fire"), new("skill_level", "23"), new("skill_rate", "0"), new("skill_count", "1")]), new(212780, "ElementalFire4D_33_Ae", 1, 23, [new("skill_name", "NPEL_SpellELSA_Fire"), new("skill_level", "23"), new("skill_rate", "0"), new("skill_count", "1")]), new(212876, "ElementalFire4NamedQ_33_Ae", 1, 24, [new("skill_name", "NPEL_SpellELSA_Fire"), new("skill_level", "24"), new("skill_rate", "0"), new("skill_count", "1")])]),
        new(16593, "NPEL_SpellELSA_Water", "40285bfb6a13c8f57ac3558db162962b9e7d091fde6c6baff81afb246698b9c5", 34, 5, 0,
            [new("mask_d72", 0L), new("calculation_mask", 31L), new("noresist438", 0L), new("accuracy43c", 0L), new("accuracy440", 0L), new("critical_damage444_bits", 0L), new("critical_damage448_bits", 0L), new("critical_probability44c_bits", 0L), new("critical_probability450_bits", 1065353216L), new("skill168", 1L), new("skill16c", 1L), new("raw_R1_2d8", 0L), new("raw_R2_2dc", 34L), new("raw_R5_2e8", 0L), new("raw_R6_2ec", 1L), new("element458", 0L), new("additional45c", 0L), new("additional460", 0L), new("percent_flag464", 0L), new("amplitude4c4", 0L), new("amplitude_flags4c8", 1L), new("sharing4cc", 0L), new("wrapper_sharing_word", 1L), new("skill_exclusive_ec4", 0L), new("limit_attribute434", 1L), new("attenuation158", 0L), new("installed_attenuation_e10", 5380640448L), new("subtype154", 5L)],
            [new(212764, "Elementalwater3D_31_An", 1, 22, [new("skill_name", "NPEL_SpellELSA_Water"), new("skill_level", "22"), new("skill_rate", "0"), new("skill_count", "1")]), new(212833, "Elementalwater3NMDQ_31_An", 1, 22, [new("skill_name", "NPEL_SpellELSA_Water"), new("skill_level", "22"), new("skill_rate", "0"), new("skill_count", "1")])]),
        new(16589, "NPEL_SpellEL_Fire", "2f043df18e8a02daab6b5fc5c6fc87e597b9fb1ad7dbf9394b1aa24f440565b6", 154, 5, 3,
            [new("mask_d72", 0L), new("calculation_mask", 31L), new("noresist438", 0L), new("accuracy43c", 0L), new("accuracy440", 0L), new("critical_damage444_bits", 0L), new("critical_damage448_bits", 0L), new("critical_probability44c_bits", 0L), new("critical_probability450_bits", 1065353216L), new("skill168", 1L), new("skill16c", 1L), new("raw_R1_2d8", 0L), new("raw_R2_2dc", 154L), new("raw_R5_2e8", 0L), new("raw_R6_2ec", 1L), new("element458", 3L), new("additional45c", 0L), new("additional460", 0L), new("percent_flag464", 0L), new("amplitude4c4", 0L), new("amplitude_flags4c8", 1L), new("sharing4cc", 0L), new("wrapper_sharing_word", 1L), new("skill_exclusive_ec4", 0L), new("limit_attribute434", 1L), new("attenuation158", 0L), new("installed_attenuation_e10", 5380640448L), new("subtype154", 5L)],
            [new(212774, "ElementalFire2D_30_Ae", 0, 21, [new("skill_name", "NPEL_SpellEL_Fire"), new("skill_level", "21"), new("skill_rate", "0"), new("skill_count", "1")]), new(212775, "ElementalFire2D_31_Ae", 0, 22, [new("skill_name", "NPEL_SpellEL_Fire"), new("skill_level", "22"), new("skill_rate", "0"), new("skill_count", "1")]), new(212779, "SouledstoneAReD_31_Ae", 1, 22, [new("skill_name", "NPEL_SpellEL_Fire"), new("skill_level", "22"), new("skill_rate", "150"), new("skill_delay_time", "12000"), new("skill_count", "3")]), new(212782, "SouledstoneAReBigS_32_Ae", 1, 24, [new("skill_name", "NPEL_SpellEL_Fire"), new("skill_level", "24"), new("skill_rate", "150"), new("skill_delay_time", "12000"), new("skill_count", "3")])]),
        new(16724, "NPEL_SpellLong_Water", "0ea9ade918fb05aed8927cae53ee6a2a86ead050c602519a93d901120c3cd012", 115, 1, 0,
            [new("mask_d72", 0L), new("calculation_mask", 31L), new("noresist438", 0L), new("accuracy43c", 0L), new("accuracy440", 0L), new("critical_damage444_bits", 0L), new("critical_damage448_bits", 0L), new("critical_probability44c_bits", 0L), new("critical_probability450_bits", 1065353216L), new("skill168", 1L), new("skill16c", 1L), new("raw_R1_2d8", 0L), new("raw_R2_2dc", 115L), new("raw_R5_2e8", 0L), new("raw_R6_2ec", 1L), new("element458", 0L), new("additional45c", 0L), new("additional460", 0L), new("percent_flag464", 0L), new("amplitude4c4", 0L), new("amplitude_flags4c8", 1L), new("sharing4cc", 0L), new("wrapper_sharing_word", 1L), new("skill_exclusive_ec4", 0L), new("limit_attribute434", 1L), new("attenuation158", 0L), new("installed_attenuation_e10", 5380640448L), new("subtype154", 1L)],
            [new(212761, "Elementalwater1D_30_An", 0, 21, [new("skill_name", "NPEL_SpellLong_Water"), new("skill_level", "21"), new("skill_rate", "200"), new("skill_delay_time", "0"), new("skill_count", "2")]), new(212762, "Elementalwater2D_30_An", 1, 21, [new("skill_name", "NPEL_SpellLong_Water"), new("skill_level", "21"), new("skill_rate", "200"), new("skill_delay_time", "15000")]), new(212763, "Elementalwater2D_31_An", 1, 22, [new("skill_name", "NPEL_SpellLong_Water"), new("skill_level", "22"), new("skill_rate", "200"), new("skill_delay_time", "15000")]), new(212764, "Elementalwater3D_31_An", 0, 22, [new("skill_name", "NPEL_SpellLong_Water"), new("skill_level", "22"), new("skill_rate", "0"), new("skill_count", "1")]), new(212833, "Elementalwater3NMDQ_31_An", 0, 22, [new("skill_name", "NPEL_SpellLong_Water"), new("skill_level", "22"), new("skill_rate", "0"), new("skill_count", "1")])]),
        new(16725, "NPEL_Spell_Air", "a6ef6050dfd6f9030781c2ec78e6f912277fa3ae496caf7c521df37c7cd6bec5", 135, 1, 1,
            [new("mask_d72", 0L), new("calculation_mask", 31L), new("noresist438", 0L), new("accuracy43c", 0L), new("accuracy440", 0L), new("critical_damage444_bits", 0L), new("critical_damage448_bits", 0L), new("critical_probability44c_bits", 0L), new("critical_probability450_bits", 1065353216L), new("skill168", 1L), new("skill16c", 1L), new("raw_R1_2d8", 0L), new("raw_R2_2dc", 135L), new("raw_R5_2e8", 0L), new("raw_R6_2ec", 1L), new("element458", 1L), new("additional45c", 0L), new("additional460", 0L), new("percent_flag464", 0L), new("amplitude4c4", 0L), new("amplitude_flags4c8", 1L), new("sharing4cc", 0L), new("wrapper_sharing_word", 1L), new("skill_exclusive_ec4", 0L), new("limit_attribute434", 1L), new("attenuation158", 0L), new("installed_attenuation_e10", 5380640448L), new("subtype154", 1L)],
            [new(212769, "Elementalair1D_30_An", 0, 21, [new("skill_name", "NPEL_Spell_Air"), new("skill_level", "21"), new("skill_rate", "200"), new("skill_delay_time", "17000"), new("skill_count", "2")]), new(212770, "Elementalair2D_30_An", 1, 21, [new("skill_name", "NPEL_Spell_Air"), new("skill_level", "21"), new("skill_rate", "200"), new("skill_delay_time", "17000"), new("skill_count", "2")]), new(212771, "Elementalair2D_31_An", 1, 22, [new("skill_name", "NPEL_Spell_Air"), new("skill_level", "22"), new("skill_rate", "200"), new("skill_delay_time", "17000"), new("skill_count", "2")]), new(212772, "Elementalair3D_32_An", 0, 23, [new("skill_name", "NPEL_Spell_Air"), new("skill_level", "23"), new("skill_rate", "0"), new("skill_count", "1")])]),
        new(16723, "NPEL_Spell_Fire", "299b1c709505e8c63d1eccc86ef31963bfa1d83bb47fa052a4bd8bdb903e4137", 135, 1, 3,
            [new("mask_d72", 0L), new("calculation_mask", 31L), new("noresist438", 0L), new("accuracy43c", 0L), new("accuracy440", 0L), new("critical_damage444_bits", 0L), new("critical_damage448_bits", 0L), new("critical_probability44c_bits", 0L), new("critical_probability450_bits", 1065353216L), new("skill168", 1L), new("skill16c", 1L), new("raw_R1_2d8", 0L), new("raw_R2_2dc", 135L), new("raw_R5_2e8", 0L), new("raw_R6_2ec", 1L), new("element458", 3L), new("additional45c", 0L), new("additional460", 0L), new("percent_flag464", 0L), new("amplitude4c4", 0L), new("amplitude_flags4c8", 1L), new("sharing4cc", 0L), new("wrapper_sharing_word", 1L), new("skill_exclusive_ec4", 0L), new("limit_attribute434", 1L), new("attenuation158", 0L), new("installed_attenuation_e10", 5380640448L), new("subtype154", 1L)],
            [new(212773, "ElementalFire1D_30_Ae", 0, 21, [new("skill_name", "NPEL_Spell_Fire"), new("skill_level", "21"), new("skill_rate", "200"), new("skill_delay_time", "10000")]), new(212774, "ElementalFire2D_30_Ae", 1, 21, [new("skill_name", "NPEL_Spell_Fire"), new("skill_level", "21"), new("skill_rate", "200"), new("skill_delay_time", "15000")]), new(212775, "ElementalFire2D_31_Ae", 1, 22, [new("skill_name", "NPEL_Spell_Fire"), new("skill_level", "22"), new("skill_rate", "200"), new("skill_delay_time", "15000")]), new(212777, "ElementalFire3D_31_Ae", 0, 22, [new("skill_name", "NPEL_Spell_Fire"), new("skill_level", "22"), new("skill_rate", "0"), new("skill_count", "1")]), new(212778, "ElementalFire3D_32_Ae", 0, 23, [new("skill_name", "NPEL_Spell_Fire"), new("skill_level", "23"), new("skill_rate", "0"), new("skill_count", "1")]), new(212780, "ElementalFire4D_33_Ae", 0, 23, [new("skill_name", "NPEL_Spell_Fire"), new("skill_level", "23"), new("skill_rate", "0"), new("skill_count", "1")]), new(212876, "ElementalFire4NamedQ_33_Ae", 0, 24, [new("skill_name", "NPEL_Spell_Fire"), new("skill_level", "24"), new("skill_rate", "0"), new("skill_count", "1")])])
    ];
    private static readonly ImmutableArray<PinnedNpc> Npcs =
    [
        new(212761, "Elementalwater1D_30_An", "5e922a1b6a45f05a97f7e8f1f8470db8a442b63726800956d3accc2ab1fc2a7e", 30, 226, 339),
        new(212762, "Elementalwater2D_30_An", "4ef64004f51dbf30ad436f59e22a20d757aecdbab62e1c7599ed11bd2499493f", 30, 226, 339),
        new(212763, "Elementalwater2D_31_An", "f97f8ce03691345573050b51eb98068570ccd19a6e821f24040656b1a2e47b53", 31, 236, 355),
        new(212764, "Elementalwater3D_31_An", "2286e543e752c1e1e77f44cbb4f03dd2292d4161b018a71857632357770500c8", 31, 236, 355),
        new(212765, "Elementalearth1D_30_An", "fc1aba5650958628616c9fbaee1ebe222603573a208e64a43d68e233d12cc0f0", 30, 152, 228),
        new(212766, "Elementalearth2D_30_An", "67906fb717448503fd1c826bfd763cf3a2819e69334603f3b19c3007913d0873", 30, 152, 228),
        new(212767, "Elementalearth2D_31_An", "8066d649e88cd21ca27287327e60718f58c1e6ba24de5effa2c84c4ce5c4e57d", 31, 159, 239),
        new(212768, "Elementalearth3D_31_An", "b5fcf0114a49355fd6cd3702e2cb2e2836bf46429f430a99e9f39c9a62542da2", 31, 159, 239),
        new(212769, "Elementalair1D_30_An", "bdff66ff66254003bd5f823c50b4ee85f5b377fde45b16b92733ee7bb3fe384f", 30, 181, 272),
        new(212770, "Elementalair2D_30_An", "93d3a604c96f6236fcfb1cf59d5db71613a365cd54cdae7e36c65485e46afd78", 30, 181, 272),
        new(212771, "Elementalair2D_31_An", "1173136bf414a29ed48625f3c82264dd3ffa005a4e04a2895fd32498a321ee31", 31, 189, 284),
        new(212772, "Elementalair3D_32_An", "2387124fc6b7d0d0f1cb93cc03d533410f7ef1cb6ae8e31865b2580f81e62476", 32, 196, 294),
        new(212773, "ElementalFire1D_30_Ae", "c2a94af3ee50134c2bf4a235d7a6f54549b3020bc17d0e285ec7cce720d128eb", 30, 154, 231),
        new(212774, "ElementalFire2D_30_Ae", "e686e486a7b5b7880757340c93f68c7917ec8389b0dc77d969f93fe8be6cdade", 30, 154, 231),
        new(212775, "ElementalFire2D_31_Ae", "5f42b4cc3f0ac6e3d678465d9bd3679d9dd1a7099b4bcf46658e9558f1c28d6a", 31, 160, 240),
        new(212776, "ShadowstalkerD_30_Ae", "cbbf5eef68a0d1f2a39322bd5a598d0d94db0664e31b6b3b90a15e6ca208a81f", 30, 181, 272),
        new(212777, "ElementalFire3D_31_Ae", "5bb00272cf90e69c43535ad884753105153ee3947f716fa8764ea99a852e13d1", 31, 160, 240),
        new(212778, "ElementalFire3D_32_Ae", "004309624e2f1ba00c87992181767c4f5f54aea0e083d35ebf37927d4a69575f", 32, 166, 249),
        new(212779, "SouledstoneAReD_31_Ae", "fef2d6bb66f4d427401431c68a500df307aeed436a1744bf59873a79956922b1", 31, 183, 275),
        new(212780, "ElementalFire4D_33_Ae", "23832cdd4abb0e092afb8daa58787035d9040205b9b8c3e723c2e76bdcd71185", 33, 173, 260),
        new(212781, "ShadowstalkerBigD_32_Ae", "bc16907e410cf71fb6e00471a27d1cb43bfca8e544d7cd5d5d414d864cfe0dff", 31, 189, 284),
        new(212782, "SouledstoneAReBigS_32_Ae", "fba425cd99cd0f1a358967bfd33e1eee80810eb2552cdd0a480f21e81c38783b", 32, 189, 284),
        new(212833, "Elementalwater3NMDQ_31_An", "279beb03e5e55b8af062359aa424551dc3347061cdc082a20238090e55c883b2", 31, 355, 516),
        new(212835, "ElementalEarth3NamedD_32_Ae", "e1c723e52067779b9d7487ecbfff5e4dd97966f4f64132a4bb992f02e5e08460", 32, 206, 300),
        new(212876, "ElementalFire4NamedQ_33_Ae", "91bb82ad05d86a836931490361ee974a1476434d2a73a8694ec6b1285078e7ca", 33, 216, 315)
    ];
}
