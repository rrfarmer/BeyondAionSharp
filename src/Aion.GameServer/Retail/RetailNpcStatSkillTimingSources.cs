using System.Collections.Frozen;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders.RetailNpc;

namespace Aion.GameServer.Retail;

/// <summary>Pinned source-marker data for selected statistics cohorts. Loading
/// timing evidence alone does not admit or schedule any NPC skill.</summary>
internal sealed class RetailNpcStatSkillTimingSources
{
    internal sealed record Entry(int MapId,int NpcId,int SkillId,int Level,ushort Word,string ReportSha256);
    private readonly FrozenDictionary<(int Map,int Npc,int Skill,int Level),Entry> entries;
    private RetailNpcStatSkillTimingSources(Dictionary<(int,int,int,int),Entry> entries)=>this.entries=entries.ToFrozenDictionary();
    internal int Count=>entries.Count;
    internal bool TryGet(int map,int npc,int skill,int level,out Entry entry)=>entries.TryGetValue((map,npc,skill,level),out entry!);

    internal static RetailNpcStatSkillTimingSources Parse(byte[] data,string hash,
        IReadOnlyDictionary<(int,int),RetailNpcStatCatalog.Member> profiles,
        IReadOnlyDictionary<int,RetailNpcStatSkillCombat.Source> skills,Func<int,byte[]> sourceBytes)
    {
        if(data.Length is <=0 or >8_000_000 || Digest(data)!=hash)throw new InvalidDataException("Timing context hash differs");
        using var doc=JsonDocument.Parse(data,new JsonDocumentOptions {MaxDepth=128});var root=doc.RootElement;
        RetailNpcDatasetValidation.UniqueJson(root);
        if(root.GetProperty("schema_version").GetInt32()!=1 ||root.GetProperty("kind").GetString()!="selected_retail_npc_drain_skill_timing")
            throw new InvalidDataException("Unreviewed timing context");
        string parent=root.GetProperty("source_manifest_sha256").GetString()??throw new InvalidDataException("Timing parent missing");
        if(parent.Length!=64 ||parent.Any(c=>!Uri.IsHexDigit(c)))throw new InvalidDataException("Timing parent hash malformed");
        var records=root.GetProperty("records").EnumerateArray().ToArray();
        if(records.Length is <1 or >65536)throw new InvalidDataException("Timing context record count differs");
        var result=new Dictionary<(int,int,int,int),Entry>();var checkedSkills=new HashSet<int>();
        foreach(var row in records)
        {
            int map=row.GetProperty("map_id").GetInt32(),npc=row.GetProperty("npc_id").GetInt32(),id=row.GetProperty("skill_id").GetInt32();
            if(!profiles.TryGetValue((map,npc),out var member)||!skills.TryGetValue(id,out var source)
                ||row.GetProperty("npc_source_sha256").GetString()!=member.Profile.SourceRecordSha256
                ||row.GetProperty("skill_source_sha256").GetString()!=source.Sha256)
                throw new InvalidDataException("Timing profile or skill source differs");
            if(checkedSkills.Add(id))
            {
                byte[] raw=sourceBytes(id);if(Digest(raw)!=source.Sha256)throw new InvalidDataException("Timing skill hash differs");
                using var reader=XmlReader.Create(new MemoryStream(raw,false),new XmlReaderSettings
                {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=200_000});
                var x=XDocument.Load(reader).Root;
                if(x?.Name!="skill_base" ||(string?)x.Element("id")!=id.ToString(CultureInfo.InvariantCulture)
                    ||(string?)x.Element("effect1_type")!="SpellATKDrain_Instant" ||x.Elements("pre_fx_delay").Any()
                    ||x.Elements("ammo_speed").Any())throw new InvalidDataException("Timing source needs reviewed zero delay and projectile speed");
            }
            var report=row.GetProperty("report");string reportHash=report.GetProperty("sha256").GetString()??"";
            byte[] reportBytes=ReadChecked(report.GetProperty("path").GetString(),reportHash,2_000_000);
            using var reportDoc=JsonDocument.Parse(reportBytes);var evidence=reportDoc.RootElement;
            RetailNpcDatasetValidation.UniqueJson(evidence);
            if(evidence.GetProperty("map").GetInt32()!=map ||evidence.GetProperty("npc").GetInt32()!=npc
                ||evidence.GetProperty("skill").GetInt32()!=id ||evidence.GetProperty("manifest_sha256").GetString()!=parent
                ||evidence.GetProperty("npc_source_sha256").GetString()!=member.Profile.SourceRecordSha256
                ||evidence.GetProperty("skill_source_sha256").GetString()!=source.Sha256)
                throw new InvalidDataException("Timing report binding differs");
            int sourceMap=evidence.TryGetProperty("source_map",out var origin)?origin.GetInt32():map;
            if(sourceMap!=map && (!profiles.TryGetValue((sourceMap,npc),out var sourceMember)
                ||sourceMember.Profile.SourceRecordSha256!=member.Profile.SourceRecordSha256
                ||sourceMember.HostTemplateSha256!=member.HostTemplateSha256))
                throw new InvalidDataException("Timing source-map membership differs");
            ushort word=ReadMarkerWord(evidence);
            int[] levels=row.GetProperty("levels").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            if(levels.Length==0 ||levels.Distinct().Count()!=levels.Length)throw new InvalidDataException("Timing levels are empty or duplicated");
            foreach(int level in levels)
            {
                if(level is <1 or >short.MaxValue ||!RetailNpcStatCatalog.MatchesSelectedSkillBinding(member.Profile,id,level,source))
                    throw new InvalidDataException($"Timing skill level binding differs: map={map} npc={npc} skill={id} level={level}");
                if(!result.TryAdd((map,npc,id,level),new(map,npc,id,level,word,reportHash)))
                    throw new InvalidDataException("Duplicate timing binding");
            }
        }
        return new(result);
    }

