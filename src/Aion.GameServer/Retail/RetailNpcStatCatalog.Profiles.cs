using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.Dataholders.RetailNpc;

namespace Aion.GameServer.Retail;

// Source/profile admission is independent of runtime publication, optional skills
// and movement packages. The full parser uses this same implementation.
internal static partial class RetailNpcStatCatalog
{
    internal const string SourceDatasetId = "05ee65a433d2764f97af414851983c7c03d5b17d5cde8f866e7fcf15756809a3";
    internal const string SourceManifestSha256 = "42bffb43c089b792ca7913815d6336a750f852b40304abffa287687f51089fda";
    internal const string SourceRatioSha256 = "b7947b4725c0d273ab62722adfe576b508e6e4179ffb18e2e9b5c1767a4b779f";
    internal sealed record Member(int MapId, SkyTempleNpcProfile Profile, string HostTemplateSha256);

    internal sealed record ProfileSnapshot(ImmutableArray<Member> Members, ImmutableArray<int> Maps);

    /// <summary>Checks only the selected base profiles and source/host identity
    /// declarations. Does not validate optional skill packages or activate NPCs.</summary>
    internal static ProfileSnapshot ReadProfileSourceForAudit(byte[] bytes, string hash, string directory)
    {
        CheckHash(bytes, hash, "statistics manifest");
        using var document = JsonDocument.Parse(bytes, new() { MaxDepth = 384 });
        var root = document.RootElement;
        RetailNpcDatasetValidation.UniqueJson(root);
        ValidateManifestIdentity(root);
        RetailNpcReviewedIdentityBindings? identities = null;
        if (root.TryGetProperty("identity_review", out var review))
            identities = RetailNpcReviewedIdentityBindings.Admit(ReadAsset(review, directory), review.GetProperty("sha256").GetString()!);
        var (profiles, maps) = ReadProfiles(root, hash, directory, identities);
        return new(profiles.Values.OrderBy(m => m.MapId).ThenBy(m => m.Profile.NpcId).ToImmutableArray(),
            maps.Order().ToImmutableArray());
    }

    private static void ValidateManifestIdentity(JsonElement root)
    {
        if (root.GetProperty("version").GetInt32() != 1 || root.GetProperty("mode").GetString() != "statistics-only-v1"
            || root.GetProperty("dataset_id").GetString() != SourceDatasetId
            || root.GetProperty("output_manifest_sha256").GetString() != SourceManifestSha256
            || root.GetProperty("region").GetString() != "USA")
            throw new InvalidDataException("Unsupported statistics source identity or purpose");
    }

