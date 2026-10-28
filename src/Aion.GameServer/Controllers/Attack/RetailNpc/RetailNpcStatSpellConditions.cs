using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.Dataholders.RetailNpc;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Selected native RaceDB projection for player and reviewed NPC sources.
/// Race IDs, race orders and 4.8 enum values are different domains. The exact
/// source context is mandatory before a race predicate can be admitted.</summary>
internal sealed class RetailNpcStatSpellConditions
{
    internal const string ContextSha256 = "5bc31d3be7705cf25a1baa6efd049d8c4ff92483c577dbe39a3cfae7915bd4dd";
    internal const string RaceSourceSha256 = "c7a056b0b0560500ba96e36fb710187410ef071c70f0550c0da5c388f24f4e7a";
    private readonly FrozenDictionary<string,int> playerMasks;
    internal readonly record struct NativeRace(int RawId,int Order);
    private readonly FrozenDictionary<string,NativeRace> nativeRaces;
    private RetailNpcStatSpellConditions(Dictionary<string,int> masks,Dictionary<string,NativeRace> races)
    {
        playerMasks=masks.ToFrozenDictionary(StringComparer.Ordinal);
        nativeRaces=races.ToFrozenDictionary(StringComparer.Ordinal);
    }

    internal static RetailNpcStatSpellConditions Parse(ReadOnlySpan<byte> bytes,string expected)
    {
        if(bytes.Length is <=0 or >65536 || expected!=ContextSha256
            ||Convert.ToHexStringLower(SHA256.HashData(bytes))!=ContextSha256)
            throw new InvalidDataException("Unreviewed native spell condition context");
        using var doc=JsonDocument.Parse(bytes.ToArray(),new(){MaxDepth=16});
        var root=doc.RootElement;RetailNpcDatasetValidation.UniqueJson(root);
        if(root.GetProperty("version").GetInt32()!=1
            ||root.GetProperty("kind").GetString()!="selected-native-player-race-conditions-v1"
            ||root.GetProperty("main_sha256").GetString()!=RetailNpcNormalScope.MainBinarySha256
            ||root.GetProperty("race_source").GetProperty("sha256").GetString()!=RaceSourceSha256
            ||root.GetProperty("race_source").GetProperty("selection").GetString()!="USA_missing_base_fallback"
            ||root.GetProperty("ordered_count").GetInt32()!=64)
            throw new InvalidDataException("Native spell condition source identity differs");
        var masks=new Dictionary<string,int>(StringComparer.Ordinal);
        var races=new Dictionary<string,NativeRace>(StringComparer.Ordinal);
        var orders=new HashSet<int>();var ids=new HashSet<int>();
        foreach(var row in root.GetProperty("races").EnumerateArray())
        {
            string name=row.GetProperty("name").GetString()!;
            int id=row.GetProperty("raw_id").GetInt32(),order=row.GetProperty("order").GetInt32();
            int mask=row.GetProperty("player_mask").GetInt32();
            if(name.Length is <1 or >128 || !name.All(c=>c<128)||name!=RetailNpcStatCatalog.NativeSkillNameKey(name)
                ||id is <0 or >32767 || order is <0 or >63 || !ids.Add(id)||!orders.Add(order)
                || mask!=(id==0?1:id==1?2:0)||!masks.TryAdd(name,mask)
                || !races.TryAdd(name,new(id,order)))
                throw new InvalidDataException("Native spell race/order projection differs");
            if(id==0&&(name!="pc_light"||order!=0)||id==1&&(name!="pc_dark"||order!=1))
                throw new InvalidDataException("Native ordinary player race mapping differs");
        }
        if(masks.Count!=64||!masks.TryGetValue("pc_light",out int light)||light!=1
            ||!masks.TryGetValue("pc_dark",out int dark)||dark!=2)
            throw new InvalidDataException("Incomplete native spell race mapping");
        return new(masks,races);
    }

    internal bool TryPlayerMask(string name,out int mask) => playerMasks.TryGetValue(name,out mask);
    internal bool TryNativeRace(string name,out NativeRace race) => nativeRaces.TryGetValue(name,out race);

    // The NPC source record's race_type is resolved by the selected 5.8 RaceDB.
    // It is neither the host faction nor the RaceDB order itself. Missing,
    // duplicated and unknown names do not acquire a guessed default.
    internal bool TryNpcRace(SkyTempleNpcProfile profile,out NativeRace race)
    {
        race=default;
        var values=profile.Source.Source.GetRawRootField("race_type").RawValues;
        if(values.Length!=1)return false;
        string name=values[0].Trim();
        if(name.Length==0 || !name.All(c=>c<=0x7f))return false;
        return nativeRaces.TryGetValue(RetailNpcStatCatalog.NativeSkillNameKey(name),out race);
    }

    // Only the catalog may bind a live actor to the reviewed source record.
    // This lookup alone does not admit any NPC-targeted skill or damage path.
    internal bool TryNpcRace(Npc npc,out NativeRace race)
    {
        race=default;
        return RetailNpcStatCatalog.TryGet(npc,out var profile) && TryNpcRace(profile,out race);
    }
}