    internal static ushort ReadMarkerWord(JsonElement root)
    {
        var rows=root.GetProperty("markers").EnumerateArray().ToArray();
        if(rows.Length!=1 ||rows[0].GetProperty("variant").GetInt32()!=1
            ||!rows[0].GetProperty("hit_word_ms").TryGetUInt16(out ushort word))
            throw new InvalidDataException("Timing requires one selected first marker");
        byte[] raw=ReadChecked(root.GetProperty("marker_path").GetString(),root.GetProperty("marker_sha256").GetString(),20_000_000);
        using var reader=XmlReader.Create(new MemoryStream(raw,false),new XmlReaderSettings
        {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=20_000_000});
        var marker=XDocument.Load(reader).Root;string name=rows[0].GetProperty("name").GetString()??"";
        string prefix="cfire"+root.GetProperty("weapon_suffix").GetString()+"_"+root.GetProperty("motion").GetString()+"_";
        if(!name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)
            ||!int.TryParse(name[prefix.Length..],NumberStyles.None,CultureInfo.InvariantCulture,out int variant) ||variant!=1)
            throw new InvalidDataException("Timing marker selection differs");
        var animations=marker?.Elements("animation").Where(x=>string.Equals((string?)x.Attribute("name"),name,StringComparison.OrdinalIgnoreCase)).ToArray()??[];
        if(marker?.Name!="animations" ||animations.Length!=1)throw new InvalidDataException("Timing marker animation differs");
        var events=animations[0].Elements().Where(x=>x.Name.LocalName is "hitpoint" or "shoot").ToArray();
        if(events.Length!=1 ||events[0].Name.LocalName!=rows[0].GetProperty("event").GetString()
            ||!float.TryParse((string?)events[0].Attribute("when"),NumberStyles.Float,CultureInfo.InvariantCulture,out float seconds)
            ||!float.IsFinite(seconds) ||seconds<0 ||seconds>60 ||(ushort)(int)(seconds*1000f)!=word)
            throw new InvalidDataException("Timing marker time differs from its asset");
        return word;
    }

    private static byte[] ReadChecked(string? path,string? hash,int max)
    {
        if(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash))throw new InvalidDataException("Timing asset identity missing");
        var file=new FileInfo(path);if(!file.Exists ||file.Length is <=0 ||file.Length>max)throw new InvalidDataException("Timing asset missing or oversized");
        byte[] bytes=File.ReadAllBytes(path);if(Digest(bytes)!=hash)throw new InvalidDataException("Timing asset hash differs");return bytes;
    }
    private static string Digest(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
