using System.Collections.Frozen;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Numeric source lineage, not skill admission or a trigger scheduler.
/// Native Provoker R17 names a child outside the NPC's declared skill list.
/// Only level-independent StatUp recipes can use this additional binding.</summary>
internal sealed class RetailNpcStatTriggeredBuffs
{
    internal sealed record Reference(int Position,string ChildName,string Receiver);
    internal sealed record Definition(int Id,string Name,string? Group,Reference[] References);
    internal sealed record Link(int ParentId,int Position,int ChildId,string Receiver);
    private readonly FrozenDictionary<int,Definition> definitions;
    private readonly FrozenDictionary<int,Link[]> links;

    internal RetailNpcStatTriggeredBuffs(IEnumerable<Definition> sources)
    {
        definitions=sources.ToFrozenDictionary(x=>x.Id);
        var names=definitions.Values.GroupBy(x=>RetailNpcStatCatalog.NativeSkillNameKey(x.Name))
            .Where(g=>g.Count()==1).ToDictionary(g=>g.Key,g=>g.Single().Id);
        links=definitions.Values.ToFrozenDictionary(x=>x.Id,x=>x.References
            .Where(p=>p.Receiver is "Me" or "Opponent" && names.ContainsKey(RetailNpcStatCatalog.NativeSkillNameKey(p.ChildName)))
            .Select(p=>new Link(x.Id,p.Position,names[RetailNpcStatCatalog.NativeSkillNameKey(p.ChildName)],p.Receiver)).ToArray());
    }

    internal static Definition Parse(ReadOnlySpan<byte> raw,string sha)
    {
        var identity=RetailNpcSkillRecord.Read(raw,sha);
        var root = identity.Root;
        string? Leaf(string tag)
        {
            var rows=root.Elements(tag).ToArray();
            if(rows.Length==0)return null;
            if(rows.Length!=1||rows[0].HasElements||rows[0].HasAttributes)
                throw new InvalidDataException("Ambiguous triggered-buff source field: "+tag);
            return rows[0].Value;
        }
        var references=new List<Reference>();
        for(int slot=1;slot<=8;slot++)
        {
            if(!string.Equals(Leaf($"effect{slot}_type"),"Provoker",StringComparison.OrdinalIgnoreCase))continue;
            if(Leaf($"effect{slot}_reserved17") is not {Length:>0} child||child=="0")continue;
            references.Add(new(slot,child,Leaf($"effect{slot}_reserved14")??""));
        }
        return new(identity.Id,identity.Name,Leaf("skill_group_name"),references.ToArray());
    }

    internal static bool IsConstant(RetailNpcStatBuffs.Source source)=>!source.Programs.IsEmpty
        &&source.Programs.All(p=>p.Supported&&!p.Parts.IsEmpty&&p.Parts.All(x=>x.Delta==0));

    internal bool MatchesStack(int child,string? stack)=>definitions.TryGetValue(child,out var definition)
        &&(RetailNpcStatCatalog.SameNativeSkillName(definition.Name,stack)
            ||RetailNpcStatCatalog.SameNativeSkillName(definition.Group,stack));

    internal static bool MatchesHost(Link link,EffectTemplate? template)=>template is ProvokerEffect effect
        &&effect.GetPosition()==link.Position&&effect.skillId==link.ChildId
        &&(link.Receiver=="Me"&&effect.provokeTarget==ProvokeTarget.ME
            ||link.Receiver=="Opponent"&&effect.provokeTarget==ProvokeTarget.OPPONENT);

    internal bool TryResolve(int child,IEnumerable<int> declaredRoots,out Link[] path,Func<Link,bool>? host=null)
    {
        path=[];var visited=new HashSet<int>();var queue=new Queue<(int Id,Link[] Path)>();
        foreach(int root in declaredRoots.Distinct())queue.Enqueue((root,[]));
        while(queue.TryDequeue(out var current))
        {
            if(current.Path.Length>0&&current.Id==child){path=current.Path;return true;}
            if(!visited.Add(current.Id))continue;
            if(visited.Count>4096)throw new InvalidDataException("Triggered-buff source graph exceeds bound");
            if(!links.TryGetValue(current.Id,out var next))continue;
            foreach(var link in next)
                if(host is null||host(link))queue.Enqueue((link.ChildId,[..current.Path,link]));
        }
        return false;
    }
}
