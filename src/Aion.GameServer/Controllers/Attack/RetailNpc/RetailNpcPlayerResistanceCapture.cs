using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using ChangeFunc = Aion.GameServer.SkillEngine.Change.Func;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Point-in-time source/owner witness for offline vampire fixtures.
/// This does not provide a production player snapshot or authorize a skill.</summary>
internal static class RetailNpcPlayerResistanceCapture
{
    internal sealed record Binding(int Id,int Level,string Stack,int Position,RetailNpcStatBuffs.Source Source);
    internal sealed record RetailOnlyBinding(int Id,int Level,string Stack,int HostPercent,int BasicLevel,RetailNpcStatBuffs.Source Source);
    internal sealed record LinkedBinding(int Id,int ChildId,int Level,string Stack,string ChildStack,int BasicLevel,int Model,int Value);

    internal sealed class Catalog
    {
        private readonly Dictionary<int,Binding> bindings;
        private readonly Dictionary<int,RetailOnlyBinding> retailOnly;
        private readonly Dictionary<int,LinkedBinding> linked;
        private Catalog(Dictionary<int,Binding> bindings,Dictionary<int,RetailOnlyBinding>? retailOnly=null,Dictionary<int,LinkedBinding>? linked=null)
        { this.bindings=bindings;this.retailOnly=retailOnly??[];this.linked=linked??[]; }
        internal bool TryGet(int id,out Binding binding) => bindings.TryGetValue(id,out binding!);
        internal bool TryGetRetailOnly(int id,out RetailOnlyBinding binding) => retailOnly.TryGetValue(id,out binding!);
        internal bool TryGetLinkedChild(int id,out LinkedBinding binding)=>linked.TryGetValue(id,out binding!);
        internal LinkedBinding? LinkedParent(int id)=>linked.Values.SingleOrDefault(b=>b.Id==id);

