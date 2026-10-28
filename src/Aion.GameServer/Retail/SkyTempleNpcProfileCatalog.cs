using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.RetailNpc;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Retail;

/// <summary>Local opt-in25-template package. Preparation and activation are separate so
/// missing combat/navigation assets cannot publish partially enabled spawned NPCs.</summary>
public static class SkyTempleNpcProfileCatalog
{
    public const int MapId = 320050000;
    public const string ArtifactSha256 = "61ffcbac2e416cabdd94a1c50abda44bf38890489a00bf7b9ddeabfb4a5970d2";
    public const string DatasetId = RetailNpcStatCatalog.SourceDatasetId;
    public const string ManifestSha256 = RetailNpcStatCatalog.SourceManifestSha256;
    public const string RatioSha256 = RetailNpcStatCatalog.SourceRatioSha256;
    private static readonly int[] Ids = [212761,212762,212763,212764,212765,212766,212767,212768,212769,212770,
        212771,212772,212773,212774,212775,212776,212777,212778,212779,212780,212781,212782,212833,212835,212876];
    private static readonly HashSet<string> HistoricalVerificationAnnotations = new(StringComparer.Ordinal)
    {
        "effective_runtime_profile_not_verified", "field_units_defaults_and_scaling_not_fully_recovered",
        "skill_effect_semantics_not_verified"
    };
    private static FrozenDictionary<int, SkyTempleNpcProfile>? prepared;
    private static FrozenDictionary<int, SkyTempleNpcProfile>? active;
    public static bool Enabled => Volatile.Read(ref active) is not null;
    public static IReadOnlyCollection<SkyTempleNpcProfile> GetPreparedProfiles()
        => prepared?.Values ?? throw new InvalidOperationException("Sky Temple profiles have not been prepared.");
    public static bool TryGet(int mapId, int npcId, out SkyTempleNpcProfile profile)
    {
        profile = null!; var current = Volatile.Read(ref active);
        return (mapId == MapId && current is not null && current.TryGetValue(npcId, out profile!))
            || RetailNpcCohortCatalog.TryGet(mapId, npcId, out profile!);
    }
    public static bool TryGet(Npc npc, out SkyTempleNpcProfile profile)
    {
        profile = null!;
        // Npc's base constructor runs before SummonedObject initializes its creator.
        // Exclude that family and unmatched content before any virtual owner lookup.
        if (npc is SummonedObject || !TryGet(npc.GetWorldId(), npc.GetNpcId(), out var candidate)) return false;
        // The spawn owner exists during construction; the factory assigns Npc.creatorId later.
        var spawn = npc.GetSpawn() ?? throw new InvalidDataException("Matched SkyTemple NPC has no spawn identity");
        if (spawn.GetCreatorId() != 0 || npc.GetCreatorId() != 0 || npc.GetSummonOwner() is not null) return false;
        profile = candidate;
        return true;
    }
    public static async Task InitializeAsync(bool requested, string? artifactPath, CancellationToken cancellationToken = default)
    {
        if (Enabled) throw new InvalidOperationException("Sky Temple profiles cannot reload while active.");
        prepared = null;
        if (!requested) return;
        if (string.IsNullOrWhiteSpace(artifactPath)) throw new InvalidDataException("AION_RETAIL_SKYTEMPLE_DATA must name the pinned cohort artifact.");
        var file = new FileInfo(artifactPath);
        if (!file.Exists || file.Length is <= 0 or > 2_000_000) throw new InvalidDataException("Sky Temple artifact is missing or exceeds2MB: " + artifactPath);
        byte[] bytes = await File.ReadAllBytesAsync(file.FullName, cancellationToken).ConfigureAwait(false);
        var result = Parse(bytes);
        foreach (var profile in result.Values)
        {
            if (DataManager.NPC_DATA.GetNpcTemplate(profile.NpcId) is null)
                throw new InvalidDataException("Sky Temple4.8 NPC identity is missing: " + profile.NpcId);
            foreach (var skill in profile.Skills)
                if (DataManager.SKILL_DATA.GetSkillTemplate(skill.Id48) is null)
                    throw new InvalidDataException($"Sky Temple unsupported4.8 skill {skill.Id48} at NPC {profile.NpcId}/ordinal {skill.Ordinal}.");
        }
        prepared = result; // Still inactive until the complete package readiness checks return normally.
    }
    internal static void ActivatePrepared()
    {
        var ready = prepared ?? throw new InvalidOperationException("Sky Temple profiles are not prepared.");
        if (ready.Count != 25) throw new InvalidDataException("Sky Temple cohort is incomplete.");
        Volatile.Write(ref active, ready);
    }
    /// <summary>Serialized integration-test scope; parses the same pinned artifact and restores
    /// both catalog states on disposal. Production bootstrap uses the readiness gates above.</summary>
    internal static IDisposable InstallForTests(ReadOnlySpan<byte> bytes)
    {
        var profiles = Parse(bytes);
        var previousPrepared = prepared; var previousActive = Volatile.Read(ref active);
        prepared = profiles; Volatile.Write(ref active, profiles);
        return new RestoreCatalog(previousPrepared, previousActive);
    }
    private sealed class RestoreCatalog(FrozenDictionary<int, SkyTempleNpcProfile>? previousPrepared,
        FrozenDictionary<int, SkyTempleNpcProfile>? previousActive) : IDisposable
    {
        private int disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            prepared = previousPrepared; Volatile.Write(ref active, previousActive);
        }
    }
    internal static FrozenDictionary<int, SkyTempleNpcProfile> Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > 2_000_000 || Convert.ToHexStringLower(SHA256.HashData(bytes)) != ArtifactSha256)
            throw new InvalidDataException("Sky Temple artifact SHA256 differs from the reviewed25-profile source.");
        using var document = JsonDocument.Parse(bytes.ToArray()); var root = document.RootElement;
        RetailNpcDatasetValidation.UniqueJson(root);
        if (root.GetProperty("version").GetInt32() != 1 || root.GetProperty("map_id").GetInt32() != MapId
            || root.GetProperty("dataset_id").GetString() != DatasetId || root.GetProperty("output_manifest_sha256").GetString() != ManifestSha256)
            throw new InvalidDataException("Sky Temple source package identity differs.");
        var source = new Dictionary<int, RetailNpcSourceProfile>();
        foreach (var row in root.GetProperty("profiles").EnumerateArray())
        {
            var profile = new RetailNpcSourceProfile(row);
            // The extractor's historical verification annotations remain in the original source.
            // They are not identity/source failures and are never upgraded to runtime verification.
            // This scoped adapter separately constructs typed bases; production activation still
            // requires the combat and navigation package readiness checks before publication.
            if (profile.DatasetId != DatasetId || profile.Blockers.Length != HistoricalVerificationAnnotations.Count
                || !HistoricalVerificationAnnotations.SetEquals(profile.Blockers))
                throw new InvalidDataException($"Sky Temple source {profile.NpcId} has incompatible identity/verification annotations: {string.Join(",", profile.Blockers)}.");
            if (!source.TryAdd(profile.NpcId, profile))
                throw new InvalidDataException($"Duplicate Sky Temple source row {profile.NpcId}.");
        }
        if (source.Count != 25 || !source.Keys.ToHashSet().SetEquals(Ids)) throw new InvalidDataException("Sky Temple25 IDs differ.");
        // This bounded source cohort is NOT relabeled as the full63,287-row reader result.
        var cohort = new RetailNpcStaticDataset(DatasetId, ManifestSha256, source, 25, 0, bytes.Length, [],
            root.GetProperty("provenance"), root.GetProperty("source_manifest"));
        byte[] ratioBytes = Convert.FromBase64String(root.GetProperty("ratio_xml_base64").GetString()!);
        if (root.GetProperty("ratio_sha256").GetString() != RatioSha256) throw new InvalidDataException("Ratio table identity differs.");
        var ratio = RetailNpcLevelRatioTableInputs.Admit(ratioBytes, new(DatasetId, ManifestSha256, "USA", RatioSha256,
            "D:/Aion58ServerTesting/Server/Map/XML/pc_stat_ratio.xml", "pc_stat_ratio.xml", "USA_missing_base_fallback"));
        if (!ratio.Success) throw new InvalidDataException("Sky Temple ratio table: " + string.Join(";", ratio.Issues.Select(x => x.Message)));
        var profiles = new Dictionary<int, SkyTempleNpcProfile>();
        foreach (int id in Ids)
        {
            var projection = RetailNpcTemplateProjection.Project(cohort, id, new(DatasetId, ManifestSha256, "USA"));
            if (!projection.Success) throw new InvalidDataException($"Sky Temple source {id}: " + string.Join(";", projection.Issues.Select(x => x.Message)));
            var input = projection.Inputs!;
            var domain = new RetailNpcOrdinarySeedDomain(input.Receipt, RatioSha256, true, true, 1, 0, 0, false, false, false);
            var core = RetailNpcOrdinaryCoreSeedProjection.ProjectOrdinaryCoreSeeds(input, ratio.Table!, domain);
            if (!core.Success) throw new InvalidDataException($"Sky Temple core {id}: " + string.Join(";", core.Issues.Select(x => x.Message)));
            var scalars = RetailNpcOrdinaryScalarProjection.Project(input, core.Seeds!, new(domain));
            var bounds = RetailNpcBoundsConstruction.Construct(input, new(input.Receipt, true, true, "managed source-construction;pilot25"));
            if (!scalars.Success || !bounds.Success || !bounds.Plan!.CanSupplySourceDerivedBounds)
                throw new InvalidDataException($"Sky Temple scalar/bounds construction failed for {id}: "
                    + string.Join(";", scalars.Issues.Select(x => x.Code + ":" + x.Message)) + ";"
                    + string.Join(";", bounds.Issues.Select(x => x.Code + ":" + x.Detail)));
            var p = new SkyTempleNpcProfile(input, core.Seeds!, scalars.Inputs!, bounds.Plan, ArtifactSha256);
            if (p.Level is < 30 or > 33 || p.MaxHp <= 0 || p.MinDamage <= 0 || p.MaxDamage < p.MinDamage
                || p.Attacks.Length != 1 || p.HitCount != 1 || !p.HasRunReturnMode
                || p.MaxChaseMilliseconds != 15000 || p.ReturnSpeedMultiplier != 2f
                || p.Skills.Any(s => s.Level <= 0 || s.Count < 0 || s.Rate < 0 || s.Delay < 0))
                throw new InvalidDataException($"Sky Temple unsupported profile values at {id}.");
            profiles.Add(id, p);
        }
        if (profiles.Values.Sum(p => p.Skills.Length) != 59) throw new InvalidDataException("Sky Temple59 ordered skill bindings differ.");
        return profiles.ToFrozenDictionary();
    }
}
