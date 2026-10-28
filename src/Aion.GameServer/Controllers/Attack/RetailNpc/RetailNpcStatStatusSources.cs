using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Aion.GameServer.Dataholders.RetailNpc;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Source-bound numeric metadata for successful existing status owners.
/// Does not select/apply a skill, replace its timers, or alter player/PvP observers.</summary>
internal sealed class RetailNpcStatStatusSources
{
    internal enum StatusKind { Dodge, Block, Parry, Blind }
    internal sealed record Part(int Position,StatusKind Kind,int Rate,int Constant,int Strength,string? UnsupportedReason,
        string SourceSha256="")
    {
        internal bool Supported=>UnsupportedReason is null;
        internal int BlindChance(int level)
        {
            if(Kind!=StatusKind.Blind||!Supported||level is <0 or >short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(level));
            // 140b898a0: signed16 factors, unsigned16 assignment, then cap100.
            ushort chance=unchecked((ushort)((short)level*(short)Rate+(short)Constant));
            return Math.Min(100,(int)chance);
        }
    }
    internal sealed record Definition(int Id,string Name,string Sha256,IReadOnlyList<Part> Parts,IReadOnlyList<string> HostStacks);
    private readonly FrozenDictionary<int,Definition> definitions;
    private RetailNpcStatStatusSources(Dictionary<int,Definition> source)=>definitions=source.ToFrozenDictionary();
    internal IReadOnlyCollection<Definition> Definitions=>definitions.Values;

    internal bool TryGet(Effect effect,EffectTemplate template,StatusKind kind,out Part part,out string reason)
    {
        part=null!;reason="missing-retail-status-definition";
        if(!definitions.TryGetValue(effect.GetSkillId(),out var definition))return false;
        if(!definition.HostStacks.Contains(effect.GetSkillTemplate().GetStack(),StringComparer.OrdinalIgnoreCase))
        {reason="retail-status-host-name-drift";return false;}
        var selected=definition.Parts.SingleOrDefault(p=>p.Position==template.GetPosition()&&p.Kind==kind);
        if(selected is null){reason="retail-status-host-slot-or-kind-drift";return false;}
        if(!selected.Supported){reason=selected.UnsupportedReason!;return false;}
        if(kind!=StatusKind.Blind&&(selected.Rate!=0||selected.Constant!=template.Value))
        {reason="retail-status-charge-owner-drift";return false;}
        part=selected;reason="";return true;
    }

    internal static RetailNpcStatStatusSources Parse(byte[] bytes,string hash,string directory,
        Func<JsonElement,string,byte[]> readAsset)
    {
        RetailNpcStatCatalog.CheckHash(bytes,hash,"retail status sources");
        using var doc=JsonDocument.Parse(bytes);var root=doc.RootElement;RetailNpcDatasetValidation.UniqueJson(root);
        if(root.GetProperty("format").GetString()!="retail-status-sources-v1"
            ||root.GetProperty("region").GetString()!="USA"
            ||root.GetProperty("main_sha256").GetString()!=RetailNpcNormalScope.MainBinarySha256)
            throw new InvalidDataException("Retail status source context differs");
        var definitions=new Dictionary<int,Definition>();
        foreach(var asset in root.GetProperty("definitions").EnumerateArray())
        {
            var definition=ParseDefinition(readAsset(asset,directory),asset.GetProperty("sha256").GetString()!);
            if(definition.Id!=asset.GetProperty("id").GetInt32()
                ||definition.Name!=asset.GetProperty("name").GetString()
                ||!definitions.TryAdd(definition.Id,definition))
                throw new InvalidDataException("Retail status identity duplicated/differs");
        }
        if(definitions.Count is <1 or >4096)throw new InvalidDataException("Retail status source count outside bounds");
        return new(definitions);
    }

    internal static Definition ParseDefinition(ReadOnlySpan<byte> bytes,string hash)
    {
        // Reuse complete-record hash, strict UTF16, identity, depth and DTD checks.
        var identity=RetailNpcStatSkillCombat.ParseSource(bytes,hash);
        var root=XElement.Parse(Encoding.Unicode.GetString(bytes));var parts=new List<Part>();
        string? Field(string name)
        {
            var values=root.Elements(name).ToArray();
            if(values.Length==0)return null;
            if(values.Length!=1||values[0].HasElements||values[0].HasAttributes)
                throw new InvalidDataException("Repeated/non-scalar retail status field: "+name);
            return values[0].Value;
        }
        int Number(string name)
        {
            string? text=Field(name);if(text is null)return 0;
            if(!int.TryParse(text,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int value))
                throw new InvalidDataException("Noncanonical retail status number: "+name);
            return value;
        }
        for(int position=1;position<=8;position++)
        {
            string prefix="effect"+position+"_";
            StatusKind? kind=Field(prefix+"type") switch
            {"AlwaysDodge"=>StatusKind.Dodge,"AlwaysBlock"=>StatusKind.Block,
                "AlwaysParry"=>StatusKind.Parry,"Blind"=>StatusKind.Blind,_=>null};
            if(kind is null)continue;
            int rate=Number(prefix+"reserved"+(kind==StatusKind.Blind?1:8));
            int constant=Number(prefix+"reserved"+(kind==StatusKind.Blind?2:9));
            int strength=kind==StatusKind.Blind?0:Math.Max(1,Number(prefix+"reserved13"));
            string? reason=kind==StatusKind.Blind
                ?rate<0||unchecked(rate+constant)<0?"invalid-native-blind-parameter-pair":null
                :rate!=0?"unverified-level-dependent-defense-charge-owner":null;
            parts.Add(new(position,kind.Value,rate,constant,strength,reason,identity.Sha256));
        }
        if(parts.Count==0)throw new InvalidDataException("No supported numeric status owner in selected source");
        // Player skill grades share a host stack (for example _G1/_G2),
        // while NPC skills often retain the individual native name. Keep the
        // stable ID, selected slot/kind and charge checks in addition to this join.
        string gradeStack=string.Join("_",identity.Name.Split('_').Where(p=>
            !(p.Length>1&&p[0]=='G'&&p.AsSpan(1).ToArray().All(char.IsAsciiDigit))));
        var stacks=new[]{identity.Name,Field("skill_group_name"),gradeStack}
            .Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new(identity.Id,identity.Name,identity.Sha256,parts.AsReadOnly(),Array.AsReadOnly(stacks));
    }
}
