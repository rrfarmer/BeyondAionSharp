using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using static Aion.GameServer.Dataholders.RetailNpc.RetailNpcDatasetValidation;

namespace Aion.GameServer.Dataholders.RetailNpc;

/// <summary>Immutable static identity evidence. This does not verify stats, skills or an effective actor.</summary>
public sealed class RetailNpcReviewedIdentityReceipt
{
    internal RetailNpcReviewedIdentityReceipt(int id, string clientName, string retailName, string sourcePath,
        long start, long end, string recordSha, string clientCanonicalSha, string previousLevel, string retailLevel,
        FrozenDictionary<string, string> identityFields)
    {
        NpcId = id; Client48Devname = clientName; Retail58Devname = retailName; SourcePath = sourcePath;
        ByteStart = start; ByteEndExclusive = end; RawRecordSha256 = recordSha;
        Client48CanonicalRecordSha256 = clientCanonicalSha; PreviousLevel48 = previousLevel;
        RetailLevel58 = retailLevel; IdentityFields = identityFields;
    }
    public int NpcId { get; }
    public string Client48Devname { get; }
    public string Retail58Devname { get; }
    public string SourcePath { get; }
    public long ByteStart { get; }
    public long ByteEndExclusive { get; }
    public string RawRecordSha256 { get; }
    /// <summary>Hash of the decoded canonical client record; not an original BXML byte span.</summary>
    public string Client48CanonicalRecordSha256 { get; }
    public string PreviousLevel48 { get; }
    public string RetailLevel58 { get; }
    public FrozenDictionary<string, string> IdentityFields { get; }
    public string ReviewSha256 => RetailNpcReviewedIdentityBindings.ReviewSha256;
    public string Client48ArchiveSha256 => RetailNpcReviewedIdentityBindings.Client48ArchiveSha256;
    public string SourceSha256 => RetailNpcReviewedIdentityBindings.RetailSourceSha256;
    public bool EffectiveProfileVerified => false;
    public bool SkillCompatibilityVerified => false;
    public bool ActivationReady => false;
}

/// <summary>
/// The complete pinned 90-row retained-ID review, not a general alias dictionary.
/// Only Admit can construct this immutable catalog. Provenance paths are opaque labels.
/// </summary>
public sealed class RetailNpcReviewedIdentityBindings
{
    public const string ReviewSha256 = "f90650eda2f22bf9e04d81a02a716fc992cbad3a186edfcefc793b54aecd3ecf";
    public const string Client48ArchiveSha256 = "6934fae2ba6edefe14659de13add33101e6f0231634fd553d359e1f6fabfce43";
    public const string RetailSourceSha256 = "e51dc42fd01b8f0cb2b2f1cd03fa15ec473202efb8079e7006f60119a68faee1";
    public const string MetadataSchema = "reviewed_same_id_abyss_reference_drift_v1";
    public const string Authority = "maintainer_supplied58_mob_profiles_2026-10-21";
    private const long RetailSourceBytes = 246594362;
    private const string ClientMemberSha256 = "c06eb02452c603249a2071dfd10b4903a123187875edc0e81bff1a9818aaf843";
    private static readonly string[] IdentityTags = ["desc", "race_type", "tribe", "npc_type", "hpgauge_level", "ai_name"];
    private static readonly FrozenSet<int> Ids = new[] { 260000, 260901, 261501, 261801, 262101, 262401 }
        .SelectMany(start => Enumerable.Range(start, 15)).ToFrozenSet();
    private sealed record Entry(int Id, string ClientName, string RetailName, long Start, long End,
        string RecordSha, string ClientCanonicalSha, string PreviousLevel, string RetailLevel,
        FrozenDictionary<string, string> Fields);
    private readonly FrozenDictionary<int, Entry> entries;
    private RetailNpcReviewedIdentityBindings(Dictionary<int, Entry> admitted)
    { entries = admitted.ToFrozenDictionary(); ReviewedNpcIds = entries.Keys.Order().ToImmutableArray(); }
    public ImmutableArray<int> ReviewedNpcIds { get; }
    public int Count => entries.Count;

