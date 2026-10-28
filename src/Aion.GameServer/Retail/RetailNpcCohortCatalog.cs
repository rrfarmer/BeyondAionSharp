using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.RetailNpc;
using Aion.GameServer.GeoEngine.RetailPath;
using Aion.GameServer.Model;

namespace Aion.GameServer.Retail;

/// <summary>Opt-in, immutable map/NPC cohorts. A manifest selects reviewed source
/// records and host identities; the existing C# projections still calculate every
/// base. Publication follows combat and loaded-map readiness, before any spawn.</summary>
internal static class RetailNpcCohortCatalog
{
    internal const string ManifestEnvironment = "AION_RETAIL_COHORT_MANIFEST";
    internal const string ManifestHashEnvironment = "AION_RETAIL_COHORT_MANIFEST_SHA256";
    private const int MaximumArtifactBytes = 16 * 1024 * 1024;
    private static readonly HashSet<string> HistoricalAnnotations = new(StringComparer.Ordinal)
    {
        "effective_runtime_profile_not_verified", "field_units_defaults_and_scaling_not_fully_recovered",
        "skill_effect_semantics_not_verified"
    };
    private sealed record Package(string ManifestSha256,
        FrozenDictionary<(int Map,int Npc),SkyTempleNpcProfile> Profiles,
        ImmutableArray<RetailNpcMapDefinition> Maps);
    private static Package? prepared;
    private static Package? active;
    internal static bool Enabled => Volatile.Read(ref active) is not null;
    internal static int ActiveCount => Volatile.Read(ref active)?.Profiles.Count ?? 0;
    internal static IReadOnlyList<RetailNpcMapDefinition> GetPreparedMaps() =>
        prepared?.Maps ?? throw new InvalidOperationException("Retail cohorts have not been prepared");
    internal static IReadOnlyCollection<SkyTempleNpcProfile> GetPreparedProfiles(int mapId) =>
        (prepared ?? throw new InvalidOperationException("Retail cohorts have not been prepared"))
        .Profiles.Where(x=>x.Key.Map==mapId).Select(x=>x.Value).ToArray();
    internal static IReadOnlyCollection<SkyTempleNpcProfile> GetActiveProfiles() =>
        Volatile.Read(ref active) is { } package ? package.Profiles.Values : Array.Empty<SkyTempleNpcProfile>();
    internal static bool TryGet(int mapId,int npcId,out SkyTempleNpcProfile profile)
    {
        profile=null!;
        return Volatile.Read(ref active) is { } current && current.Profiles.TryGetValue((mapId,npcId),out profile!);
    }

    internal static async Task<bool> InitializeAsync(string? manifestPath,string? expectedSha256,
        CancellationToken cancellationToken=default)
    {
        if(Enabled)throw new InvalidOperationException("Active retail cohorts cannot reload");
        prepared=null;
        if(string.IsNullOrWhiteSpace(manifestPath)&&string.IsNullOrWhiteSpace(expectedSha256))return false;
        if(string.IsNullOrWhiteSpace(manifestPath)||!Digest(expectedSha256))
            throw new InvalidDataException("Retail cohort manifest and its separately selected SHA256 are both required");
        string path=Path.GetFullPath(manifestPath);
        var file=new FileInfo(path);
        if(!file.Exists||file.Length is <=0 or >MaximumArtifactBytes)
            throw new InvalidDataException("Missing/oversized retail cohort manifest: "+path);
        byte[] bytes=await File.ReadAllBytesAsync(path,cancellationToken).ConfigureAwait(false);
        var package=Parse(bytes,expectedSha256!,Path.GetDirectoryName(path)!);
        foreach(var group in package.Profiles.GroupBy(x=>x.Value.Cohort!.Id))
        {
            var profiles=group.Select(x=>x.Value).ToArray();
            foreach(var profile in profiles)ValidateHost(profile);
            RetailNpcCohortCombatReadiness.Validate(profiles);
        }
        prepared=package;
        return true;
    }

    internal static void ActivatePrepared()
    {
        var package=prepared??throw new InvalidOperationException("Retail cohorts have not been prepared");
        // Publication cannot bypass the loaded-world join by merely parsing source files.
        SkyTempleNpcNavigationAssets.ValidateLoadedMaps();
        Volatile.Write(ref active,package);
    }