    private static (Dictionary<(int Map, int Npc), Member> Profiles, HashSet<int> Maps) ReadProfiles(
        JsonElement root, string expected, string directory, RetailNpcReviewedIdentityBindings? identities)
    {
        var result = new Dictionary<(int, int), Member>();
        var maps = root.TryGetProperty("supported_maps",out var selectedMaps)
            ? selectedMaps.EnumerateArray().Select(x=>x.GetInt32()).ToHashSet()
            : root.GetProperty("cohorts").EnumerateArray().Select(x=>x.GetProperty("map_id").GetInt32()).Where(x=>x>0).ToHashSet();
        if (maps.Count is <1 or >2048 || maps.Any(m=>m<=0)) throw new InvalidDataException("Explicit supported combat maps required");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cohort in root.GetProperty("cohorts").EnumerateArray())
        {
            string name = cohort.GetProperty("id").GetString()!;
            if (string.IsNullOrWhiteSpace(name) || !names.Add(name)) throw new InvalidDataException("Duplicate/empty statistics cohort");
            int map = cohort.GetProperty("map_id").GetInt32();
            if (map < 0 || map > 0 && !maps.Contains(map)) throw new InvalidDataException("Statistics cohort is outside supported combat maps");
            byte[] sourceBytes = ReadAsset(cohort.GetProperty("profile_artifact"), directory);
            using var sourceDoc = JsonDocument.Parse(sourceBytes, new() { MaxDepth = 384 });
            var artifact = sourceDoc.RootElement; RetailNpcDatasetValidation.UniqueJson(artifact);
            if (artifact.GetProperty("dataset_id").GetString() != SourceDatasetId
                || artifact.GetProperty("output_manifest_sha256").GetString() != SourceManifestSha256)
                throw new InvalidDataException("Statistics profile source identity differs");
            var rows = artifact.GetProperty("profiles").EnumerateArray().ToDictionary(x => x.GetProperty("npc_id").GetInt32());
            var members = cohort.GetProperty("members").EnumerateArray().ToArray();
            if (members.Length == 0 || members.Select(x => x.GetProperty("npc_id").GetInt32()).Distinct().Count() != members.Length)
                throw new InvalidDataException("Empty/duplicate statistics members");
            if (identities is not null) identities.ValidateSourceContext(artifact.GetProperty("source_manifest"), artifact.GetProperty("provenance"));
            var ratio = RetailNpcLevelRatioTableInputs.Admit(Convert.FromBase64String(artifact.GetProperty("ratio_xml_base64").GetString()!),
                new(SourceDatasetId, SourceManifestSha256, "USA",
                    SourceRatioSha256, "D:/Aion58ServerTesting/Server/Map/XML/pc_stat_ratio.xml",
                    "pc_stat_ratio.xml", "USA_missing_base_fallback"));
            if (!ratio.Success) throw new InvalidDataException("Statistics ratio source: " + string.Join(";", ratio.Issues.Select(x => x.Message)));
            foreach (var member in members)
            {
                int id = member.GetProperty("npc_id").GetInt32();
                if (!rows.TryGetValue(id, out var raw)) throw new InvalidDataException("Missing statistics source record: " + id);
                string clientName = raw.GetProperty("binding48").GetProperty("devnames48").EnumerateArray().Select(x => x.GetString()).Single()!;
                RetailNpcReviewedIdentityReceipt? identity = null;
                if (clientName != raw.GetProperty("devname").GetString())
                {
                    if (identities is null) throw new InvalidDataException("Unreviewed changed NPC identity: " + id);
                    var span = raw.GetProperty("source");
                    var file = artifact.GetProperty("source_manifest").EnumerateArray().Single(x => x.GetProperty("path").GetString() == span.GetProperty("path").GetString());
                    identity = identities.Bind(id, clientName, raw.GetProperty("devname").GetString()!,
                        member.GetProperty("host_level").GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture),
                        raw.GetProperty("binding48").GetProperty("reviewed_reference_drift"), raw.GetProperty("raw_record"), span,
                        new(file.GetProperty("bytes").GetInt64(), file.GetProperty("sha256").GetString()!));
                }
                var source = new RetailNpcSourceProfile(raw, identity);
                ValidateSourceAnnotations(source);
                var dataset = new RetailNpcStaticDataset(SourceDatasetId, SourceManifestSha256,
                    new Dictionary<int, RetailNpcSourceProfile> { [id] = source }, 1, 0, sourceBytes.Length, [],
                    artifact.GetProperty("provenance"), artifact.GetProperty("source_manifest"));
                var projection = RetailNpcTemplateProjection.ProjectStatistics(dataset, id, new(dataset.DatasetId, dataset.OutputManifestSha256, "USA"));
                if (!projection.Success) throw new InvalidDataException($"Statistics projection {id}: " + string.Join(";", projection.Issues.Select(x => x.Message)));
                var input = projection.Inputs!;
                if (input.Receipt.RawRecordSha256 != member.GetProperty("source_record_sha256").GetString())
                    throw new InvalidDataException("Statistics record differs from review: " + id);
                bool dual = input.Integers["dual"].ParsedValue is not (null or 0);
                var domain = new RetailNpcOrdinarySeedDomain(input.Receipt, SourceRatioSha256, true, true, 1, 0, 0, false, false, dual)
                    { StatisticsBeforeDualSplit = dual };
                var core = RetailNpcOrdinaryCoreSeedProjection.ProjectOrdinaryCoreSeeds(input, ratio.Table!, domain);
                if (!core.Success) throw new InvalidDataException($"Statistics core {id}: " + string.Join(";", core.Issues.Select(x => x.Message)));
                var scalars = RetailNpcOrdinaryScalarProjection.ProjectStatistics(input, core.Seeds!, new(domain));
                var bounds = RetailNpcBoundsConstruction.Construct(input, new(input.Receipt, true, true, "Source projection only; statistics admission does not apply bounds"));
                if (!scalars.Success || !bounds.Success) throw new InvalidDataException("Statistics scalar construction failed: " + id);
                var profile = new SkyTempleNpcProfile(input, core.Seeds!, scalars.Inputs!, bounds.Plan!, expected, statisticsOnly: true);
                if (!result.TryAdd((map, id), new(map, profile, member.GetProperty("host_template_sha256").GetString()!)))
                    throw new InvalidDataException("Overlapping statistics membership");
            }
        }
        if (result.Count is < 1 or > 65536) throw new InvalidDataException("Empty/oversized statistics package");
        return (result, maps);
    }

    private static byte[] ReadAsset(JsonElement spec, string directory)
    {
        string path = Path.GetFullPath(spec.GetProperty("path").GetString()!, directory);
        var file = new FileInfo(path);
        if (!file.Exists || file.Length is <= 0 or > 64 * 1024 * 1024) throw new InvalidDataException("Missing/oversized statistics asset: " + path);
        byte[] bytes = File.ReadAllBytes(path); CheckHash(bytes, spec.GetProperty("sha256").GetString()!, path); return bytes;
    }
    internal static void CheckHash(ReadOnlySpan<byte> bytes, string hash, string name)
    {
        if (bytes.Length is <= 0 or > 64 * 1024 * 1024 || hash.Length != 64 || Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
            throw new InvalidDataException("Retail statistics SHA256 mismatch: " + name);
    }
    internal static void ValidateSourceAnnotations(RetailNpcSourceProfile source)
    {
        foreach (string annotation in source.Blockers)
        {
            if (annotation is "effective_runtime_profile_not_verified" or "field_units_defaults_and_scaling_not_fully_recovered"
                or "skill_effect_semantics_not_verified" or "unresolved_skill_identity_binding") continue;
            // NPCDB::Load 14027c890 selects seven divided files, not npcs.xml.
            // Independently byte-checked 2026-10-25: only level65/75 differs.
            // Keep this exact selected record and the crosscheck annotation.
            if (annotation == "aggregate_semantic_conflict_requires_review" && source.NpcId == 855263
                && source.SourceProvenance.GetProperty("raw_sha256").GetString() == "54e5194aa185e73279a0291a3b53d1da33cb89603217810ebbe1fd9020de3f3b") continue;
            throw new InvalidDataException("Unresolved statistics source annotation: " + annotation);
        }
    }
}