    public static RetailNpcReviewedIdentityBindings Admit(ReadOnlyMemory<byte> originalReviewBytes, string expectedSha256)
    {
        Require(originalReviewBytes.Length is > 0 and <= 1024 * 1024, "Bounded original identity review bytes are required");
        byte[] ownedReview = originalReviewBytes.ToArray();
        Require(expectedSha256 == ReviewSha256 && Convert.ToHexStringLower(SHA256.HashData(ownedReview)) == ReviewSha256,
            "Only the complete independently pinned original identity review is supported");
        try
        {
            using var document = JsonDocument.Parse(ownedReview, new JsonDocumentOptions { MaxDepth = 64 });
            var root = document.RootElement; UniqueJson(root);
            Require(String(root, "classification") == "STATIC_IDENTITY_DRIFT_REVIEW_NOT_PROFILE_OR_ACTIVATION", "Identity review classification differs");
            True(root, "missing27_unchanged"); False(root, "automatic_alias_imported"); False(root, "original_phase0_or_dataset_changed");
            var summary = root.GetProperty("summary");
            RetailNpcDatasetValidation.Count(summary, "reviewed", 90);
            RetailNpcDatasetValidation.Count(summary, "identity_pass", 90);
            RetailNpcDatasetValidation.Count(summary, "identity_mismatch", 0);
            Require(root.GetProperty("mismatches").GetArrayLength() == 0, "Review has identity mismatches");
            var receipts = root.GetProperty("source_receipts").EnumerateArray().ToArray();
            Require(receipts.Count(x => String(x, "sha256") == Client48ArchiveSha256 && x.GetProperty("bytes").GetInt64() == 3005028) == 1
                && receipts.Count(x => String(x, "sha256") == RetailSourceSha256 && x.GetProperty("bytes").GetInt64() == RetailSourceBytes) == 1,
                "Original independent review source receipts differ");
            var rows = new Dictionary<int, Entry>();
            foreach (var row in root.GetProperty("rows").EnumerateArray())
            {
                int id = row.GetProperty("npc_id").GetInt32();
                Require(Ids.Contains(id) && !rows.ContainsKey(id), "Unreviewed or duplicate identity ID");
                True(row, "static_identity_review_pass"); False(row, "effective_profile_verified"); False(row, "skills_compatible"); False(row, "activation_ready");
                Require(row.GetProperty("mismatches").GetArrayLength() == 0, "Unresolved review row");
                string oldName = String(row, "client48_name"), newName = String(row, "retail58_same_id_name");
                Require(oldName.Length > 0 && newName == oldName + "_No" && String(row, "client48_entry") == "client_npcs_monster.xml", "Reviewed retained-ID names differ");
                True(row, "client_canonical_hash_is_not_original_bxml_span");
                string canonical = String(row, "client48_canonical_record_sha256"); Hash(canonical);
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                var comparisons = row.GetProperty("identity_field_comparisons"); Keys(comparisons, IdentityTags);
                foreach (string tag in IdentityTags)
                {
                    var comparison = comparisons.GetProperty(tag); True(comparison, "both_present_equal");
                    string value = String(comparison, "client48");
                    Require(value.Length > 0 && value == String(comparison, "retail58"), "Reviewed independent identity field differs");
                    values.Add(tag, value);
                }
                var levels = row.GetProperty("levels"); string previous = String(levels, "csharp48"), level = String(levels, "retail58");
                Require(previous == String(levels, "java48") && level == "65", "Reviewed source level facts differ");
                var alternatives = row.GetProperty("same_old_name_other_retail_records");
                Require(alternatives.GetArrayLength() == 1 && alternatives[0].GetProperty("id").GetInt32() != id
                    && String(alternatives[0], "name") == oldName && String(alternatives[0], "level") == "75", "Different-ID fork evidence differs");
                var raw = row.GetProperty("retail_raw_record");
                long start = raw.GetProperty("raw_record_start").GetInt64(), end = raw.GetProperty("raw_record_end").GetInt64();
                Require(start >= 0 && end > start && end <= RetailSourceBytes && (start & 1) == 0 && (end & 1) == 0, "Invalid reviewed UTF16 byte span");
                string rawSha = String(raw, "raw_record_sha256"); Hash(rawSha);
                rows.Add(id, new(id, oldName, newName, start, end, rawSha, canonical, previous, level, values.ToFrozenDictionary(StringComparer.Ordinal)));
            }
            Require(rows.Keys.ToHashSet().SetEquals(Ids) && rows.Values.Select(x => x.ClientName).Distinct(StringComparer.Ordinal).Count() == 90,
                "Complete unique 90-row review is required");
            return new(rows);
        }
        catch (Exception error) when (error is JsonException or FormatException or OverflowException or KeyNotFoundException or InvalidOperationException)
        { throw new InvalidDataException("Original reviewed identity admission failed: " + error.Message, error); }
    }

    internal bool Covers(int id, string clientName, string retailName) => entries.TryGetValue(id, out var row)
        && row.ClientName == clientName && row.RetailName == retailName;

