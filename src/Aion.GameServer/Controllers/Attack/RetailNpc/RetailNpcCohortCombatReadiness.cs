using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Admission for reviewed data-driven ordinary cohorts. Reconstructs reached
/// source timing; it never supplies actor/getter/RNG/HP answers or publishes a catalog.</summary>
internal static class RetailNpcCohortCombatReadiness
{
    private const string NpcSha="c24e600107352268d377c9a7361e52d848e96af8deff61259e031b98b7130350";
    private const string HostSkillsSha="e06ee05fa4f56bc4107c1be929dc0fecb8ec8a8dfbeaf26a235446461a14ace0";
    private static readonly string[] WeaponTypes=["noweapon","1h_sword","1h_dagger","1h_mace","2h_sword",
        "2h_polearm","2h_staff","2h_keyblade","2h_harp","2h_orb","2h_book","1h_gun","2h_cannon","bow",
        "1h_toolhoe","2h_toolpick","2h_toolrod","2h_toolaxe"];
    private static readonly Dictionary<string,string> NativeBodies=new(StringComparer.Ordinal)
    {
        ["0x1401b4630"]="e698b70eace8e01ce5015e22cde87a4fbfccc9eb73371232bf13f1ac07f199b2",
        ["0x1401b5490"]="8768e37ff1167d9912b95ab3e3dce825ef9af5580bc4b9e6d8e11d65f09bd202",
        ["0x1401b76c0"]="2e4a5ebe640a25d792cc39c0ae18e7340f15c7436470377839c266c3863f2f8a",
        ["0x140292a50"]="a9a9393f434650dbd30b31ebb7c3a2d94aa77d3132ec3624cc421428ad7efcfd",
        ["0x1400dc880"]="d404d39479c8231fdb91b76cc3af6d3f9bbe482900d25ea4bbf8938b3d35d52b",
        ["0x1403169a0"]="6e4460802a5cc961e49325d1904f800c2e7f21cc64ec6e73629f2e74c2bbd2bf"
    };

