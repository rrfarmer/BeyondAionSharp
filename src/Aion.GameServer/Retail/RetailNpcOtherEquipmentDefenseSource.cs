using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;
using Aion.GameServer.Dataholders.RetailNpc;

namespace Aion.GameServer.Retail;

/// <summary>Offline source for the unenchanted raw defense of matched headgear
/// and accessories. Equip eligibility and additional bonuses remain separate.</summary>
internal sealed class RetailNpcOtherEquipmentDefenseSource
{
    internal enum Failure { None, UnknownItem, IdentityMismatch, UnreviewedEnchant }
    internal sealed record Candidate(int Id,string Name,string HostGroup,int RawPhysicalDefense,bool PhysicalDefenseExplicit,
        int HostMaxEnchant,ImmutableArray<byte> ClassPermits,int ClassPresentMask)
    {
        internal bool TryGetClassPermit(int nativeClass,out byte permit,out bool present)
        {
            permit=0;present=false;
            if((uint)nativeClass>=17)return false;
            permit=ClassPermits[nativeClass];present=(ClassPresentMask&(1<<nativeClass))!=0;return true;
        }
    }
    private readonly FrozenDictionary<int,Candidate> items;
    private RetailNpcOtherEquipmentDefenseSource(Dictionary<int,Candidate> items)=>this.items=items.ToFrozenDictionary();
    internal int ItemCount=>items.Count;

    internal static RetailNpcOtherEquipmentDefenseSource ParseForOfflineFixture(byte[] bytes,string sha)
    {
        if(sha!="53ebb035f0d1aa834638373b56ec991fc527cdf1ac1da56ab52533fd63eb3784")
            throw new InvalidDataException("Unreviewed other equipment defense source");
        RetailNpcStatCatalog.CheckHash(bytes,sha,"other equipment physical-defense source");
        using var doc=JsonDocument.Parse(bytes,new JsonDocumentOptions {MaxDepth=12});var root=doc.RootElement;
        RetailNpcDatasetValidation.UniqueJson(root);
        if(root.GetProperty("version").GetInt32()!=1||root.GetProperty("purpose").GetString()!="offline-reviewed-other-equipment-physical-defense"
            ||root.GetProperty("region").GetString()!="USA"||root.GetProperty("max_reviewed_enchant").GetInt32()!=0)
            throw new InvalidDataException("Unreviewed other equipment defense identity");
        var provenance=root.GetProperty("provenance");
        foreach(var (key,value) in new[]{
            ("armors_sha256","11167024df7400bf6ff051f84bcf6ef1dffdec1949b33ff409f42e39e3e9716a"),
            ("accessories_sha256","7ce2457a34b8082826146804a1af8223e9e8f36d9e4c1a41603baea56f2d1470"),
            ("identity_sha256","22ca15359baded4953cacf6a19ee6e61fd33e92ed05d0cc7f048072df8cd001e"),
            ("permit_sha256","b878cf8dc464f86708cf9149b5c28411351377dd49e474629c8a6320b27aae44"),
            ("default_proof_sha256","1f50138ffcf06c31e5abb2cf1aa54e047cf3f052d9d95f79632235152c8eece2")})
            if(provenance.GetProperty(key).GetString()!=value)throw new InvalidDataException("Unreviewed other equipment provenance: "+key);
        if(provenance.GetProperty("selection").GetString()!="USA missing; base fallback")
            throw new InvalidDataException("Unreviewed other equipment region");
        var items=new Dictionary<int,Candidate>();
        foreach(var row in root.GetProperty("items").EnumerateArray())
        {
            int id=row.GetProperty("id").GetInt32(),raw=row.GetProperty("raw_pdef").GetInt32();
            int cap=row.GetProperty("host_max_enchant").GetInt32(),present=row.GetProperty("class_present_mask").GetInt32();
            bool physicalPresent=row.GetProperty("physical_present").GetBoolean();
            string? name=row.GetProperty("name").GetString(),group=row.GetProperty("host_group").GetString();
            bool range=group switch {"HEAD"=>id is >=125000000 and <125400000,"EARRING"=>id is >=120000000 and <120100000,
                "NECKLACE"=>id is >=121000000 and <121100000,"RING"=>id is >=122000000 and <122100000,
                "BELT"=>id is >=123000000 and <123100000,_=>false};
            var permits=row.GetProperty("class_permits").EnumerateArray().Select(x=>x.GetByte()).ToImmutableArray();
            if(!range ||raw<0 ||!physicalPresent && raw!=0 ||cap is <0 or >255 ||string.IsNullOrEmpty(name)
                ||name.Any(c=>c>0x7f) ||permits.Length!=17 ||permits.Any(x=>x>=100)
                ||present is <0 or >=(1<<17) ||permits.Where((x,i)=>(present&(1<<i))==0&&x!=0).Any()
                ||!items.TryAdd(id,new(id,name,group!,raw,physicalPresent,cap,permits,present)))
                throw new InvalidDataException("Invalid other equipment defense item");
        }
        if(items.Count!=9614 ||items.Values.Count(x=>!x.PhysicalDefenseExplicit)!=4430
            ||items.Values.Count(x=>x.RawPhysicalDefense!=0)!=1364)
            throw new InvalidDataException("Unexpected other equipment defense coverage");
        return new(items);
    }

    internal Failure Lookup(int id,string? hostName,int enchant,out Candidate? candidate)
    {
        candidate=null;
        if(!items.TryGetValue(id,out var item))return Failure.UnknownItem;
        if(hostName is null||hostName.Any(c=>c>0x7f)||!string.Equals(hostName,item.Name,StringComparison.OrdinalIgnoreCase))
            return Failure.IdentityMismatch;
        if(enchant!=0)return Failure.UnreviewedEnchant;
        candidate=item;return Failure.None;
    }

    internal static int RevisedPhysicalDefense(Candidate item,ushort requiredLevel,ushort playerLevel,
        bool revisionEnabled,RetailNpcArmorDefenseSource.Restriction restriction)
    {
        ArgumentNullException.ThrowIfNull(item);
        var armor=new RetailNpcArmorDefenseSource.Candidate(item.Id,item.Name,item.RawPhysicalDefense,0,item.HostMaxEnchant,0,item.HostGroup);
        return RetailNpcArmorDefenseSource.RevisedPhysicalDefense(armor,requiredLevel,playerLevel,revisionEnabled,restriction);
    }
}
