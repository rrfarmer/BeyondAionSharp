using System.Collections.Frozen;
using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.RetailNpc;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Retail;

internal static partial class RetailNpcStatCatalog
{
    private sealed partial record Package
    {
        internal FrozenDictionary<int, RetailNpcStatBuffs.Source> Buffs { get; init; } = FrozenDictionary<int, RetailNpcStatBuffs.Source>.Empty;
        internal FrozenDictionary<int, string> BuffRootNames { get; init; } = FrozenDictionary<int, string>.Empty;
        internal RetailNpcStatTriggeredBuffs TriggeredBuffs { get; init; } = new([]);
    }

    internal static IReadOnlyCollection<RetailNpcStatBuffs.Source> BuffSources =>
        Volatile.Read(ref active) is { } package ? package.Buffs.Values : Array.Empty<RetailNpcStatBuffs.Source>();

    static partial void ReadBuffPackage(JsonElement root, string directory, ref Package package)
    {
        var names = new Dictionary<int, string>();
        var buffs = new Dictionary<int, RetailNpcStatBuffs.Source>();
        var definitions = new List<RetailNpcStatTriggeredBuffs.Definition>();
        if (root.TryGetProperty("skill_sources", out var sources))
        foreach (var asset in sources.EnumerateArray())
        {
            byte[] bytes = ReadAsset(asset, directory);
            string pin = asset.GetProperty("sha256").GetString()!;
            var source = RetailNpcStatBuffs.Parse(bytes, pin);
            if (source.Id != asset.GetProperty("id").GetInt32() || !names.TryAdd(source.Id, source.Name))
                throw new InvalidDataException("Statistics buff source identity/duplicate differs");
            if (!source.Programs.IsEmpty) buffs.Add(source.Id, source);
            definitions.Add(RetailNpcStatTriggeredBuffs.Parse(bytes, pin));
        }
        package = package with { Buffs = buffs.ToFrozenDictionary(), BuffRootNames = names.ToFrozenDictionary(),
            TriggeredBuffs = new(definitions) };
    }

    private static bool HasDeclaredBuffRoot(Package package, SkyTempleNpcProfile profile, int id, int level) =>
        package.BuffRootNames.TryGetValue(id, out var name) && MatchesDeclaredSkillBinding(profile, id, level, id, name);

    static partial void RetireAdditionalModifiers(Npc npc) => RetailNpcStatBuffs.RestoreHostForRetiredTarget(npc);

    // Only a numeric-level context: the existing skill entry, duration and other
    // effect positions keep their actual host level. Require a unique source
    // level and the exact existing host NPC list binding before correcting it.
    internal static bool TryGetBuffNumericLevel(Npc caster, SkyTempleNpcProfile profile, int id, int hostLevel, out int level)
    {
        level = hostLevel;
        if (Volatile.Read(ref active) is not { } package) return false;
        var levels = DeclaredSkills(profile).Where(s => s.Id == id).Select(s => s.Level).Distinct().ToArray();
        if (levels.Length != 1 || levels[0] == hostLevel || !HasDeclaredBuffRoot(package, profile, id, levels[0])) return false;
        var host = DataManager.NPC_SKILL_DATA.GetOrCreateNpcSkillList(caster.GetNpcId());
        bool bound = Enumerable.Range(0, host.Size()).Select(host.GetSkillOnPosition)
            .Any(s => s.GetSkillId() == id && s.GetSkillLevel() == hostLevel);
        if (!bound) return false;
        level = levels[0]; return true;
    }

    // Correct the numeric program of an existing 4.8 cast without claiming
    // retail AI admission. The source program must be constant at every legal
    // level, entirely supported, and joined to this exact host entry/stack.
    // Do not add skills, infer a missing source level, or authorize scripted
    // injections merely because the skill exists in the global source table.
    internal static bool TryGetRetainedHostBuff(Npc caster, SkyTempleNpcProfile profile, int id, int hostLevel,
        out RetailNpcStatBuffs.Source source)
    {
        source = null!;
        if (hostLevel is < 0 or > short.MaxValue || !profile.StatisticsOnly
            || !TryGet(caster, out var current) || !ReferenceEquals(current, profile)
            || Volatile.Read(ref active) is not { } package || !package.Buffs.TryGetValue(id, out var candidate)
            || !RetailNpcStatTriggeredBuffs.IsConstant(candidate)) return false;
        var template = DataManager.SKILL_DATA.GetSkillTemplate(id);
        if (template is null || template.GetSkillId() != candidate.Id
            || !package.TriggeredBuffs.MatchesStack(id, template.GetStack())) return false;
        var host = DataManager.NPC_SKILL_DATA.GetOrCreateNpcSkillList(caster.GetNpcId());
        if (!Enumerable.Range(0, host.Size()).Select(host.GetSkillOnPosition)
            .Any(s => s.GetSkillId() == id && s.GetSkillLevel() == hostLevel)) return false;
        source = candidate;
        return true;
    }

