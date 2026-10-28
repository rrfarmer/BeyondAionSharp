using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;
using Aion.GameServer.Dataholders.RetailNpc;

namespace Aion.GameServer.Retail;

/// <summary>Offline 5.8 shield defense inputs. No equipment eligibility,
/// bonuses, item effects, or live player state are established here.</summary>
internal sealed class RetailNpcShieldDefenseSource
{
    internal enum Failure { None, UnknownItem, IdentityMismatch, UnreviewedEnchant }
    internal sealed record Candidate(int Id,string Name,int RawPhysicalDefense,bool PhysicalDefenseExplicit,int HostMaxEnchant,
        ImmutableArray<byte> ClassPermits,int ClassPresentMask)
    {
        internal bool TryGetClassPermit(int nativeClass,out byte permit,out bool present)
        {
            permit=0;present=false;
            if((uint)nativeClass>=17)return false;
            permit=ClassPermits[nativeClass];present=(ClassPresentMask&(1<<nativeClass))!=0;return true;
        }
    }
    private readonly FrozenDictionary<int,Candidate> items;
    private RetailNpcShieldDefenseSource(Dictionary<int,Candidate> items)=>this.items=items.ToFrozenDictionary();
    internal int ItemCount=>items.Count;

    internal static RetailNpcShieldDefenseSource ParseForOfflineFixture(byte[] bytes,string sha)
    {
        if(sha!="5d31e7d11491ec75d4c2804fe59632e4566ea0228b4bddf7e1b4320f7da69ae6")
            throw new InvalidDataException("Unreviewed shield defense source");
        RetailNpcStatCatalog.CheckHash(bytes,sha,"shield physical-defense source");
        using var doc=JsonDocument.Parse(bytes,new JsonDocumentOptions {MaxDepth=12});var root=doc.RootElement;
        RetailNpcDatasetValidation.UniqueJson(root);
        if(root.GetProperty("version").GetInt32()!=2||root.GetProperty("purpose").GetString()!="offline-reviewed-shield-physical-defense"
            ||root.GetProperty("region").GetString()!="USA"||root.GetProperty("max_reviewed_enchant").GetInt32()!=15)
            throw new InvalidDataException("Unreviewed shield defense identity");
        var provenance=root.GetProperty("provenance");
        foreach(var (key,value) in new[]{
            ("armors_sha256","11167024df7400bf6ff051f84bcf6ef1dffdec1949b33ff409f42e39e3e9716a"),
            ("enchant_sha256","1938ed5e4be74f2ba4fd8bac6c7f25e232987090bab27c3684724b02f39a6850"),
            ("identity_sha256","22ca15359baded4953cacf6a19ee6e61fd33e92ed05d0cc7f048072df8cd001e"),
            ("permit_sha256","b878cf8dc464f86708cf9149b5c28411351377dd49e474629c8a6320b27aae44")})
            if(provenance.GetProperty(key).GetString()!=value)throw new InvalidDataException("Unreviewed shield provenance: "+key);
        if(provenance.GetProperty("selection").GetString()!="USA missing; base fallback")
            throw new InvalidDataException("Unreviewed shield region");
        var items=new Dictionary<int,Candidate>();
        foreach(var row in root.GetProperty("items").EnumerateArray())
        {
            int id=row.GetProperty("id").GetInt32(),raw=row.GetProperty("raw_pdef").GetInt32();
            bool physicalPresent=row.GetProperty("physical_present").GetBoolean();
            int cap=row.GetProperty("host_max_enchant").GetInt32(),present=row.GetProperty("class_present_mask").GetInt32();
            string? name=row.GetProperty("name").GetString();
            var permits=row.GetProperty("class_permits").EnumerateArray().Select(x=>x.GetByte()).ToImmutableArray();
            if(id is <115000000 or >=115100000 ||raw<0 ||!physicalPresent && raw!=0 ||cap is <0 or >15 ||string.IsNullOrEmpty(name)
                ||name.Any(c=>c>0x7f) ||permits.Length!=17 ||permits.Any(x=>x>=100)
                ||present is <0 or >=(1<<17) ||permits.Where((x,i)=>(present&(1<<i))==0&&x!=0).Any()
                ||!items.TryAdd(id,new(id,name,raw,physicalPresent,cap,permits,present)))throw new InvalidDataException("Invalid shield defense item");
        }
        var defaulted=root.GetProperty("defaulted_absent_pdef").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
        if(items.Count!=1635 ||defaulted.Length!=37 ||defaulted.Distinct().Count()!=37
            ||!defaulted.ToHashSet().SetEquals(items.Values.Where(x=>!x.PhysicalDefenseExplicit).Select(x=>x.Id)))
            throw new InvalidDataException("Unexpected shield defense coverage");
        return new(items);
    }

    internal Failure Lookup(int id,string? hostName,int enchant,out Candidate? candidate)
    {
        candidate=null;
        if(!items.TryGetValue(id,out var item))return Failure.UnknownItem;
        if(hostName is null||hostName.Any(c=>c>0x7f)||!string.Equals(hostName,item.Name,StringComparison.OrdinalIgnoreCase))
            return Failure.IdentityMismatch;
        if(enchant<0||enchant>15||enchant>item.HostMaxEnchant)return Failure.UnreviewedEnchant;
        candidate=item;return Failure.None;
    }

    internal static int RevisedPhysicalDefense(Candidate item,ushort requiredLevel,ushort playerLevel,
        bool revisionEnabled,RetailNpcArmorDefenseSource.Restriction restriction)
    {
        ArgumentNullException.ThrowIfNull(item);
        // The selected default shield table contributes zero physical defense
        // through +15; its selector-2 revision is shared with armor.
        var armor=new RetailNpcArmorDefenseSource.Candidate(item.Id,item.Name,item.RawPhysicalDefense,0,item.HostMaxEnchant,15,"shield");
        return RetailNpcArmorDefenseSource.RevisedPhysicalDefense(armor,requiredLevel,playerLevel,revisionEnabled,restriction);
    }
}
