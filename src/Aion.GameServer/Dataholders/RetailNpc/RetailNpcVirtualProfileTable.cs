using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using static Aion.GameServer.Dataholders.RetailNpc.RetailNpcVirtualGuard;

namespace Aion.GameServer.Dataholders.RetailNpc;

/// <summary>Bounded atomic source admission; stricter nine-rank/ASCII/canonical CRT subset is managed policy.</summary>
public sealed class RetailNpcVirtualProfileTable
{
    private RetailNpcVirtualProfileTable(byte[] bytes, RetailNpcVirtualTableReceipt receipt,
        IEnumerable<RetailNpcVirtualDescriptor> records, IEnumerable<string> names)
    { SourceBytes = bytes.ToImmutableArray(); Receipt = receipt; Records = records.ToImmutableArray(); Names = names.ToImmutableArray(); }
    public ImmutableArray<byte> SourceBytes { get; }
    public RetailNpcVirtualTableReceipt Receipt { get; }
    public ImmutableArray<RetailNpcVirtualDescriptor> Records { get; }
    public ImmutableArray<string> Names { get; }
    public bool StaticSourceAdmitted => true;
    public bool RuntimeObservationVerified => false;
    public bool ActivationReady => false;
    private static readonly Regex Number = new(@"^[+-]?[0-9]+%?$", RegexOptions.CultureInvariant);
    internal static readonly ImmutableArray<int> PairOffsets = Enumerable.Range(0, 22).Select(i => i * 8)
        .Concat(Enumerable.Range(0, 10).Select(i => 0xc0 + i * 8)).ToImmutableArray();
    internal static readonly FrozenDictionary<string, int> FieldOffsets = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["attack_delay"]=0x78,["attack_range"]=0x70,["block"]=0xa8,["cash"]=0xc8,["cash_rate"]=0xc0,
        ["common_drop"]=0xd0,["concentration"]=0x100,["critical"]=0x90,["dodge"]=0x98,
        ["ed_water_bonus"]=0,["ed_air_bonus"]=8,["ed_earth_bonus"]=16,["ed_fire_bonus"]=24,["ed_light_bonus"]=32,["ed_dark_bonus"]=40,
        ["er_water_bonus"]=0,["er_air_bonus"]=8,["er_earth_bonus"]=16,["er_fire_bonus"]=24,["er_light_bonus"]=32,["er_dark_bonus"]=40,
        ["exp"]=0xb0,["general_drop"]=0xe0,["hit_accuracy"]=0x80,["hit_count"]=0x88,["hp_regen"]=0x38,
        ["magical_critical"]=0xf8,["magical_defend"]=0x60,["magical_hit_accuracy"]=0x108,["magical_resist"]=0x68,
        ["max_damage"]=0xf0,["max_hp"]=0x30,["max_mp"]=0x40,["min_damage"]=0xe8,["mp_regen"]=0x48,
        ["parry"]=0xa0,["physical_defend"]=0x58,["rare_drop"]=0xd8,["speed"]=0x50
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static RetailNpcVirtualResult<RetailNpcVirtualProfileTable> Admit(ReadOnlySpan<byte> source, RetailNpcVirtualTablePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        try
        {
            foreach (string hash in new[] { policy.DatasetId, policy.OutputManifestSha256, policy.PackageContextSha256, policy.ExpectedSourceSha256 }) Hash(hash);
            Need(policy.MaximumBytes is > 0 and <= 4 * 1024 * 1024 && policy.MaximumCharacters is > 0 and <= 2 * 1024 * 1024
                && policy.MaximumEntityCharacters is > 0 and <= 65536, "virtual_table_budget", "Explicit bounded XML limits required");
            Need(source.Length == policy.ExpectedSourceBytes && source.Length > 0 && source.Length <= policy.MaximumBytes,
                "virtual_table_hash", "Whole source length differs");
            byte[] bytes = source.ToArray();
            Need(Digest(bytes) == policy.ExpectedSourceSha256, "virtual_table_hash", "Owned whole-source hash differs");
            Need(Text(policy.SourceIdentity, 4096) && Text(policy.Region, 32), "virtual_table_source", "Source identity and region required");
            var regions = new[] { "USA", "Europe", "Asian", "Japan", "China", "Taiwan", "Russia", "L10N" };
            Need(regions.Contains(policy.Region, StringComparer.Ordinal), "virtual_table_region", "Unsupported canonical region");
            string relative = policy.Selection == RetailNpcVirtualTableSelection.RegionalOverride ? policy.Region + "/DynamicCreation.xml" : "DynamicCreation.xml";
            Need(Enum.IsDefined(policy.Selection) && policy.SelectedRelativePath == relative, "virtual_table_selection", "Pinned dataset-relative table selection required; no traversal/provenance path is opened");
            using var stream = new MemoryStream(bytes, false);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null,
                MaxCharactersInDocument = policy.MaximumCharacters, MaxCharactersFromEntities = policy.MaximumEntityCharacters });
            var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            Need(document.DocumentType is null || string.IsNullOrEmpty(document.DocumentType.SystemId)
                && string.IsNullOrEmpty(document.DocumentType.PublicId)
                && !Regex.IsMatch(document.DocumentType.InternalSubset ?? "", @"\b(SYSTEM|PUBLIC)\b", RegexOptions.CultureInvariant),
                "virtual_table_external_entity", "External DTD/entity declarations are unsupported");
            var root = document.Root;
            Need(root is not null && root.Name == "dynamiccreations" && !root.HasAttributes,
                "virtual_table_root", "Canonical dynamiccreations root required");
            Need(document.Descendants().All(x => x.Ancestors().Count() <= 32), "virtual_table_depth", "XML depth exceeds support bound");
            var names = new List<string>(); var records = new List<RetailNpcVirtualDescriptor>(); var keys = new HashSet<(int, byte)>();
            foreach (var node in root.Elements())
            {
                Need(records.Count < 2259 && node.Name == "attr_set_name" && node.Attributes().Count() == 2
                    && node.Attributes().All(a => a.Name == "attr_set_name" || a.Name == "rank"), "virtual_table_record", "Canonical bounded attr_set_name record required");
                string name = node.Attribute("attr_set_name")?.Value ?? "";
                Need(name.Length is > 0 and <= 80 && name.All(c => c is >= '!' and <= '~'), "virtual_table_name", "Bounded nonempty ASCII name required");
                string rankText = node.Attribute("rank")?.Value ?? "";
                Need(Regex.IsMatch(rankText, @"^[1-9]$", RegexOptions.CultureInvariant), "virtual_table_rank", "Canonical rank1..9 required");
                byte rank = byte.Parse(rankText, CultureInfo.InvariantCulture);
                int index = names.FindIndex(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (index < 0) { Need(names.Count < 251, "virtual_table_capacity", "Native name capacity251 exceeded"); names.Add(name); index = names.Count - 1; }
                Need(keys.Add((index + 1, rank)), "virtual_table_duplicate_key", "Duplicate native name/rank key rejects the whole source");
                var pairs = PairOffsets.ToDictionary(x => x, _ => new RetailNpcVirtualPair(0, 100));
                long expFlat = 0; int expPercent = 100; var occurrences = new List<RetailNpcVirtualOccurrence>();
                foreach (var child in node.Elements())
                {
                    Need(occurrences.Count < 256, "virtual_table_field_count", "Too many ordered descriptor fields");
                    string tag = child.Name.LocalName;
                    Need(child.Name.NamespaceName.Length == 0 && (FieldOffsets.ContainsKey(tag) || tag is "physical_attack" or "magical_attack"),
                        "virtual_table_unknown_field", "Unknown native field blocks strict admission: " + tag);
                    string raw = child.ToString(SaveOptions.DisableFormatting);
                    if (tag is "physical_attack" or "magical_attack")
                    { occurrences.Add(new(occurrences.Count, tag, child.Value, raw, null, true, false, null)); continue; }
                    Need(!child.HasElements && !child.HasAttributes && child.Value.Length <= 128, "virtual_table_scalar", "Plain bounded scalar required");
                    string lexeme = child.Value;
                    Need(Number.IsMatch(lexeme), "virtual_table_lexeme", "Unsupported CRT spelling, empty text or noninteger");
                    bool percent = lexeme.EndsWith('%'); string digits = percent ? lexeme[..^1] : lexeme;
                    Need(long.TryParse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value)
                        && (tag == "exp" || value is >= int.MinValue and <= int.MaxValue), "virtual_table_width", "Signed field width overflow");
                    int offset = FieldOffsets[tag];
                    if (tag == "exp") { expFlat = percent ? 0 : value; expPercent = percent ? unchecked((int)value) : 0; }
                    else pairs[offset] = new(percent ? 0 : (int)value, percent ? (int)value : 0);
                    occurrences.Add(new(occurrences.Count, tag, lexeme, raw, offset, false, percent, value));
                }
                Need(node.Nodes().OfType<XText>().All(x => string.IsNullOrWhiteSpace(x.Value)), "virtual_table_text", "Unexpected record text");
                byte[] image = new byte[0x110];
                foreach (var pair in pairs) { BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(pair.Key), pair.Value.Flat); BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(pair.Key + 4), pair.Value.Percent); }
                BinaryPrimitives.WriteInt64LittleEndian(image.AsSpan(0xb0), expFlat); BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(0xb8), expPercent);
                // Padding is zero only in this canonical semantic carrier; never an allocation observation.
                records.Add(new(records.Count, name, rank, index + 1, node.ToString(SaveOptions.DisableFormatting), pairs, expFlat, expPercent, occurrences, image, Digest(image)));
            }
            Need(records.Count > 0 && names.Count > 0 && root.Nodes().OfType<XText>().All(x => string.IsNullOrWhiteSpace(x.Value)), "virtual_table_empty", "Nonempty canonical table required");
            Need(records.Count == names.Count * 9 && names.Select((_, i) => i + 1).All(i => Enumerable.Range(1, 9).All(r => keys.Contains((i, (byte)r)))),
                "virtual_table_rank_coverage", "Managed admission requires all nine keys per name; native NULL/sentinel check does not guarantee this");
            var receipt = new RetailNpcVirtualTableReceipt(policy.DatasetId, policy.OutputManifestSha256, policy.Region,
                RetailNpcTemplateProjection.MainBinarySha256, policy.PackageContextSha256, policy.ExpectedSourceSha256, bytes.Length,
                policy.SourceIdentity, policy.SelectedRelativePath, policy.Selection);
            return new(new(bytes, receipt, records, names), []);
        }
        catch (Exception e) when (e is RetailNpcVirtualError or XmlException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
        { return Failure<RetailNpcVirtualProfileTable>(e); }
    }

    public RetailNpcVirtualResult<RetailNpcVirtualSelection> Select(string name, byte rank, RetailNpcVirtualRegistryEvidence registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        try
        {
            Hash(registry.ContentSha256); Need(Text(registry.Epoch, 256) && Text(registry.Evidence, 4096), "virtual_registry_epoch", "Explicit registry epoch/evidence required");
            Need(registry.Origin is RetailNpcModifierInputOrigin.FixedMathematicalFixture or RetailNpcModifierInputOrigin.ExternalActorSnapshot,
                "virtual_registry_origin", "Unsupported registry origin");
            Need(registry.Origin == RetailNpcModifierInputOrigin.FixedMathematicalFixture ? registry.ActorObjectIdentity is null : Text(registry.ActorObjectIdentity, 256),
                "virtual_registry_actor", "Registry actor assertion differs from origin");
            Need(!registry.Entries.IsDefault && registry.Entries.Length == Names.Length && registry.Entries.All(e => e is not null && e.NativeNameId is >= 1 and <= 251
                && Names.Contains(e.Name, StringComparer.OrdinalIgnoreCase)) && registry.Entries.Select(e => e.NativeNameId).Distinct().Count() == Names.Length
                && registry.Entries.Select(e => e.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == Names.Length, "virtual_registry_coverage", "Complete unique name/ID mapping required");
            Need(RegistryHash(registry.Entries) == registry.ContentSha256, "virtual_registry_hash", "Registry canonical hash differs");
            if (registry.Origin == RetailNpcModifierInputOrigin.FixedMathematicalFixture)
                Need(Names.Select((n, i) => registry.Entries.Single(e => e.Name.Equals(n, StringComparison.OrdinalIgnoreCase)).NativeNameId == i + 1).All(x => x),
                    "virtual_registry_fixture", "Fresh-source mathematical registry must retain first-seen IDs");
            var record = Records.SingleOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && x.Rank == rank);
            Need(record is not null, "virtual_profile_key", "Required selected name/rank absent");
            int id = registry.Entries.Single(x => x.Name.Equals(record.Name, StringComparison.OrdinalIgnoreCase)).NativeNameId;
            return new(new(this, record, registry, id), []);
        }
        catch (Exception e) when (e is RetailNpcVirtualError or ArgumentException or InvalidOperationException) { return Failure<RetailNpcVirtualSelection>(e); }
    }
    public static string RegistryHash(ImmutableArray<RetailNpcVirtualRegistryEntry> entries)
    {
        Need(!entries.IsDefault && entries.Length <= 251 && entries.All(e => e is not null && e.Name is { Length: > 0 and <= 80 }
            && e.Name.All(c => c is >= '!' and <= '~')), "virtual_registry_bound", "Bounded ASCII registry carrier required");
        using var bytes = new MemoryStream(); using var writer = new BinaryWriter(bytes, new UTF8Encoding(false, true), true);
        foreach (var e in entries.OrderBy(x => x.NativeNameId)) { writer.Write(e.NativeNameId); writer.Write(e.Name.ToLowerInvariant()); }
        writer.Flush(); return Digest(bytes.ToArray());
    }
    public static string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