    private static void ValidateHost(SkyTempleNpcProfile p)
    {
        var binding=p.Cohort!;
        var host=DataManager.NPC_DATA.GetNpcTemplate(p.NpcId)
            ??throw new InvalidDataException("Missing supported4.8 cohort NPC: "+p.NpcId);
        if(host.GetLevel()!=binding.ExpectedHostLevel||p.Level!=binding.ExpectedHostLevel
            ||host.GetAiName()!=binding.ExpectedHostAi||RetailNpcHostIdentity.Template(host)!=binding.HostTemplateSha256
            ||host.GetTribe() is TribeClass.PET or TribeClass.PET_DARK)
            throw new InvalidDataException("Cohort host identity/level/AI/context drift: "+p.NpcId);
        int spots=0;
        foreach(var group in DataManager.SPAWNS_DATA.GetSpawnsForNpc(binding.MapId,p.NpcId))
        foreach(var spot in group.GetSpawnTemplates())
        {
            spots++;
            if(spot.GetCreatorId()!=0||spot.IsAerialSpawn()
                ||(!string.IsNullOrEmpty(spot.GetAiName())&&spot.GetAiName()!=binding.ExpectedHostAi))
                throw new InvalidDataException($"Unsupported cohort spawn owner/flight/AI override: {binding.MapId}/{p.NpcId}");
        }
        if(spots==0)throw new InvalidDataException("Cohort has no current supported spawn: "+p.NpcId);
    }