    internal static void Validate(IReadOnlyCollection<SkyTempleNpcProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        Require(profiles.Count>0&&profiles.Count<=4096&&profiles.All(p=>p is not null)
            &&profiles.Select(p=>(p.Cohort?.MapId,p.NpcId)).Distinct().Count()==profiles.Count,
            "Unique bounded cohort profiles are required");
        var assets=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
        var selected=profiles.SelectMany(p=>p.Skills).Select(s=>s.Id48).Distinct().ToArray();
        var expected=selected.Length==0?null:ReadHostSkills(selected);
        if(selected.Contains(17039))
        {
            var child=DataManager.SKILL_DATA.GetSkillTemplate(8217);var reference=expected?.GetSkillTemplate(8217);
            Require(child is not null&&reference is not null&&RetailNpcHostIdentity.Skill(child)==RetailNpcHostIdentity.Skill(reference),
                "Current complete privately produced Stagger child graph differs");
        }
        foreach(var p in profiles)
        {
            var c=p.Cohort;Require(c is not null,"Generic profile has no reviewed cohort binding");
            Require(c.NormalMotion is not null&&c.SpellSources is not null&&!c.SkillMotions.IsDefault,
                "Incomplete immutable cohort metadata");
            Require(IsSha(c.ManifestSha256)&&p.SourceRecordSha256.Length==64&&p.Source.Receipt.NpcSha256==NpcSha
                &&p.MaxHp>0&&p.MinDamage>0&&p.MaxDamage>=p.MinDamage&&p.MaxDamage<=ushort.MaxValue
                &&p.StatRatio is >0 and <=ushort.MaxValue&&p.HitCount==1&&p.Level is >=1 and <=100
                &&p.AttackDelay is >=500 and <=10000&&p.Attacks.Length==1,
                "Unsupported constructed cohort combat profile: "+p.NpcId);
            var host=DataManager.NPC_DATA.GetNpcTemplate(p.NpcId);
            Require(host is not null&&host.GetLevel()==c.ExpectedHostLevel&&host.GetAiName()==c.ExpectedHostAi
                &&RetailNpcHostIdentity.Template(host)==c.HostTemplateSha256,"Current4.8 NPC graph differs: "+p.NpcId);
            var a=p.Attacks[0];Require(a.Ordinal==0&&a.Weight>0&&a.Modifier==100&&float.IsFinite(a.Range)
                &&a.Range is >0 and <=255,"Unsupported source normal row: "+p.NpcId);
            _=SkyTempleCombatInputs.Element(a.Type);
            Require(p.Skills.Select(s=>s.Ordinal).SequenceEqual(Enumerable.Range(0,p.Skills.Length))
                &&c.SkillMotions.Length==p.Skills.Length&&c.SkillMotions.Select(s=>s.Ordinal).SequenceEqual(Enumerable.Range(0,p.Skills.Length))
                &&p.Skills.Select(s=>(s.Id48,s.Level)).Distinct().Count()==p.Skills.Length,
                "Skill row order/duplicate runtime timing identity differs: "+p.NpcId);
            Require(!c.NormalMotion.Evidence.IsDefault&&c.NormalMotion.Evidence.Length is >0 and <=1_048_576
                &&Hash(c.NormalMotion.Evidence.AsSpan())==c.NormalMotion.EvidenceSha256,"Normal source evidence is missing or changed");
            using var document=JsonDocument.Parse(c.NormalMotion.Evidence.ToArray(),new JsonDocumentOptions{MaxDepth=64});
            var e=document.RootElement;
            Require(e.GetProperty("schema_version").GetInt32()==1&&Text(e,"kind")=="native-source-constructed-normal-motion"
                &&e.GetProperty("npc_id").GetInt32()==p.NpcId&&Text(e,"source_record_sha256")==p.SourceRecordSha256,
                "Normal evidence source identity differs");
            var context=e.GetProperty("source_context");
            Require(Text(context,"dataset_id")==p.DatasetId&&Text(context,"manifest_sha256")==p.Source.Receipt.OutputManifestSha256
                &&Text(context,"npc_region")==p.Source.Receipt.Region,"Source context was retargeted");
            var binary=e.GetProperty("native_binary");Require(Text(binary,"sha256")==NpcSha,"Native build identity differs");
            _=Asset(binary,assets,100_000_000);
            var bodies=e.GetProperty("native_body_pins").EnumerateArray().ToArray();
            foreach(var pair in NativeBodies)
                Require(bodies.Count(x=>Text(x,"va")==pair.Key&&Text(x,"file_backed_body_sha256")==pair.Value)==1,
                    "Known native source/body receipt differs: "+pair.Key);
            var source=e.GetProperty("npc_source");
            Require(Text(source,"path")==p.Source.Receipt.SourcePath&&source.GetProperty("byte_start").GetInt64()==p.Source.Receipt.ByteStart
                &&source.GetProperty("byte_end_exclusive").GetInt64()==p.Source.Receipt.ByteEndExclusive
                &&Text(source,"raw_sha256")==p.SourceRecordSha256,"NPC source span differs");
            XElement npc=Span(source);Require(npc.Name=="npc"&&Leaf(npc,"id")==p.NpcId.ToString(CultureInfo.InvariantCulture)
                &&Leaf(npc,"name")==p.Devname,"Actual source NPC identity differs");
            string? mesh=Leaf(npc,"mesh"),dir=Leaf(npc,"dir");
            Require(mesh==OptionalText(e,"mesh")&&dir==OptionalText(e,"dir")
                &&mesh==Raw(p,"mesh")&&dir==Raw(p,"dir"),"Model source selection differs");
            XElement? animations=null;string suffix="";var normal=e.GetProperty("normal");
            if(mesh is null&&dir is null)
            {
                // Both native lookup entrypoints return0 before category/cache access;
                // ObjectClass1b76c0 restores all4words300/count1 on that exact failure.
                Require(e.TryGetProperty("normal_case",out var nativeCase)&&nativeCase.GetString()=="both_model_null_lookup_failure"
                    &&c.NormalMotion.VariantCount==1&&c.NormalMotion.FirstHit==300&&p.Skills.Length==0,
                    "Only the proved both-model-null normal fallback is admitted");
            }
            else
            {
                int[] categories=Categories(npc,e.GetProperty("weapon_binding"));suffix=Suffix(categories[0],categories[1]);
                Require(suffix==Text(e.GetProperty("weapon_binding"),"suffix"),"Weapon suffix differs");
                var marker=normal.GetProperty("marker_asset");string selectedModel=mesh??dir!;
                Require(!string.IsNullOrEmpty(selectedModel)&&string.Equals(Path.GetFileName(Text(marker,"path")),selectedModel+".xml",StringComparison.OrdinalIgnoreCase),
                    "Marker filename is not the selected model");
                animations=Xml(Asset(marker,assets,20_000_000),false);Require(animations.Name=="animations","Marker root differs");
                var fast=Normal(animations,suffix,false);var slow=Normal(animations,suffix,true);
                Require(fast.Count is 1 or 2&&fast.Count==slow.Count&&fast.Words.Take(fast.Count).SequenceEqual(slow.Words.Take(slow.Count))
                    &&fast.Count==c.NormalMotion.VariantCount&&fast.Words[0]==c.NormalMotion.FirstHit
                    &&(fast.Count==1?c.NormalMotion.SecondHit is null:fast.Words[1]==c.NormalMotion.SecondHit),
                    "Actual marker variants differ from admitted normal timing");
                Require(normal.GetProperty("native_lookup_succeeded").GetBoolean()&&normal.GetProperty("variant_count").GetInt32()==fast.Count,
                    "Normal fallback/variant result was substituted");
            }
            var sourceSkillRows=e.GetProperty("skill_motions").EnumerateArray().ToArray();
            Require(sourceSkillRows.Length==p.Skills.Length,"Evidence omitted a source skill");
            foreach(var s in p.Skills)
            {
                Require(animations is not null,"Skill missing-model fallback is outside this finite consumer");
                Require((Raw(p,"dual") is null or "0")&&Raw(p,"exclusive_attribute") is null
                    &&(Raw(p,"magical_damage_trim") is null or "0"),"Unsupported ordinary NPC spell source branch");
                Require(s.Id48 is 16561 or 16590 or 16725,"This cohort release admits only the three reviewed exported spell recipes");
                Require(s.Level is >0 and <=short.MaxValue&&s.Count>=0&&s.Rate>=0&&s.Delay>=0,"Unsupported skill row values");
                Require(c.SpellSources.TryGetValue(s.Id48,out var bytes)&&!bytes.IsDefault,"Unreviewed cohort skill recipe: "+s.Id48);
                var spell=RetailNpcSpellInstantParameters.ProjectCohortSpell(bytes.AsSpan(),p,s,RetailNpcSpellInstantParameters.RequiredPins);
                XElement skill=Xml(bytes.AsSpan().ToArray(),true);var t=DataManager.SKILL_DATA.GetSkillTemplate(s.Id48);
                var reference=expected?.GetSkillTemplate(s.Id48);
                Require(t is not null&&reference is not null&&t.GetType()==typeof(SkillTemplate)
                    &&RetailNpcHostIdentity.Skill(t)==RetailNpcHostIdentity.Skill(reference),"Current full4.8 skill graph differs: "+s.Id48);
                var tp=t.GetProperties();var rp=reference.GetProperties();
                Require(tp is not null&&rp is not null&&tp.GetFirstTarget()==rp.GetFirstTarget()&&tp.GetTargetType()==rp.GetTargetType()
                    &&tp.GetTargetRelation()==rp.GetTargetRelation()&&tp.GetTargetSpecies()==rp.GetTargetSpecies(),"Resolved target program differs");
                var m=c.SkillMotions[s.Ordinal];var sr=sourceSkillRows[s.Ordinal];
                Require(m.Ordinal==s.Ordinal&&m.SkillId==s.Id48&&m.Level==s.Level&&m.EvidenceSha256==c.NormalMotion.EvidenceSha256
                    &&sr.GetProperty("ordinal").GetInt32()==s.Ordinal&&sr.GetProperty("skill_id").GetInt32()==s.Id48
                    &&sr.GetProperty("level").GetInt32()==s.Level&&sr.GetProperty("blockers").GetArrayLength()==0,
                    "Skill motion source/ordinal or unsupported recipe differs");
                uint cast=UInt(skill,"casting_delay"),reuse=UInt(skill,"delay_time");
                Require(Leaf(skill,"pre_fx_delay") is null or "0","Nonzero preFX is outside this finite consumer");
                float ammo=Float(skill,"ammo_speed");string motion=Leaf(skill,"motion_name")??throw new InvalidDataException("Missing source skill motion");
                var sm=SkillMotion(animations,suffix,motion);
                Require(m.FirstHit==sm.First&&m.Recovery==sm.Total&&m.Cast==cast&&m.Reuse==reuse&&m.Ammo==ammo
                    &&t.GetDuration()==cast&&SkyTempleNpcSkillState.ReuseMilliseconds(t.GetCooldown())==reuse&&t.GetAmmoSpeed()==ammo
                    &&string.Equals(t.GetStack(),spell.Name,StringComparison.OrdinalIgnoreCase),"Source/current skill timing differs: "+s.Id48);
            }
        }
    }