        // In this reviewed fixture, the 4.8 child owns the modifier which the
        // 5.8 parent owns. Both active host effects are required to observe it.
        internal Catalog WithLinkedForOfflineFixture(ReadOnlySpan<byte> bytes,string expectedSha,string directory)
        {
            if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=expectedSha)throw new InvalidDataException("Linked resistance catalog hash differs");
            using var doc=JsonDocument.Parse(bytes.ToArray());var root=doc.RootElement;
            if(root.GetProperty("version").GetInt32()!=1||root.GetProperty("purpose").GetString()!="offline-parent-child-general-resistance")
                throw new InvalidDataException("Linked resistance catalog identity differs");
            var extra=new Dictionary<int,LinkedBinding>();var parents=new HashSet<int>();
            foreach(var row in root.GetProperty("records").EnumerateArray())
            {
                int id=row.GetProperty("id").GetInt32(),child=row.GetProperty("child_id").GetInt32(),level=row.GetProperty("level").GetInt32();
                int basic=row.GetProperty("basic_level").GetInt32(),model=row.GetProperty("model").GetInt32();
                string name=row.GetProperty("retail_name").GetString()!,childName=row.GetProperty("retail_child_name").GetString()!;
                string stack=row.GetProperty("stack").GetString()!,childStack=row.GetProperty("child_stack").GetString()!;
                string file=row.GetProperty("file").GetString()!,sha=row.GetProperty("sha256").GetString()!;
                if(id<=0||child<=0||level<=0||basic<=0||model<=0||file!=$"skill-{id}.utf16le.xml"
                    ||bindings.ContainsKey(child)||retailOnly.ContainsKey(id)||!parents.Add(id)
                    ||Regex.Replace(name.ToUpperInvariant(),@"_G[0-9]+(?=_|$)","")!=stack
                    ||Regex.Replace(childName.ToUpperInvariant(),@"_G[0-9]+(?=_|$)","")!=childStack)
                    throw new InvalidDataException("Invalid linked resistance binding");
                byte[] raw=File.ReadAllBytes(Path.Combine(directory,file));
                if(raw.Length==0||raw.Length>131072||raw.Length%2!=0||Convert.ToHexStringLower(SHA256.HashData(raw))!=sha)
                    throw new InvalidDataException("Linked resistance source hash differs");
                using var reader=XmlReader.Create(new StringReader(new UnicodeEncoding(false,false,true).GetString(raw)),
                    new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536});
                var source=XDocument.Load(reader).Root;
                string? Field(string field)
                {
                    var nodes=source?.Elements(field).ToArray();
                    return nodes is {Length:1} && !nodes[0].HasAttributes && !nodes[0].HasElements?nodes[0].Value:null;
                }
                if(source?.Name!="skill_base" || Field("id")!=id.ToString()||Field("name")!=name
                    ||Field("effect1_type")!="SkillLauncher"||Field("effect1_noresist")!="1"||Field("effect1_reserved1")!=childName
                    ||Field("effect1_reserved2")!="1"||Field("effect2_type")!="ShapeChange"
                    ||Field("effect2_noresist")!="1"||Field("effect2_target_type")!="Target_Only"
                    ||Field("effect2_cond_preeffect_prob1")!="0"||Field("effect2_cond_preeffect_prob2")!="100"
                    ||Field("effect2_effectid")!="175"||Field("effect2_basiclv")!=basic.ToString()
                    ||Field("effect2_remain2")!="600000"||Field("effect2_cond_preeffect")!="e1"
                    ||Field("effect4_type")!="StatUp"||Field("effect4_noresist")!="1"||Field("effect4_target_type")!="Target_Only"
                    ||Field("effect4_cond_preeffect_prob1")!="0"||Field("effect4_cond_preeffect_prob2")!="100"
                    ||Field("effect4_basiclv")!=basic.ToString()
                    ||Field("effect4_remain2")!="600000"||Field("effect4_cond_preeffect")!="e1_2"
                    ||!string.Equals(Field("effect4_reserved18"),"arAll",StringComparison.OrdinalIgnoreCase)
                    ||Field("effect4_reserved15")!="0"||Field("effect4_reserved16")!="100000"||Field("effect4_reserved17")!="0"
                    ||!extra.TryAdd(child,new(id,child,level,stack,childStack,basic,model,100000)))
                    throw new InvalidDataException("Unreviewed linked resistance source");
            }
            if(extra.Count==0)throw new InvalidDataException("Empty linked resistance catalog");
            return new(bindings,retailOnly,extra);
        }

        // These sources carry a retail resistance absent from the corresponding
        // host effect. They are opt-in fixture data, never gameplay admission.
        internal Catalog WithRetailOnlyForOfflineFixture(ReadOnlySpan<byte> bytes,string expectedSha,string directory)
        {
            if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=expectedSha)throw new InvalidDataException("Retail-only resistance catalog hash differs");
            using var doc=JsonDocument.Parse(bytes.ToArray());var root=doc.RootElement;
            if(root.GetProperty("version").GetInt32()!=1 || root.GetProperty("purpose").GetString()!="offline-player-retail-only-general-resistance")
                throw new InvalidDataException("Retail-only resistance catalog identity differs");
            var extra=new Dictionary<int,RetailOnlyBinding>();
            foreach(var row in root.GetProperty("records").EnumerateArray())
            {
                int id=row.GetProperty("id").GetInt32(),level=row.GetProperty("level").GetInt32();
                int percent=row.GetProperty("host_percent").GetInt32(),basic=row.GetProperty("basic_level").GetInt32();
                string stack=row.GetProperty("stack").GetString()!,name=row.GetProperty("retail_name").GetString()!;
                string file=row.GetProperty("file").GetString()!,sha=row.GetProperty("sha256").GetString()!;
                if(id<=0||level<=0||percent<=0||basic<=0||file!=$"skill-{id}.utf16le.xml"||bindings.ContainsKey(id)
                    ||Regex.Replace(name.ToUpperInvariant(),@"_G[0-9]+(?=_|$)","")!=stack)
                    throw new InvalidDataException("Invalid retail-only resistance binding");
                var source=RetailNpcStatBuffs.ParsePlayerResistance(File.ReadAllBytes(Path.Combine(directory,file)),sha);
                var program=source.Programs.SingleOrDefault(p=>p.Position==2);
                var parts=program?.Parts.Where(p=>p.Stat==StatEnum.ABNORMAL_RESISTANCE_ALL).ToArray();
                if(source.Id!=id||source.Name!=name||program is null||!program.Supported
                    ||parts is not {Length:1}||parts[0].Percent||parts[0].Delta!=0||parts[0].Value<=0
                    ||!extra.TryAdd(id,new(id,level,stack,percent,basic,source)))
                    throw new InvalidDataException("Unreviewed retail-only resistance source");
            }
            if(extra.Count==0)throw new InvalidDataException("Empty retail-only resistance catalog");
            return new(bindings,extra,linked);
        }

        // The caller pins the complete catalog; every referenced record is
        // independently hash-checked by the existing retail StatUp parser.
        internal static Catalog LoadForOfflineFixture(ReadOnlySpan<byte> bytes,string expectedSha,string directory)
        {
            if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=expectedSha)
                throw new InvalidDataException("Player resistance catalog hash differs");
            using var doc=JsonDocument.Parse(bytes.ToArray());var root=doc.RootElement;
            if(root.GetProperty("version").GetInt32()!=1)throw new InvalidDataException("Player resistance catalog version differs");
            var entries=new Dictionary<int,Binding>();
            foreach(var row in root.GetProperty("records").EnumerateArray())
            {
                int id=row.GetProperty("id").GetInt32(),level=row.GetProperty("level").GetInt32(),position=row.GetProperty("position").GetInt32();
                string stack=row.GetProperty("stack").GetString()!,name=row.GetProperty("retail_name").GetString()!;
                string sha=row.GetProperty("sha256").GetString()!,file=row.GetProperty("file").GetString()!;
                if(id<=0||level<=0||position is <1 or >8||string.IsNullOrWhiteSpace(stack)
                    ||string.IsNullOrWhiteSpace(name)||file!=$"skill-{id}.utf16le.xml")
                    throw new InvalidDataException("Invalid player resistance binding");
                var source=RetailNpcStatBuffs.ParsePlayerResistance(File.ReadAllBytes(Path.Combine(directory,file)),sha);
                if(source.Id!=id||source.Name!=name||!entries.TryAdd(id,new(id,level,stack,position,source)))
                    throw new InvalidDataException("Ambiguous player resistance binding");
            }
            if(entries.Count==0)throw new InvalidDataException("Empty player resistance catalog");
            return new(entries);
        }
    }

    internal static bool TryCaptureForOfflineFixture(Player target,Effect effect,Catalog catalog,
        out RetailNpcStatSpellDrain.CurseModifiers modifier)
        => TryCapture(target,catalog,effect,out modifier);

    internal static bool TryCaptureGeneralForOfflineFixture(Player target,Catalog catalog,
        out RetailNpcStatSpellDrain.CurseModifiers modifier)
        => TryCapture(target,catalog,null,out modifier);

    private static bool TryCapture(Player target,Catalog catalog,Effect? required,
        out RetailNpcStatSpellDrain.CurseModifiers modifier)
    {
        modifier=default;
        if(!target.IsSpawned()||target.IsDead())return false;
        var stats=target.GetGameStats();
        if(!stats.TryCaptureRetailStatMutation(out long stamp))return false;
        var functions=stats.GetStatsSorted(StatEnum.ABNORMAL_RESISTANCE_ALL);
        if(required is not null && functions.Count!=1)return false;
        var active=new HashSet<Effect>(target.GetEffectController().GetAllEffects(),ReferenceEqualityComparer.Instance);
        var owners=new HashSet<Effect>(ReferenceEqualityComparer.Instance);
        int percent=0,flat=0;
        foreach(var function in functions)
        {
            if(function is not StatFunctionProxy proxy ||proxy.GetOwner() is not Effect effect
                ||!owners.Add(effect)||(required is not null&&!ReferenceEquals(effect,required)))return false;
            if(!TryReadModifier(target,effect,proxy,catalog,active,out var part)
                && !TryReadLinked(target,effect,proxy,catalog,active,out part))return false;
            percent=unchecked(percent+part.Percent);flat=unchecked(flat+part.Flat);
        }
        // Inspect both sides of ownership. A catalogued active success may
        // have lost its stat function (or never installed it); iterating only
        // the current functions would silently turn that source into zero.
        foreach(var effect in active)
        {
            if(catalog.TryGet(effect.GetSkillId(),out var binding)
                && effect.IsInSuccessEffects(binding.Position) && !owners.Contains(effect))return false;
            if(catalog.TryGetLinkedChild(effect.GetSkillId(),out _) && effect.IsInSuccessEffects(4) && !owners.Contains(effect))return false;
            if(catalog.LinkedParent(effect.GetSkillId()) is { } parent && effect.IsInSuccessEffects(2)
                && !active.Any(child=>child.GetSkillId()==parent.ChildId && child.IsInSuccessEffects(4) && owners.Contains(child)))return false;
            if(catalog.TryGetRetailOnly(effect.GetSkillId(),out var extra) && effect.IsInSuccessEffects(2))
            {
                if(required is not null&&!ReferenceEquals(required,effect)
                    ||!TryReadRetailOnly(target,effect,extra,out int value))return false;
                flat=unchecked(flat+value);
            }
        }
        // This is still a point-in-time witness. Mutation of function fields or
        // effect membership after the check requires a separate commit owner.
        if(!stats.RetailStatMutationUnchanged(stamp)
            ||!active.SetEquals(target.GetEffectController().GetAllEffects()))return false;
        modifier=new(percent,flat);return true;
    }

    private static bool TryReadLinked(Player target,Effect child,StatFunctionProxy proxy,Catalog catalog,
        HashSet<Effect> active,out RetailNpcStatSpellDrain.CurseModifiers modifier)
    {
        modifier=default;
        if(!catalog.TryGetLinkedChild(child.GetSkillId(),out var b)||child.RetailHasEnded||!active.Contains(child)
            ||!ReferenceEquals(child.GetEffector(),target)||!ReferenceEquals(child.GetEffected(),target)
            ||child.GetSkillLevel()!=1||child.GetStack()!=b.ChildStack
            ||!ReferenceEquals(child.GetSkillTemplate(),DataManager.SKILL_DATA.GetSkillTemplate(b.ChildId))
            ||child.GetSkillTemplate().GetEffectTemplate(4) is not StatupEffect stat
            ||!child.IsInSuccessEffects(4)||!ReferenceEquals(child.EffectInPos(4),stat)
            ||stat.Effectid!=185524||stat.BasicLvl!=b.BasicLevel||stat.Duration1!=0||stat.Duration2!=600000
            ||!stat.NoResist||stat.PreEffects is not [1]||stat.EffectConditions is not null||stat.EffectSubConditions is not null)return false;
        var host=stat.change?.Where(c=>c.Stat==StatEnum.ABNORMAL_RESISTANCE_ALL).ToArray();
        if(host is not {Length:1}||host[0].Func!=ChangeFunc.ADD||host[0].Value!=b.Value||host[0].Delta!=0||host[0].Conditions is not null
            ||proxy.GetProxiedFunction().GetType()!=typeof(StatAddFunction)||proxy.HasConditions()||!proxy.IsBonus()||proxy.GetValue()!=b.Value)return false;
        var matching=active.Where(e=>e.GetSkillId()==b.Id).ToArray();
        if(matching is not {Length:1})return false;
        var parent=matching[0];
        if(parent.RetailHasEnded||!ReferenceEquals(parent.GetEffector(),target)||!ReferenceEquals(parent.GetEffected(),target)
            ||parent.GetSkillLevel()!=b.Level||parent.GetStack()!=b.Stack
            ||!ReferenceEquals(parent.GetSkillTemplate(),DataManager.SKILL_DATA.GetSkillTemplate(b.Id))
            ||parent.GetSkillTemplate().GetEffectTemplate(1) is not SkillLauncherEffect launch||launch.skillId!=b.ChildId
            ||!parent.IsInSuccessEffects(1)||!ReferenceEquals(parent.EffectInPos(1),launch)
            ||parent.GetSkillTemplate().GetEffectTemplate(2) is not ShapeChangeEffect shape||!parent.IsInSuccessEffects(2)
            ||!ReferenceEquals(parent.EffectInPos(2),shape)||shape.model!=b.Model||shape.Effectid!=175||shape.BasicLvl!=b.BasicLevel
            ||shape.Duration1!=0||shape.Duration2!=600000||!shape.NoResist||shape.PreEffects is not [1]
            ||shape.EffectConditions is not null||shape.EffectSubConditions is not null)return false;
        modifier=new(0,b.Value);return true;
    }

    private static bool TryReadRetailOnly(Player target,Effect effect,RetailOnlyBinding binding,out int value)
    {
        value=0;
        if(effect.RetailHasEnded || !ReferenceEquals(effect.GetEffector(),target)||!ReferenceEquals(effect.GetEffected(),target)
            ||effect.GetSkillLevel()!=binding.Level||effect.GetStack()!=binding.Stack
            ||!ReferenceEquals(effect.GetSkillTemplate(),DataManager.SKILL_DATA.GetSkillTemplate(binding.Id))
            ||effect.GetSkillTemplate().GetEffectTemplate(2) is not StatupEffect template
            ||!ReferenceEquals(effect.EffectInPos(2),template)||template.Effectid!=988672
            ||template.BasicLvl!=binding.BasicLevel||template.Duration1!=0||template.Duration2!=4800000
            ||!template.NoResist||template.PreEffects is not [1]||template.EffectConditions is not null
            ||template.EffectSubConditions is not null)return false;
        var change=template.change?.Where(c=>c.Stat==StatEnum.PHYSICAL_ATTACK).ToArray();
        if(change is not {Length:1}||change[0].Func!=ChangeFunc.PERCENT||change[0].Value!=binding.HostPercent
            ||change[0].Delta!=0||change[0].Conditions is not null
            ||template.change!.Any(c=>c.Stat==StatEnum.ABNORMAL_RESISTANCE_ALL))return false;
        var owned=target.GetGameStats().GetStatsSorted(StatEnum.PHYSICAL_ATTACK)
            .OfType<StatFunctionProxy>().Where(p=>ReferenceEquals(p.GetOwner(),effect)).ToArray();
        if(owned is not {Length:1}||owned[0].GetProxiedFunction().GetType()!=typeof(StatRateFunction)
            ||owned[0].HasConditions()||!owned[0].IsBonus()||owned[0].GetValue()!=binding.HostPercent)return false;
        value=binding.Source.Programs.Single(p=>p.Position==2).Parts.Single(p=>p.Stat==StatEnum.ABNORMAL_RESISTANCE_ALL).Value;
        return true;
    }

    private static bool TryReadModifier(Player target,Effect effect,StatFunctionProxy proxy,Catalog catalog,
        HashSet<Effect> active,out RetailNpcStatSpellDrain.CurseModifiers modifier)
    {
        modifier=default;
        if(!catalog.TryGet(effect.GetSkillId(),out var binding)||effect.RetailHasEnded||!active.Contains(effect)
            ||!ReferenceEquals(effect.GetEffector(),target)||!ReferenceEquals(effect.GetEffected(),target)
            ||effect.GetSkillLevel()!=binding.Level||effect.GetStack()!=binding.Stack
            ||!ReferenceEquals(effect.GetSkillTemplate(),DataManager.SKILL_DATA.GetSkillTemplate(binding.Id)))return false;
        var program=binding.Source.Programs.SingleOrDefault(p=>p.Position==binding.Position);
        if(program is null||!program.Supported)return false;
        var parts=program.Parts.Where(p=>p.Stat==StatEnum.ABNORMAL_RESISTANCE_ALL).ToArray();
        if(parts.Length!=1)return false;
        int value=RetailNpcStatBuffs.Value(parts[0],effect.GetSkillLevel());
        if(effect.GetSkillTemplate().GetEffectTemplate(binding.Position) is not StatupEffect template
            ||!effect.IsInSuccessEffects(binding.Position)||!ReferenceEquals(effect.EffectInPos(binding.Position),template))return false;
        var host=template.change?.Where(c=>c.Stat==StatEnum.ABNORMAL_RESISTANCE_ALL).ToArray();
        var expectedFunc=parts[0].Percent?ChangeFunc.PERCENT:ChangeFunc.ADD;
        if(host is not {Length:1}||host[0].Func!=expectedFunc||host[0].Conditions is not null
            ||unchecked(host[0].Value+host[0].Delta*effect.GetSkillLevel())!=value)return false;
        var expectedType=parts[0].Percent?typeof(StatRateFunction):typeof(StatAddFunction);
        if(!ReferenceEquals(proxy.GetOwner(),effect)||proxy.GetProxiedFunction().GetType()!=expectedType
            ||proxy.HasConditions()||!proxy.IsBonus()||proxy.GetValue()!=value)return false;
        modifier=parts[0].Percent?new(value,0):new(0,value);return true;
    }
}