    private static Package Parse(ReadOnlySpan<byte> bytes,string expectedSha256,string directory)
    {
        RequireHash(bytes,expectedSha256,"retail cohort manifest");
        using var document=JsonDocument.Parse(bytes.ToArray(),new(){MaxDepth=384});
        var root=document.RootElement;
        RetailNpcDatasetValidation.UniqueJson(root);
        if(root.GetProperty("version").GetInt32()!=1
            ||root.GetProperty("dataset_id").GetString()!=SkyTempleNpcProfileCatalog.DatasetId
            ||root.GetProperty("output_manifest_sha256").GetString()!=SkyTempleNpcProfileCatalog.ManifestSha256
            ||root.GetProperty("region").GetString()!="USA")
            throw new InvalidDataException("Unsupported retail cohort source version/region");
        var maps=root.GetProperty("maps").EnumerateArray().Select(RetailNpcMapDefinition.Parse).ToImmutableArray();
        if(maps.Length is <1 or >32||maps.Select(x=>x.MapId).Distinct().Count()!=maps.Length
            ||maps.Any(x=>x.MapId==SkyTempleNpcProfileCatalog.MapId))
            throw new InvalidDataException("Missing/duplicate cohort map or attempted legacy-pilot replacement");
        var profiles=new Dictionary<(int,int),SkyTempleNpcProfile>();
        var cohortNames=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in root.GetProperty("cohorts").EnumerateArray())
        {
            string id=RequiredText(row,"id");int mapId=row.GetProperty("map_id").GetInt32();
            if(RequiredText(row,"domain")!="fresh-unowned-static-ground-kind1-v1")
                throw new InvalidDataException("Unreviewed retail cohort owner/scaling/behavior domain: "+id);
            if(id.Length>96||!cohortNames.Add(id)||!maps.Any(m=>m.MapId==mapId))
                throw new InvalidDataException("Duplicate cohort or missing map definition: "+id);
            var members=row.GetProperty("members").EnumerateArray().Select(x=>x.Clone()).ToArray();
            if(members.Length is <1 or >1024||members.Select(x=>x.GetProperty("npc_id").GetInt32()).Distinct().Count()!=members.Length)
                throw new InvalidDataException("Missing/duplicate/oversized cohort membership: "+id);
            var spells=ImmutableDictionary.CreateBuilder<int,ImmutableArray<byte>>();
            foreach(var spell in row.GetProperty("spell_sources").EnumerateArray())
            {
                int skill=spell.GetProperty("skill_id").GetInt32();
                byte[] raw=Convert.FromBase64String(RequiredText(spell,"utf16le_base64"));
                RequireHash(raw,RequiredText(spell,"sha256"),"cohort spell source");
                if(raw.Length is <=0 or >131072||raw.Length%2!=0||!spells.TryAdd(skill,raw.ToImmutableArray()))
                    throw new InvalidDataException("Missing/duplicate/oversized cohort spell source");
            }
            var artifact=row.GetProperty("profile_artifact");
            string artifactHash=RequiredText(artifact,"sha256");
            string path=Path.GetFullPath(RequiredText(artifact,"path"),directory);
            var file=new FileInfo(path);
            if(!file.Exists||file.Length is <=0 or >MaximumArtifactBytes)throw new InvalidDataException("Missing/oversized cohort profile artifact: "+path);
            byte[] rawProfiles=File.ReadAllBytes(path);
            RequireHash(rawProfiles,artifactHash,"cohort profile artifact");
            foreach(var p in Project(rawProfiles,artifactHash,mapId,id,expectedSha256,directory,members,spells.ToImmutable()))
                if(!profiles.TryAdd((mapId,p.NpcId),p))throw new InvalidDataException($"Overlapping retail cohort membership: {mapId}/{p.NpcId}");
        }
        if(cohortNames.Count is <1 or >64||maps.Any(m=>!profiles.Keys.Any(k=>k.Item1==m.MapId)))
            throw new InvalidDataException("Empty/oversized cohort package or map without members");
        return new(expectedSha256,profiles.ToFrozenDictionary(),maps);
    }

    /// <summary>Serialized fixture scope; never used by bootstrap. Source hashes and
    /// projections are identical to production; loaded-map publication is tested by SIM.</summary>
    internal static IDisposable InstallForTests(ReadOnlySpan<byte> manifest,string expectedSha256,string directory)
    {
        var package=Parse(manifest,expectedSha256,directory);
        var previousPrepared=prepared;var previousActive=Volatile.Read(ref active);
        prepared=package;Volatile.Write(ref active,package);
        return new Restore(previousPrepared,previousActive);
    }
    private sealed class Restore(Package? previousPrepared,Package? previousActive):IDisposable
    {
        private int disposed;
        public void Dispose()
        {
            if(Interlocked.Exchange(ref disposed,1)!=0)return;
            prepared=previousPrepared;Volatile.Write(ref active,previousActive);
        }
    }

    private static IEnumerable<SkyTempleNpcProfile> Project(byte[] bytes,string artifactHash,int mapId,string id,
        string manifestHash,string directory,JsonElement[] members,ImmutableDictionary<int,ImmutableArray<byte>> spells)
    {
        using var doc=JsonDocument.Parse(bytes,new(){MaxDepth=384});var root=doc.RootElement;
        RetailNpcDatasetValidation.UniqueJson(root);
        if(root.GetProperty("version").GetInt32()!=1||root.GetProperty("map_id").GetInt32()!=mapId
            ||root.GetProperty("dataset_id").GetString()!=SkyTempleNpcProfileCatalog.DatasetId
            ||root.GetProperty("output_manifest_sha256").GetString()!=SkyTempleNpcProfileCatalog.ManifestSha256)
            throw new InvalidDataException("Cohort profile artifact source identity differs");
        var source=new Dictionary<int,RetailNpcSourceProfile>();
        foreach(var row in root.GetProperty("profiles").EnumerateArray())
        {
            var p=new RetailNpcSourceProfile(row);
            if(p.DatasetId!=SkyTempleNpcProfileCatalog.DatasetId||p.Blockers.Length!=HistoricalAnnotations.Count
                ||!HistoricalAnnotations.SetEquals(p.Blockers)||!source.TryAdd(p.NpcId,p))
                throw new InvalidDataException("Unsupported/duplicate cohort source record: "+p.NpcId);
        }
        if(!source.Keys.ToHashSet().SetEquals(members.Select(x=>x.GetProperty("npc_id").GetInt32())))
            throw new InvalidDataException("Cohort artifact and reviewed membership differ");
        var dataset=new RetailNpcStaticDataset(SkyTempleNpcProfileCatalog.DatasetId,SkyTempleNpcProfileCatalog.ManifestSha256,
            source,source.Count,0,bytes.Length,[],root.GetProperty("provenance"),root.GetProperty("source_manifest"));
        byte[] ratioBytes=Convert.FromBase64String(RequiredText(root,"ratio_xml_base64"));
        if(RequiredText(root,"ratio_sha256")!=SkyTempleNpcProfileCatalog.RatioSha256)
            throw new InvalidDataException("Cohort ratio selection differs");
        var ratio=RetailNpcLevelRatioTableInputs.Admit(ratioBytes,new(dataset.DatasetId,dataset.OutputManifestSha256,"USA",
            SkyTempleNpcProfileCatalog.RatioSha256,"D:/Aion58ServerTesting/Server/Map/XML/pc_stat_ratio.xml","pc_stat_ratio.xml","USA_missing_base_fallback"));
        if(!ratio.Success)throw new InvalidDataException("Cohort ratio table: "+string.Join(";",ratio.Issues.Select(x=>x.Message)));
        foreach(var member in members)
        {
            int npcId=member.GetProperty("npc_id").GetInt32();
            var projection=RetailNpcTemplateProjection.Project(dataset,npcId,new(dataset.DatasetId,dataset.OutputManifestSha256,"USA"));
            if(!projection.Success)throw new InvalidDataException($"Cohort projection {npcId}: "+string.Join(";",projection.Issues.Select(x=>x.Message)));
            var input=projection.Inputs!;
            if(input.Receipt.RawRecordSha256!=RequiredText(member,"source_record_sha256"))
                throw new InvalidDataException("Reviewed cohort source record differs: "+npcId);
            var domain=new RetailNpcOrdinarySeedDomain(input.Receipt,SkyTempleNpcProfileCatalog.RatioSha256,true,true,1,0,0,false,false,false);
            var core=RetailNpcOrdinaryCoreSeedProjection.ProjectOrdinaryCoreSeeds(input,ratio.Table!,domain);
            if(!core.Success)throw new InvalidDataException($"Cohort core {npcId}: "+string.Join(";",core.Issues.Select(x=>x.Message)));
            var scalars=RetailNpcOrdinaryScalarProjection.Project(input,core.Seeds!,new(domain));
            var bounds=RetailNpcBoundsConstruction.Construct(input,new(input.Receipt,true,true,"managed source-construction;reviewed cohort "+id));
            if(!scalars.Success||!bounds.Success||!bounds.Plan!.CanSupplySourceDerivedBounds)
                throw new InvalidDataException("Unsupported cohort scalar/bounds construction: "+npcId);
            var normal=member.GetProperty("normal_motion");
            var normalAsset=normal.GetProperty("evidence_asset");
            string normalHash=RequiredDigest(normalAsset,"sha256");
            string normalPath=Path.GetFullPath(RequiredText(normalAsset,"path"),directory);
            var normalFile=new FileInfo(normalPath);
            if(!normalFile.Exists||normalFile.Length is <=0 or >131072
                ||normalFile.Length!=normalAsset.GetProperty("byte_length").GetInt64())
                throw new InvalidDataException("Missing/oversized cohort normal-motion evidence: "+npcId);
            byte[] normalEvidence=File.ReadAllBytes(normalPath);
            RequireHash(normalEvidence,normalHash,"cohort normal-motion evidence");
            var skillMotions=member.GetProperty("skill_motions").EnumerateArray().Select(m=>new RetailNpcSkillMotion(
                m.GetProperty("ordinal").GetInt32(),m.GetProperty("skill_id").GetInt32(),m.GetProperty("level").GetInt32(),
                m.GetProperty("first_hit_ms").GetUInt16(),m.GetProperty("recovery_ms").GetUInt32(),m.GetProperty("cast_ms").GetUInt32(),
                m.GetProperty("reuse_ms").GetUInt32(),m.GetProperty("ammo_speed").GetSingle(),RequiredDigest(m,"evidence_sha256"))).ToImmutableArray();
            var binding=new RetailNpcCohortBinding(id,mapId,manifestHash,
                new(normal.GetProperty("first_hit_ms").GetUInt16(),normal.GetProperty("second_hit_ms").ValueKind==JsonValueKind.Null
                    ?null:normal.GetProperty("second_hit_ms").GetUInt16(),normalHash,normalEvidence.ToImmutableArray()),
                skillMotions,member.GetProperty("host_level").GetInt32(),RequiredText(member,"host_ai"),RequiredDigest(member,"host_template_sha256"),spells);
            var profile=new SkyTempleNpcProfile(input,core.Seeds!,scalars.Inputs!,bounds.Plan,manifestHash,binding);
            // This is the ordinary ground/non-dual package, not an implicit admission of
            // every family for which a detached comparison can calculate base numbers.
            if(profile.Level is <1 or >100||profile.Level!=binding.ExpectedHostLevel||profile.MaxHp<=0
                ||profile.StatRatio!=1000||profile.MinDamage<=0||profile.MaxDamage>ushort.MaxValue||profile.MaxDamage<profile.MinDamage
                ||profile.HitCount!=1||profile.Attacks.Length!=1||!profile.HasRunReturnMode||profile.MaxChaseMilliseconds<=0
                ||!float.IsFinite(profile.ReturnSpeedMultiplier)||profile.ReturnSpeedMultiplier<=0
                ||input.Integers["max_mp"].Presence!=RetailNpcSourcePresence.Absent
                ||input.Integers["mp_regen"].Presence!=RetailNpcSourcePresence.Absent)
                throw new InvalidDataException("Unsupported ordinary cohort context/profile: "+npcId);
            yield return profile;
        }
    }

    internal static void RequireHash(ReadOnlySpan<byte> bytes,string expected,string subject)
    {
        if(bytes.Length>MaximumArtifactBytes||!Digest(expected)||Convert.ToHexStringLower(SHA256.HashData(bytes))!=expected)
            throw new InvalidDataException(subject+" SHA256 differs from selected reviewed identity");
    }
    private static bool Digest(string? text)=>text is {Length:64}&&text.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string RequiredDigest(JsonElement row,string name)
    {string text=RequiredText(row,name);return Digest(text)?text:throw new InvalidDataException("Invalid cohort digest: "+name);}
    private static string RequiredText(JsonElement row,string name)=>row.GetProperty(name).GetString() is {Length:>0} text
        ?text:throw new InvalidDataException("Missing cohort text: "+name);
}
