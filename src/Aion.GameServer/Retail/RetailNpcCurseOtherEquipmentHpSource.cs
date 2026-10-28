using System.Collections.Frozen;
using System.Text.Json;
using Aion.GameServer.Dataholders.RetailNpc;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Retail;

/// <summary>Reviewed raw/default-enchant HP components for common non-weapon
/// equipment. Tempering, bonus attributes, sockets and effects remain separate.</summary>
internal sealed class RetailNpcCurseOtherEquipmentHpSource
{
    internal const string ReviewedAccessoriesSha="7ce2457a34b8082826146804a1af8223e9e8f36d9e4c1a41603baea56f2d1470";
    internal enum Failure { None, UnknownItem, IdentityMismatch, HostTemplateDrift, UnreviewedEnchantLevel, SlotMismatch }
    internal sealed record Candidate(int ItemId,string Name,string HostGroup,int HostMaxEnchant,int MaxReviewedEnchant,bool MaxHpExplicit)
    { internal int RawMaxHp=>0;internal int EnchantMaxHp=>0; }
    private static readonly (int Start,int End,string Group,long Slots,int MaxEnchant)[] Ranges=
    [ (115000000,115100000,"SHIELD",(long)ItemSlot.SUB_HAND,15),
      (125000000,125400000,"HEAD",(long)ItemSlot.HELMET,0),
      (120000000,120100000,"EARRING",(long)ItemSlot.EARRING_RIGHT_OR_LEFT,0),
      (121000000,121100000,"NECKLACE",(long)ItemSlot.NECKLACE,0),
      (122000000,122100000,"RING",(long)ItemSlot.RING_RIGHT_OR_LEFT,0),
      (123000000,123100000,"BELT",(long)ItemSlot.WAIST,0) ];
    private readonly FrozenDictionary<int,Candidate> items;
    private RetailNpcCurseOtherEquipmentHpSource(Dictionary<int,Candidate> items)=>this.items=items.ToFrozenDictionary();
    internal int ItemCount=>items.Count;
    internal static RetailNpcCurseOtherEquipmentHpSource Parse(byte[] bytes,string sha)
    {
        RetailNpcStatCatalog.CheckHash(bytes,sha,"curse other equipment HP source");
        using var doc=JsonDocument.Parse(bytes,new JsonDocumentOptions {MaxDepth=12});
        var root=doc.RootElement;RetailNpcDatasetValidation.UniqueJson(root);
        if(root.GetProperty("version").GetInt32()!=1||root.GetProperty("purpose").GetString()!="retail-other-equipment-hp-base-v1"
            ||root.GetProperty("region").GetString()!="USA")throw new InvalidDataException("Unsupported curse equipment HP identity");
        var p=root.GetProperty("provenance");
        if(p.GetProperty("retail_armors_sha256").GetString()!=RetailNpcCurseArmorSource.ReviewedRetailItemsSha
            ||p.GetProperty("retail_accessories_sha256").GetString()!=ReviewedAccessoriesSha
            ||p.GetProperty("retail_enchant_sha256").GetString()!=RetailNpcCurseArmorSource.ReviewedRetailEnchantsSha
            ||p.GetProperty("host_items_sha256").GetString()!=RetailNpcCurseArmorSource.ReviewedHostItemsSha)
            throw new InvalidDataException("Invalid curse equipment HP provenance");
        foreach(string key in new[]{"armor_selection","accessory_selection","enchant_selection"})
            if(p.GetProperty(key).GetString() is not ("USA override" or "USA missing; base fallback"))
                throw new InvalidDataException("Unreviewed curse equipment HP region selection");
        var items=new Dictionary<int,Candidate>();
        foreach(var row in root.GetProperty("items").EnumerateArray())
        {
            int id=row.GetProperty("id").GetInt32(),cap=row.GetProperty("host_max_enchant").GetInt32(),max=row.GetProperty("max_enchant_level").GetInt32();
            string? name=row.GetProperty("name").GetString(),group=row.GetProperty("host_group").GetString();
            var range=Ranges.SingleOrDefault(x=>id>=x.Start&&id<x.End);
            if(range.Group is null||group!=range.Group||max!=range.MaxEnchant||string.IsNullOrEmpty(name)||name.Any(c=>c>0x7f)
                ||cap is <0 or >255||(group=="SHIELD"&&cap>15)
                ||!items.TryAdd(id,new(id,name,group,cap,max,row.GetProperty("max_hp_explicit").GetBoolean())))
                throw new InvalidDataException("Invalid or duplicate curse equipment HP item");
        }
        if(items.Count is <1 or >100000)throw new InvalidDataException("Invalid curse equipment HP item count");
        return new(items);
    }
    internal Failure Lookup(int id,ItemTemplate template,int enchant,long occupiedSlot,out Candidate? candidate)
    {
        candidate=null;
        if(!items.TryGetValue(id,out var found))return Failure.UnknownItem;
        string? name=template.GetRetailSourceName();
        if(name is null||name.Any(c=>c>0x7f)||!string.Equals(name,found.Name,StringComparison.OrdinalIgnoreCase))return Failure.IdentityMismatch;
        if(template.GetTemplateId()!=id||template.IsWeapon()||template.GetItemGroup().ToString()!=found.HostGroup
            ||template.GetMaxEnchantLevel()!=found.HostMaxEnchant||template.GetEnchantName() is not null)return Failure.HostTemplateDrift;
        var range=Ranges.Single(x=>id>=x.Start&&id<x.End);
        if(occupiedSlot<=0||(occupiedSlot&(occupiedSlot-1))!=0||(range.Slots&occupiedSlot)==0)return Failure.SlotMismatch;
        if(enchant<0||enchant>found.MaxReviewedEnchant||enchant>found.HostMaxEnchant)return Failure.UnreviewedEnchantLevel;
        candidate=found;return Failure.None;
    }
}