    private static (ushort[] Words,int Count) Normal(XElement root,string suffix,bool slow)
    {
        ushort[] words=[300,300,300,300];bool[] written=new bool[4];int count=1;string prefix="cattack"+suffix;
        foreach(var n in root.Elements("animation"))
        {
            string name=(string?)n.Attribute("name")??"";
            if(!name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))continue;
            string tail=slow?name[(name.LastIndexOf('_')+1)..]:name[(prefix.Length)..];
            if(!slow){if(!tail.StartsWith('_'))continue;tail=tail[1..];}
            if(!Variant(tail,out int variant)||!When(n,out ushort hit,out _))continue;
            words[variant-1]=hit;written[variant-1]=true;count=System.Math.Max(count,variant);
        }
        Require(written.Take(count).All(x=>x),"Missing/gapped normal marker; native fallback not admitted");return(words,count);
    }
    private static (ushort First,ushort Total) SkillMotion(XElement root,string suffix,string motion)
    {
        string prefix="cfire"+suffix+"_"+motion+"_";ushort[] fast=[0,0,0,0],slow=[800,800,800,800];ushort total=800;bool found=false;
        foreach(var n in root.Elements("animation"))
        {
            string name=(string?)n.Attribute("name")??"";
            if(!name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||!Variant(name[prefix.Length..],out int v)
                ||!When(n,out ushort hit,out ushort length))continue;
            fast[v-1]=slow[v-1]=hit;total=length;found=true;
        }
        Require(found&&fast[0]==slow[0]&&fast[0]>0&&total>0,"Missing/ambiguous skill marker; no guessed fallback");return(fast[0],total);
    }
    private static bool Variant(string value,out int n)
    {n=0;return value.Length>0&&value.All(x=>x is >= '0' and <= '9')
        &&int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out n)&&n is >=1 and <=4;}
    private static bool When(XElement n,out ushort hit,out ushort total)
    {
        hit=total=0;var h=n.Elements("hitpoint").FirstOrDefault()??n.Elements("shoot").FirstOrDefault();
        string? when=(string?)h?.Attribute("when"),length=(string?)n.Attribute("animation_length")??when;
        return Word(when,out hit)&&Word(length,out total);
    }
    private static bool Word(string? text,out ushort word)
    {
        word=0;if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out double d))return false;
        float seconds=(float)d;if(!float.IsFinite(seconds)||seconds is <0 or >60||seconds!=0&&float.IsSubnormal(seconds))return false;
        float product=seconds*1000f;word=unchecked((ushort)(long)product);return true;
    }
    private static int[] Categories(XElement npc,JsonElement proof)
    {
        var groups=npc.Elements("visible_equipments").ToArray();Require(groups.Length<=1,"Duplicate equipment source");
        var slots=proof.GetProperty("slots").EnumerateArray().ToArray();Require(slots.Length==2,"Weapon category proof is incomplete");
        int[] result=new int[2];string[] names=["main","sub"];
        for(int i=0;i<2;i++)
        {
            string? value=groups.Length==0?null:Leaf(groups[0],names[i]);var r=slots[i];Require(Text(r,"slot")==names[i],"Weapon slot order differs");
            if(value is null){Require(r.GetProperty("category").GetInt32()==0&&!r.TryGetProperty("item",out _),"Absent slot must retain constructor0");continue;}
            var item=r.GetProperty("item");var source=Span(item.GetProperty("source"));
            Require(string.Equals(value,Leaf(source,"name"),StringComparison.OrdinalIgnoreCase),"Weapon source name differs");
            int id=int.Parse(Leaf(source,"id")!,CultureInfo.InvariantCulture);string? type=Leaf(source,"weapon_type");
            int category=id is <100000000 or >=103100000?0:string.Equals(type,"ALLWP",StringComparison.OrdinalIgnoreCase)?18:Array.FindIndex(WeaponTypes,x=>string.Equals(x,type,StringComparison.OrdinalIgnoreCase));
            Require(category>=0&&category==r.GetProperty("category").GetInt32(),"Unproved weapon type/category");result[i]=category;
        }
        return result;
    }
    private static string Suffix(int a,int b)=>a!=0&&b!=0?a switch{11=>"_2gun",18=>"_flame",19=>"_earth",21=>"_wind",_=>"_2weapon"}:
        a switch{0=>"_noweapon",1 or 14=>"_1hand",2=>"_dagger",3=>"_mace",4 or 15 or 16 or 17=>"_2hand",5=>"_polearm",6=>"_staff",7=>"_keyblade",8=>"_harp",9=>"_orb",10=>"_book",11=>"_1gun",12=>"_cannon",13=>"_bow",20=>"_aqua",_=>throw new InvalidDataException("Unproved weapon suffix")};
    private static byte[] Asset(JsonElement pin,Dictionary<string,byte[]> cache,long maximum)
    {
        string path=Text(pin,"path"),hash=Text(pin,"sha256");long length=pin.GetProperty("bytes").GetInt64();
        Require(IsSha(hash)&&length>0&&length<=maximum,"Invalid pinned asset bound");string key=path+"|"+hash;
        if(cache.TryGetValue(key,out var held)){Require(held.LongLength==length,"Cached source length differs");return held;}
        var info=new FileInfo(path);Require(info.Exists&&info.Length==length,"Missing or changed source asset: "+path);
        byte[] bytes=File.ReadAllBytes(path);Require(bytes.LongLength==length&&Hash(bytes)==hash,"Source asset hash differs: "+path);cache[key]=bytes;return bytes;
    }
    private static XElement Span(JsonElement source)
    {
        string path=Text(source,"path");long start=source.GetProperty("byte_start").GetInt64(),end=source.GetProperty("byte_end_exclusive").GetInt64();
        Require(start>=0&&end>start&&end-start<=131072,"Source span is outside finite bound");
        using var file=File.OpenRead(path);Require(end<=file.Length,"Source span exceeds file");file.Position=start;
        byte[] bytes=new byte[checked((int)(end-start))];file.ReadExactly(bytes);Require(Hash(bytes)==Text(source,"raw_sha256"),"Original source span changed");return Xml(bytes,true);
    }
    private static XElement Xml(byte[] bytes,bool utf16)
    {
        var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=20_000_000,MaxCharactersFromEntities=0};
        if(utf16){using var text=new StringReader(new UnicodeEncoding(false,false,true).GetString(bytes));using var reader=XmlReader.Create(text,settings);return XElement.Load(reader,LoadOptions.PreserveWhitespace);}
        using var stream=new MemoryStream(bytes);using var xml=XmlReader.Create(stream,settings);return XElement.Load(xml,LoadOptions.PreserveWhitespace);
    }
    private static SkillData ReadHostSkills(int[] ids)
    {
        string? directory=DataManager.GetRegisteredInstance()?.StaticData.StaticDataDirectory;Require(!string.IsNullOrWhiteSpace(directory),"Current static-data directory is unknown");
        string path=Path.Combine(directory,"skills","skill_templates.xml");var info=new FileInfo(path);
        Require(info.Exists&&info.Length is >0 and <=20_000_000,"Reviewed host skill source is missing or oversized");byte[] bytes=File.ReadAllBytes(path);
        Require(bytes.Length is >0 and <=20_000_000&&Hash(bytes)==HostSkillsSha,"Reviewed complete4.8 skill source changed");
        var wanted=ids.Concat(ids.Contains(17039)?[8217]:Array.Empty<int>()).ToHashSet();
        var doc=Xml(bytes,false);var records=doc.Elements("skill_template").Where(x=>wanted.Contains((int?)x.Attribute("skill_id")??-1)).ToArray();
        Require(records.Length==wanted.Count&&records.Select(x=>(int)x.Attribute("skill_id")!).Distinct().Count()==wanted.Count,"Skill source is missing or duplicated");
        using var text=new StringReader(new XElement("skill_data",records).ToString(SaveOptions.DisableFormatting));
        var holder=(SkillData?)new XmlSerializer(typeof(SkillData)).Deserialize(text);Require(holder is not null,"Current reference skill graph decode failed");holder.AfterUnmarshal(null!);return holder;
    }
    private static string? Raw(SkyTempleNpcProfile p,string key)
    {var values=p.Source.Source.GetRawRootField(key).RawValues;Require(values.Length<=1,"Duplicate model source field");return values.Length==0?null:values[0];}
    private static string? Leaf(XElement root,string name)
    {var values=root.Elements(name).ToArray();Require(values.Length<=1,"Duplicate source field: "+name);return values.Length==0?null:values[0].Value;}
    private static uint UInt(XElement root,string name)=>uint.Parse(Leaf(root,name)??"0",CultureInfo.InvariantCulture);
    private static float Float(XElement root,string name)
    {float value=float.Parse(Leaf(root,name)??"0",CultureInfo.InvariantCulture);Require(float.IsFinite(value)&&value>=0,"Unsupported source float");return value;}
    private static string Text(JsonElement e,string name)=>e.GetProperty(name).GetString()??throw new InvalidDataException("Missing "+name);
    private static string? OptionalText(JsonElement e,string name)=>e.GetProperty(name).ValueKind==JsonValueKind.Null?null:Text(e,name);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static bool IsSha(string value)=>value.Length==64&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Require([DoesNotReturnIf(false)]bool condition,string message)
    {if(!condition)throw new InvalidDataException(message);}
}