    internal static bool MatchesDeclaredSkillBinding(SkyTempleNpcProfile profile, int id, int level, int sourceId, string sourceName)

    {
        if(!profile.StatisticsOnly ||sourceId!=id)return false;
        var groups = profile.Source.Source.GetRawRootChildren("skills");
        if (groups.Length != 1) return false;
        var rows = RetailNpcSourceProfile.RawChildren(groups[0]).Where(x => x[0].GetString() == "data").ToArray();
        foreach (var binding in profile.Source.Source.SkillBindings.EnumerateArray())
        {
            if (!binding.TryGetProperty("supported48_id", out var value) || value.ValueKind != JsonValueKind.Number || value.GetInt32() != id
                || binding.GetProperty("status").GetString() != "dual_join_agrees_static_only"
                || binding.GetProperty("source_group_index").GetInt32() != 0) continue;
            int index = binding.GetProperty("source_entry_index").GetInt32();
            if (index < 0 || index >= rows.Length) return false;
            string name = RetailNpcSourceProfile.RawChildren(rows[index]).Where(x => x[0].GetString() == "skill_name")
                .Select(RetailNpcSourceProfile.RawText).Single();
            if (!SameNativeSkillName(name, sourceName) || name != binding.GetProperty("name").GetString()
                || SkyTempleNpcProfile.LeafInt(rows[index], "skill_level") != level) continue;
            return true;
        }
        return false;
    }

    // Main NPC parser140dae920 -> registry140d9edb0 -> map140d9e780;
    // actual lookup/insert compare via _wcsicmp. All selected skill names are
    // ASCII. Preserve exact non-ASCII spelling rather than assuming a locale.
    internal static string NativeSkillNameKey(string name) =>
        name.All(c => c <= 0x7f) ? name.ToLowerInvariant() : name;

    internal static bool SameNativeSkillName(string? left, string? right) =>
        left is not null && right is not null && NativeSkillNameKey(left) == NativeSkillNameKey(right);

    internal static bool TryGetBuff(SkyTempleNpcProfile profile,int id,int level,out RetailNpcStatBuffs.Source source)
        =>TryGetBuff(profile,id,level,out source,out _);

    internal static bool TryGetBuff(SkyTempleNpcProfile profile,int id,int level,out RetailNpcStatBuffs.Source source,
        out RetailNpcStatTriggeredBuffs.Link[] lineage,Func<RetailNpcStatTriggeredBuffs.Link,bool>? host=null)
    {
        source=null!;lineage=[];
        if(!profile.StatisticsOnly||level is <0 or >short.MaxValue||Volatile.Read(ref active) is not { } package
            ||!package.Buffs.TryGetValue(id,out var candidate))return false;
        if(HasDeclaredBuffRoot(package,profile,id,level)){source=candidate;return true;}
        // R17 child skills are not added to NPC skill lists. Their numeric value
        // is proven for every legal level only when all source deltas are zero;
        // existing trigger admission, level choice and lifetime remain in charge.
        if(!RetailNpcStatTriggeredBuffs.IsConstant(candidate))return false;
        var roots=DeclaredSkills(profile).Where(s=>HasDeclaredBuffRoot(package,profile,s.Id,s.Level)).Select(s=>s.Id);
        if(!package.TriggeredBuffs.TryResolve(id,roots,out lineage,host))return false;
        source=candidate;return true;
    }

    internal static bool TriggeredBuffMatchesStack(int id,string? stack)=>
        Volatile.Read(ref active)?.TriggeredBuffs.MatchesStack(id,stack)==true;

    private static IEnumerable<(int Id,int Level)> DeclaredSkills(SkyTempleNpcProfile profile)
    {
        // Statistics-only projection intentionally publishes no replacement
        // skill list. Read its retained source bindings, as TryGetSkill does.
        var groups=profile.Source.Source.GetRawRootChildren("skills");
        if(groups.Length!=1)yield break;
        var rows=RetailNpcSourceProfile.RawChildren(groups[0]).Where(x=>x[0].GetString()=="data").ToArray();
        foreach(var binding in profile.Source.Source.SkillBindings.EnumerateArray())
        {
            if(!binding.TryGetProperty("supported48_id",out var id)||id.ValueKind!=JsonValueKind.Number
                ||binding.GetProperty("status").GetString()!="dual_join_agrees_static_only"
                ||binding.GetProperty("source_group_index").GetInt32()!=0)continue;
            int index=binding.GetProperty("source_entry_index").GetInt32();
            if(index<0||index>=rows.Length)continue;
            int level=SkyTempleNpcProfile.LeafInt(rows[index],"skill_level");
            if(level is >=0 and <=short.MaxValue)yield return(id.GetInt32(),level);
        }
    }

}
