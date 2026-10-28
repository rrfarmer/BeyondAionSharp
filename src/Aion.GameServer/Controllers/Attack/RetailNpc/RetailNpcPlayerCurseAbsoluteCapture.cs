using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Offline point-in-time witness for selected player absolute-status records.
/// This does not establish atomic effect ownership or authorize gameplay.</summary>
internal static class RetailNpcPlayerCurseAbsoluteCapture
{
    internal sealed record Binding(int Id,int Level,string Stack,int Position,bool Debuff,int SetId,ushort? Curse);
    internal sealed class Catalog
    {
        private readonly Dictionary<(int,int),Binding> entries;
        private Catalog(Dictionary<(int,int),Binding> entries)=>this.entries=entries;
        private static XElement ReadRecord(string directory,string file,string expectedSha,string expectedFile,string rootName)
        {
            if(file!=expectedFile)throw new InvalidDataException("Absolute source path differs");
            byte[] raw=File.ReadAllBytes(Path.Combine(directory,file));
            if(raw.Length==0||raw.Length>131072||raw.Length%2!=0||Convert.ToHexStringLower(SHA256.HashData(raw))!=expectedSha)
                throw new InvalidDataException("Absolute source hash differs");
            using var reader=XmlReader.Create(new StringReader(new UnicodeEncoding(false,false,true).GetString(raw)),
                new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536});
            var root=XDocument.Load(reader).Root;
            if(root is null||root.Name!=rootName||root.HasAttributes)throw new InvalidDataException("Absolute source root differs");
            return root;
        }
        private static string? Leaf(XElement root,string name)
        {
            var nodes=root.Elements(name).ToArray();
            if(nodes.Length==0)return null;
            if(nodes.Length!=1||nodes[0].HasElements||nodes[0].HasAttributes)throw new InvalidDataException("Ambiguous absolute source field");
            return nodes[0].Value;
        }
        internal static Catalog LoadForOfflineFixture(ReadOnlySpan<byte> bytes,string expectedSha,string directory)
        {
            if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=expectedSha)throw new InvalidDataException("Absolute catalog hash differs");
            using var doc=JsonDocument.Parse(bytes.ToArray());var root=doc.RootElement;
            if(root.GetProperty("version").GetInt32()!=1||root.GetProperty("purpose").GetString()!="offline-player-absolute-curse-v1")
                throw new InvalidDataException("Absolute catalog identity differs");
            var entries=new Dictionary<(int,int),Binding>();
            foreach(var row in root.GetProperty("records").EnumerateArray())
            {
                int id=row.GetProperty("id").GetInt32(),level=row.GetProperty("level").GetInt32();
                int position=row.GetProperty("position").GetInt32(),setId=row.GetProperty("set_id").GetInt32();
                string stack=row.GetProperty("stack").GetString()!,kind=row.GetProperty("kind").GetString()!;
                string skillName=row.GetProperty("skill_name").GetString()!,absoluteName=row.GetProperty("absolute_name").GetString()!;
                var rawCurse=row.GetProperty("curse");ushort? curse=rawCurse.ValueKind==JsonValueKind.Null?null:rawCurse.GetUInt16();
                if(id<=0||level<=0||position is <1 or >8||setId is <1 or >100||string.IsNullOrWhiteSpace(stack)
                    ||kind is not ("absstatbuff" or "absstatdebuff"))throw new InvalidDataException("Invalid absolute binding");
                var skill=ReadRecord(directory,row.GetProperty("skill_file").GetString()!,row.GetProperty("skill_sha256").GetString()!,
                    $"skill-{id}.utf16le.xml","skill_base");
                var absolute=ReadRecord(directory,row.GetProperty("absolute_file").GetString()!,row.GetProperty("absolute_sha256").GetString()!,
                    $"absolute-{setId}.utf16le.xml","absolute_stat_to_pc");
                string expectedType=kind=="absstatdebuff"?"AbsoluteStatToPCDebuff":"AbsoluteStatToPCBuff";
                string? sourceCurse=Leaf(absolute,"ar_curse");
                if(Leaf(skill,"id")!=id.ToString()||Leaf(skill,"name")!=skillName
                    ||!string.Equals(Leaf(skill,$"effect{position}_type"),expectedType,StringComparison.OrdinalIgnoreCase)
                    ||!string.Equals(Leaf(skill,$"effect{position}_reserved1"),absoluteName,StringComparison.OrdinalIgnoreCase)
                    ||Leaf(absolute,"id")!=setId.ToString()||Leaf(absolute,"name")!=absoluteName
                    ||(curse is null?sourceCurse is not null:sourceCurse!=curse.Value.ToString())
                    ||!entries.TryAdd((id,position),new(id,level,stack,position,kind=="absstatdebuff",setId,curse)))
                    throw new InvalidDataException("Absolute source binding differs");
            }
            if(entries.Count==0)throw new InvalidDataException("Empty absolute catalog");
            return new(entries);
        }
        internal bool TryGet(int id,int position,out Binding binding)=>entries.TryGetValue((id,position),out binding!);
    }

    internal sealed record Observed(Effect Owner,AbstractAbsoluteStatEffect Template,int Level,string Stack,int SetId,
        IStatFunction? Function,IStatFunction? Leaf,int? Value);

    internal sealed class Receipt(Player target,CreatureGameStats stats,long stamp,HashSet<Effect> active,
        Observed[] observed,RetailNpcStatSpellDrain.CurseAbsolute buff,RetailNpcStatSpellDrain.CurseAbsolute debuff)
    {
        internal RetailNpcStatSpellDrain.CurseAbsolute Buff=>buff;
        internal RetailNpcStatSpellDrain.CurseAbsolute Debuff=>debuff;
        internal bool Covers(IStatFunction function)=>observed.Any(x=>ReferenceEquals(x.Function,function));
        internal bool IsCurrent(Player current)
        {
            if(!ReferenceEquals(current,target)||!current.IsSpawned()||current.IsDead()
                ||!ReferenceEquals(current.GetGameStats(),stats)||!stats.RetailStatMutationUnchanged(stamp)
                ||!active.SetEquals(current.GetEffectController().GetAllEffects()))return false;
            var functions=stats.GetStatsSorted(StatEnum.CURSE_RESISTANCE);
            foreach(var entry in observed)
            {
                if(entry.Owner.RetailHasEnded||entry.Owner.GetSkillLevel()!=entry.Level||entry.Owner.GetStack()!=entry.Stack
                    ||entry.Template.statSetId!=entry.SetId
                    ||!ReferenceEquals(entry.Owner.GetEffected(),current)||!ReferenceEquals(entry.Owner.GetEffector(),current)
                    ||!ReferenceEquals(entry.Owner.EffectInPos(entry.Template.Position),entry.Template))return false;
                if(entry.Function is not null && (!functions.Any(f=>ReferenceEquals(f,entry.Function))
                    ||entry.Function is not StatFunctionProxy proxy||!ReferenceEquals(proxy.GetOwner(),entry.Owner)
                    ||!ReferenceEquals(proxy.GetProxiedFunction(),entry.Leaf)||proxy.GetValue()!=entry.Value
                    ||proxy.HasConditions()||proxy.IsBonus()))return false;
            }
            return stats.RetailStatMutationUnchanged(stamp);
        }
    }

    internal static bool TryCaptureForOfflineFixture(Player target,Catalog catalog,
        out RetailNpcStatSpellDrain.CurseAbsolute buff,out RetailNpcStatSpellDrain.CurseAbsolute debuff)
    {
        buff=default;debuff=default;
        if(!TryCaptureReceiptForOfflineFixture(target,catalog,out var receipt))return false;
        buff=receipt!.Buff;debuff=receipt.Debuff;return true;
    }

    internal static bool TryCaptureReceiptForOfflineFixture(Player target,Catalog catalog,out Receipt? receipt)
    {
        receipt=null;RetailNpcStatSpellDrain.CurseAbsolute buff=default,debuff=default;
        if(!target.IsSpawned()||target.IsDead())return false;
        var stats=target.GetGameStats();if(!stats.TryCaptureRetailStatMutation(out long stamp))return false;
        var active=new HashSet<Effect>(target.GetEffectController().GetAllEffects(),ReferenceEqualityComparer.Instance);
        var functions=stats.GetStatsSorted(StatEnum.CURSE_RESISTANCE);var owners=new HashSet<Effect>(ReferenceEqualityComparer.Instance);
        var observed=new List<Observed>();
        foreach(var effect in active)
        foreach(var selected in effect.GetSuccessEffects().OfType<AbstractAbsoluteStatEffect>())
        {
            bool isDebuff=selected.GetType()==typeof(AbsoluteStatToPCDebuffEffect);
            if(!isDebuff && selected.GetType()!=typeof(AbsoluteStatToPCBuffEffect))return false;
            if(effect.RetailHasEnded||!ReferenceEquals(effect.GetEffector(),target)||!ReferenceEquals(effect.GetEffected(),target)
                ||!owners.Add(effect)||!catalog.TryGet(effect.GetSkillId(),selected.Position,out var binding)
                ||binding.Debuff!=isDebuff||effect.GetSkillLevel()!=binding.Level
                ||effect.GetStack()!=binding.Stack||selected.statSetId!=binding.SetId
                ||!ReferenceEquals(effect.GetSkillTemplate(),DataManager.SKILL_DATA.GetSkillTemplate(binding.Id))
                ||!ReferenceEquals(effect.GetSkillTemplate().GetEffectTemplate(binding.Position),selected))return false;
            var host=selected.GetModifiersSet()?.GetModifiers()?.Where(f=>f.GetName()==StatEnum.CURSE_RESISTANCE).ToArray();
            if(host is null||host.Length!=(binding.Curse is null?0:1))return false;
            if(binding.Curse is { } value && (host[0].GetType()!=typeof(StatAbsFunction)||host[0].IsBonus()
                ||host[0].HasConditions()||host[0].GetValue()!=value))return false;
            var actual=functions.Where(f=>ReferenceEquals(f.GetOwner(),effect)).ToArray();
            if(actual.Length!=host.Length)return false;
            if(actual.Length==1 && (actual[0] is not StatFunctionProxy proxy
                ||!ReferenceEquals(proxy.GetProxiedFunction(),host[0])||proxy.GetValue()!=binding.Curse
                ||proxy.HasConditions()||proxy.IsBonus()))return false;
            observed.Add(new(effect,selected,effect.GetSkillLevel(),effect.GetStack(),binding.SetId,
                actual.Length==0?null:actual[0],actual.Length==0?null:host[0],binding.Curse));
            var result=new RetailNpcStatSpellDrain.CurseAbsolute((ushort)binding.SetId,binding.Curse.HasValue,binding.Curse.GetValueOrDefault());
            if(isDebuff){if(debuff.Id!=0)return false;debuff=result;}
            else {if(buff.Id!=0)return false;buff=result;}
        }
        foreach(var f in functions)
        {
            var leaf=f is StatFunctionProxy proxy?proxy.GetProxiedFunction():f;
            if(leaf is StatAbsFunction && (f.GetOwner() is not Effect effect||!owners.Contains(effect)))return false;
        }
        var candidate=new Receipt(target,stats,stamp,active,observed.ToArray(),buff,debuff);
        if(!candidate.IsCurrent(target))return false;
        receipt=candidate;return true;
    }
}