    internal void ValidateSourceContext(JsonElement sources, JsonElement provenance)
    {
        Require(provenance.TryGetProperty("reviewed_same_id_identity_policy", out var policy)
            && policy.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(policy.GetString()), "Reviewed identity provenance exception is missing");
        var all = sources.EnumerateArray().ToArray();
        JsonElement OneRole(string role)
        {
            var selected = all.Where(x => x.TryGetProperty("role", out var r) && r.ValueKind == JsonValueKind.String && r.GetString() == role).ToArray();
            Require(selected.Length == 1, "Exactly one reviewed identity source role is required: " + role); return selected[0];
        }
        var archive = OneRole("independent48_identity_archive");
        Require(String(archive, "sha256") == Client48ArchiveSha256 && archive.GetProperty("bytes").GetInt64() == 3005028
            && String(archive, "decoder_mode") == "strict_utf16_used_strings;duplicate_attributes_rejected;exactly_one_scalar_id_and_name", "Independent current client archive context differs");
        var members = archive.GetProperty("members").EnumerateArray().Where(x => String(x, "member") == "client_npcs_monster.xml").ToArray();
        Require(members.Length == 1 && String(members[0], "sha256") == ClientMemberSha256 && members[0].GetProperty("bytes").GetInt64() == 18188216
            && members[0].GetProperty("identity_record_count").GetInt32() == 44381, "Independent client identity member differs");
        True(members[0], "identity_table"); True(members[0], "crc_size_verified");
        var review = OneRole("exact_pinned90_same_id_reference_drift_review");
        Require(String(review, "sha256") == ReviewSha256 && review.GetProperty("bytes").GetInt64() == 224987, "Selected review source receipt differs");
    }

    internal RetailNpcReviewedIdentityReceipt Bind(int id, string clientName, string retailName, string previousLevel,
        JsonElement metadata, JsonElement raw, JsonElement source, RetailNpcManifestSource sourceReceipt)
    {
        Require(Covers(id, clientName, retailName), "No exact retained-ID reviewed binding: " + id);
        var row = entries[id];
        Require(previousLevel == row.PreviousLevel, "Current 4.8 template level differs from the reviewed compatibility metadata");
        UniqueJson(metadata);
        Keys(metadata, "schema", "npc_id", "client48_devname", "retail58_devname", "source_sha256", "raw_record_sha256",
            "identity_review_sha256", "client48_archive_sha256", "previous_level48", "retail_level58", "authority",
            "effective_profile_verified", "skill_compatibility_verified", "activation_ready");
        Require(String(metadata, "schema") == MetadataSchema && metadata.GetProperty("npc_id").GetInt32() == id
            && String(metadata, "client48_devname") == row.ClientName && String(metadata, "retail58_devname") == row.RetailName
            && String(metadata, "source_sha256") == RetailSourceSha256 && String(metadata, "raw_record_sha256") == row.RecordSha
            && String(metadata, "identity_review_sha256") == ReviewSha256 && String(metadata, "client48_archive_sha256") == Client48ArchiveSha256
            && String(metadata, "previous_level48") == row.PreviousLevel && String(metadata, "retail_level58") == row.RetailLevel
            && String(metadata, "authority") == Authority, "Reviewed identity metadata differs from original admitted evidence");
        False(metadata, "effective_profile_verified"); False(metadata, "skill_compatibility_verified"); False(metadata, "activation_ready");
        string path = String(source, "path");
        Require(sourceReceipt.Sha256 == RetailSourceSha256 && sourceReceipt.Bytes == RetailSourceBytes
            && path.Replace('\\', '/').Split('/')[^1] == "npcs_abyss_monsters.xml"
            && source.GetProperty("byte_start").GetInt64() == row.Start && source.GetProperty("byte_end_exclusive").GetInt64() == row.End
            && String(source, "raw_sha256") == row.RecordSha, "Original selected divided source hash/span/record differs");
        string Scalar(string tag)
        {
            var occurrences = RetailNpcSourceProfile.RawChildren(raw).Where(x => x[0].GetString() == tag).ToArray();
            Require(occurrences.Length == 1 && !RetailNpcSourceProfile.RawChildren(occurrences[0]).Any(), "Missing, duplicate or nested reviewed identity field: " + tag);
            return RetailNpcSourceProfile.RawText(occurrences[0]).Trim();
        }
        Require(Scalar("id") == id.ToString(CultureInfo.InvariantCulture) && Scalar("name") == row.RetailName && Scalar("level") == row.RetailLevel,
            "Selected retained-ID raw identity/level differs");
        foreach (var (tag, value) in row.Fields) Require(Scalar(tag) == value, "Selected reviewed identity field differs: " + tag);
        return new(id, row.ClientName, row.RetailName, path, row.Start, row.End, row.RecordSha, row.ClientCanonicalSha,
            row.PreviousLevel, row.RetailLevel, row.Fields);
    }

    internal void RequireComplete(HashSet<int> observed) => Require(observed.SetEquals(Ids), "Full stream does not contain all and only the 90 reviewed retained IDs");
    private static void True(JsonElement element, string key) => Require(element.GetProperty(key).ValueKind == JsonValueKind.True, "Expected true review/source flag: " + key);
    private static void Keys(JsonElement element, params string[] keys)
    {
        Require(element.ValueKind == JsonValueKind.Object && element.EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.Ordinal).SetEquals(keys),
            "Unknown or missing reviewed identity contract key");
    }
}
