using System.Globalization;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Bootstrap admission for the one reviewed25-NPC package. This checks loaded
/// content and its compiled combat/timing consumers; it does not publish the catalog or
/// assert that a live actor, client, effect, RNG seed or native cache was observed.</summary>
internal static class SkyTempleNpcCombatReadiness
{
    private const string HostSkillsSha256="e06ee05fa4f56bc4107c1be929dc0fecb8ec8a8dfbeaf26a235446461a14ace0";
    private static readonly int[] NpcIds=[212761,212762,212763,212764,212765,212766,212767,212768,212769,
        212770,212771,212772,212773,212774,212775,212776,212777,212778,212779,212780,212781,212782,212833,212835,212876];

    internal static void Validate(IReadOnlyCollection<SkyTempleNpcProfile> profiles,string? staticDataDirectory=null)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        if(profiles.Count!=25||!profiles.Select(p=>p.NpcId).ToHashSet().SetEquals(NpcIds)||profiles.Select(p=>p.NpcId).Distinct().Count()!=25)
            throw new InvalidDataException("SkyTemple readiness requires the exact25 unique profiles");
        var bindings=profiles.SelectMany(p=>p.Skills).ToArray();
        if(bindings.Length!=59||!bindings.Select(s=>s.Id48).ToHashSet().SetEquals(SkyTempleNpcCombat.SelectedSkills))
            throw new InvalidDataException("SkyTemple readiness requires all59 bindings and20 supported skills");
        var expected=ReadHostSkills(staticDataDirectory??DataManager.GetRegisteredInstance()?.StaticData.StaticDataDirectory);
        var serializer=new XmlSerializer(typeof(SkillTemplate));
        foreach(int id in SkyTempleNpcCombat.SelectedSkills.Concat([8217,8223]))
        {
            var actual=DataManager.SKILL_DATA.GetSkillTemplate(id)
                ??throw new InvalidDataException("SkyTemple loaded skill is missing: "+id);
            var reference=expected.GetSkillTemplate(id)
                ??throw new InvalidDataException("SkyTemple source skill is missing: "+id);
            // Compare the complete XML-bound graph, including effect subclasses, all
            // conditions/modifiers, preeffects, subeffects, durations and motion fields.
            // Both graphs use SkillData's actual child initialization (including NoResist).
            if(actual.GetType()!=typeof(SkillTemplate)||Serialize(serializer,actual)!=Serialize(serializer,reference))
                throw new InvalidDataException("SkyTemple loaded skill graph differs from reviewed4.8 content: "+id);
            var a=actual.GetProperties()??throw new InvalidDataException("SkyTemple skill properties are missing: "+id);
            var r=reference.GetProperties()??throw new InvalidDataException("SkyTemple source properties are missing: "+id);
            // Nullable enum readers have private lazy caches. Verify the values consumed
            // by real targeting as well as their serialized source tokens.
            if(a.GetFirstTarget()!=r.GetFirstTarget()||a.GetTargetType()!=r.GetTargetType()
                ||a.GetTargetRelation()!=r.GetTargetRelation()||a.GetTargetSpecies()!=r.GetTargetSpecies())
                throw new InvalidDataException("SkyTemple resolved target properties differ: "+id);
        }
        foreach(var p in profiles)
        {
            if(p.DatasetId!=SkyTempleNpcProfileCatalog.DatasetId||p.Generation!=SkyTempleNpcProfileCatalog.ArtifactSha256
                ||p.SourceRecordSha256.Length!=64||p.MaxHp<=0||p.MinDamage<=0||p.MaxDamage<p.MinDamage
                ||p.MaxDamage>ushort.MaxValue||p.HitCount!=1||p.StatRatio!=1000||p.Level is <30 or >33
                ||p.AttackDelay is <500 or >10000||p.Attacks.Length!=1)
                throw new InvalidDataException("SkyTemple constructed profile differs: "+p.NpcId);
            if(DataManager.NPC_DATA.GetNpcTemplate(p.NpcId) is null)
                throw new InvalidDataException("SkyTemple4.8 NPC content is missing: "+p.NpcId);
            var attack=p.Attacks[0];
            if(attack.Ordinal!=0||attack.Weight!=1000||attack.Modifier!=100||!float.IsFinite(attack.Range)||attack.Range is <=0 or >255)
                throw new InvalidDataException("SkyTemple normal attack row differs: "+p.NpcId);
            _=SkyTempleCombatInputs.Element(attack.Type);
            var normal=SkyTempleNpcCombat.NormalMotion(p.NpcId);
            if(normal.First==0||normal.Second==0)throw new InvalidDataException("SkyTemple normal animation is missing: "+p.NpcId);
            var ammo=p.Source.Source.GetRawRootField("ammo_speed").RawValues;
            float speed=0;
            if(ammo.Length>1||(ammo.Length==1&&(!float.TryParse(ammo[0],NumberStyles.Float,CultureInfo.InvariantCulture,out speed)||!float.IsFinite(speed))))
                throw new InvalidDataException("SkyTemple normal projectile source is malformed: "+p.NpcId);
            float sourceSpeed=p.NpcId is 212761 or 212762 or 212763 or 212764 or 212833?50f:0f;
            if(speed!=sourceSpeed)throw new InvalidDataException("SkyTemple normal projectile binding differs: "+p.NpcId);
            if(!p.Skills.Select(s=>s.Ordinal).SequenceEqual(Enumerable.Range(0,p.Skills.Length)))
                throw new InvalidDataException("SkyTemple skill source order differs: "+p.NpcId);
            foreach(var s in p.Skills)
            {
                var actual=DataManager.SKILL_DATA.GetSkillTemplate(s.Id48)
                    ??throw new InvalidDataException("SkyTemple loaded binding is missing: "+s.Id48);
                var motion=SkyTempleNpcSkillTiming.SourceMotion(p.NpcId,s.Id48,s.Level);
                if(s.Level<=0||s.Count<0||s.Rate<0||s.Delay<0
                    ||!string.Equals(s.Name,actual.GetStack(),StringComparison.OrdinalIgnoreCase)
                    ||actual.GetDuration()!=motion.Cast||SkyTempleNpcSkillState.ReuseMilliseconds(actual.GetCooldown())!=motion.Reuse
                    ||actual.GetAmmoSpeed()!=motion.Ammo||motion.FirstHit==0||motion.Recovery==0)
                    throw new InvalidDataException($"SkyTemple source/header/motion binding differs: {p.NpcId}/{s.Ordinal}/{s.Id48}");
            }
        }
    }

    private static SkillData ReadHostSkills(string? directory)
    {
        if(string.IsNullOrWhiteSpace(directory))throw new InvalidDataException("SkyTemple loaded static-data directory is unknown");
        string path=Path.Combine(directory,"skills","skill_templates.xml");
        var file=new FileInfo(path);
        if(!file.Exists||file.Length is <=0 or >20_000_000)throw new InvalidDataException("SkyTemple reviewed skill source is missing: "+path);
        byte[] bytes=File.ReadAllBytes(path);
        if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=HostSkillsSha256)
            throw new InvalidDataException("SkyTemple reviewed4.8 skill source SHA256 differs");
        using var stream=new MemoryStream(bytes);
        using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=20_000_000});
        var document=XDocument.Load(reader,LoadOptions.PreserveWhitespace);
        var ids=SkyTempleNpcCombat.SelectedSkills.Concat([8217,8223]).ToHashSet();
        var records=document.Root?.Elements("skill_template").Where(x=>int.TryParse((string?)x.Attribute("skill_id"),out int id)&&ids.Contains(id)).ToArray()
            ??throw new InvalidDataException("SkyTemple skill source has no root");
        if(records.Length!=22||records.Select(x=>(int)x.Attribute("skill_id")!).Distinct().Count()!=22)
            throw new InvalidDataException("SkyTemple skill source is incomplete or ambiguous");
        // Deserialize only the22 reached records, not another complete skill catalog.
        using var selected=new StringReader(new XElement("skill_data",records).ToString(SaveOptions.DisableFormatting));
        var holder=(SkillData?)new XmlSerializer(typeof(SkillData)).Deserialize(selected)
            ??throw new InvalidDataException("SkyTemple source skill decode failed");
        holder.AfterUnmarshal(null!);return holder;
    }

    private static string Serialize(XmlSerializer serializer,SkillTemplate template)
    {
        using var writer=new StringWriter(CultureInfo.InvariantCulture);
        serializer.Serialize(writer,template);return writer.ToString();
    }
}
