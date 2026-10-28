using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;
using Aion.GameServer.Dataholders.RetailNpc;

namespace Aion.GameServer.Retail;

/// <summary>Selected 5.8 class requirements for matched item identities.
/// This does not establish equip eligibility or the native item adjustment.</summary>
internal sealed class RetailNpcCurseClassPermitSource
{
    internal const string ReviewedSha="b878cf8dc464f86708cf9149b5c28411351377dd49e474629c8a6320b27aae44";
    private sealed record Row(int Id,string Name,string Group,string Kind,ImmutableArray<byte> Permits,int Present);
    private readonly FrozenDictionary<int,Row> items;
    private RetailNpcCurseClassPermitSource(Dictionary<int,Row> items)=>this.items=items.ToFrozenDictionary();
    internal int ItemCount=>items.Count;

    internal static RetailNpcCurseClassPermitSource Parse(byte[] bytes,string sha)
    {
        if(sha!=ReviewedSha)throw new InvalidDataException("Unreviewed item class permit source");
        RetailNpcStatCatalog.CheckHash(bytes,sha,"curse class permit source");
        using var doc=JsonDocument.Parse(bytes,new JsonDocumentOptions {MaxDepth=16});
        var root=doc.RootElement;RetailNpcDatasetValidation.UniqueJson(root);
        if(root.GetProperty("version").GetInt32()!=1 ||root.GetProperty("purpose").GetString()!="retail-matched-item-class-permits-v1"
            ||root.GetProperty("region").GetString()!="USA"
            ||root.GetProperty("bonus_source_sha256").GetString()!=RetailNpcCurseItemBonusSource.ReviewedSourceSha)
            throw new InvalidDataException("Invalid item class permit provenance");
        var source=root.GetProperty("retail_source_sha256");
        if(source.GetProperty("item_armors.xml").GetString()!=RetailNpcCurseArmorSource.ReviewedRetailItemsSha
            ||source.GetProperty("item_weapons.xml").GetString()!=RetailNpcCurseOffhandSource.ReviewedWeaponsSha
            ||source.GetProperty("item_accessories.xml").GetString()!=RetailNpcCurseOtherEquipmentHpSource.ReviewedAccessoriesSha)
            throw new InvalidDataException("Invalid retail item source identity");
        var rows=new Dictionary<int,Row>();
        foreach(var row in root.GetProperty("items").EnumerateArray())
        {
            int id=row.GetProperty("id").GetInt32(),present=row.GetProperty("class_present_mask").GetInt32();
            string? name=row.GetProperty("name").GetString(),group=row.GetProperty("host_group").GetString(),kind=row.GetProperty("kind").GetString();
            var permits=row.GetProperty("class_permits").EnumerateArray().Select(x=>x.GetByte()).ToImmutableArray();
            if(id<=0 ||string.IsNullOrEmpty(name)||name.Any(c=>c>0x7f) ||string.IsNullOrEmpty(group)
                ||kind is not ("armor" or "weapon" or "other") ||permits.Length!=17 ||permits.Any(x=>x>=100)
                ||present is <0 or >=(1<<17) ||permits.Where((x,i)=>(present&(1<<i))==0&&x!=0).Any()
                ||!rows.TryAdd(id,new(id,name,group,kind,permits,present)))
                throw new InvalidDataException("Invalid or duplicate item class permit");
        }
        if(rows.Count!=54153)throw new InvalidDataException("Item class permit coverage changed");
        return new(rows);
    }

    internal bool TryLookup(RetailNpcCurseItemBonusSource.Candidate item,int nativeClass,out byte permit,out bool sourcePresent)
    {
        ArgumentNullException.ThrowIfNull(item);permit=0;sourcePresent=false;
        if((uint)nativeClass>=17 ||!items.TryGetValue(item.ItemId,out var row)
            ||!string.Equals(row.Name,item.Name,StringComparison.OrdinalIgnoreCase) ||row.Group!=item.HostGroup ||row.Kind!=item.Kind)
            return false;
        permit=row.Permits[nativeClass];sourcePresent=(row.Present&(1<<nativeClass))!=0;return true;
    }
}
