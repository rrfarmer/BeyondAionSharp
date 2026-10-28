using System.Collections.Frozen;
using System.Text.Json;
using Aion.GameServer.Dataholders.RetailNpc;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Retail;

/// <summary>Reviewed zero raw/default-enchant weapon HP inputs. This does not
/// include item bonuses, player state or effect ownership and cannot by itself
/// establish a complete retail curse calculation.</summary>
internal sealed class RetailNpcCurseWeaponHpSource
{
    internal enum Failure { None, UnknownItem, IdentityMismatch, HostTemplateDrift, UnreviewedEnchantLevel }
    internal sealed record Candidate(int ItemId,string Name,string HostGroup,string Table,int HostMaxEnchant,bool MaxHpExplicit)
    {
        internal int RawMaxHp=>0;
        internal int EnchantMaxHp=>0;
        // Matched group and ID ranges are validated during source parsing.
        // The native item factory selects TwoHandWeapon for these ranges.
        internal bool IsRetailTwoHanded=>Table is "orb" or "book" or "2hsword" or "polearm" or "staff" or "bow" or "cannon" or "harp" or "keyblade"
            &&Ranges.Any(x=>ItemId>=x.Start &&ItemId<x.Start+100000 &&HostGroup==x.Group &&Table==x.Table);
    }
    private static readonly (int Start,string Group,string Table)[] Ranges=
    [ (100000000,"SWORD","sword"),(100100000,"MACE","mace"),(100200000,"DAGGER","dagger"),
      (100500000,"ORB","orb"),(100600000,"SPELLBOOK","book"),(100900000,"GREATSWORD","2hsword"),
      (101300000,"POLEARM","polearm"),(101500000,"STAFF","staff"),(101700000,"BOW","bow"),
      (101800000,"GUN","gun"),(101900000,"CANNON","cannon"),(102000000,"HARP","harp"),
      (102100000,"KEYBLADE","keyblade") ];
    private readonly FrozenDictionary<int,Candidate> items;
    private RetailNpcCurseWeaponHpSource(Dictionary<int,Candidate> items)=>this.items=items.ToFrozenDictionary();
    internal int ItemCount=>items.Count;
    internal static RetailNpcCurseWeaponHpSource Parse(byte[] bytes,string sha)
    {
        RetailNpcStatCatalog.CheckHash(bytes,sha,"curse weapon HP source");
        using var doc=JsonDocument.Parse(bytes,new JsonDocumentOptions {MaxDepth=12});
        var root=doc.RootElement;RetailNpcDatasetValidation.UniqueJson(root);
        if(root.GetProperty("version").GetInt32()!=1||root.GetProperty("purpose").GetString()!="retail-default-weapon-hp-base-v1"
            ||root.GetProperty("region").GetString()!="USA")throw new InvalidDataException("Unsupported curse weapon HP identity");
        var provenance=root.GetProperty("provenance");
        if(provenance.GetProperty("retail_items_sha256").GetString()!=RetailNpcCurseOffhandSource.ReviewedWeaponsSha
            ||provenance.GetProperty("retail_enchant_sha256").GetString()!=RetailNpcCurseArmorSource.ReviewedRetailEnchantsSha
            ||provenance.GetProperty("host_items_sha256").GetString()!=RetailNpcCurseArmorSource.ReviewedHostItemsSha
            ||provenance.GetProperty("retail_selection").GetString() is not ("USA override" or "USA missing; base fallback")
            ||provenance.GetProperty("enchant_selection").GetString() is not ("USA override" or "USA missing; base fallback"))
            throw new InvalidDataException("Invalid curse weapon HP provenance");
        var items=new Dictionary<int,Candidate>();
        foreach(var row in root.GetProperty("items").EnumerateArray())
        {
            int id=row.GetProperty("id").GetInt32(),cap=row.GetProperty("host_max_enchant").GetInt32();
            string? name=row.GetProperty("name").GetString(),group=row.GetProperty("host_group").GetString(),table=row.GetProperty("table").GetString();
            var range=Ranges.SingleOrDefault(x=>id>=x.Start&&id<x.Start+100000);
            if(range.Group is null||group!=range.Group||table!=range.Table||string.IsNullOrEmpty(name)||name.Any(c=>c>0x7f)
                ||cap is <0 or >15||!items.TryAdd(id,new(id,name,group,table,cap,row.GetProperty("max_hp_explicit").GetBoolean())))
                throw new InvalidDataException("Invalid or duplicate curse weapon HP item");
        }
        if(items.Count is <1 or >100000)throw new InvalidDataException("Invalid curse weapon HP item count");
        return new(items);
    }
    internal Failure Lookup(int id,ItemTemplate template,int enchant,out Candidate? candidate)
    {
        candidate=null;
        if(!items.TryGetValue(id,out var found))return Failure.UnknownItem;
        string? name=template.GetRetailSourceName();
        if(name is null||name.Any(c=>c>0x7f)||!string.Equals(name,found.Name,StringComparison.OrdinalIgnoreCase))return Failure.IdentityMismatch;
        if(template.GetTemplateId()!=id||!template.IsWeapon()||template.GetItemGroup().ToString()!=found.HostGroup
            ||template.GetMaxEnchantLevel()!=found.HostMaxEnchant||template.GetEnchantName() is not null)return Failure.HostTemplateDrift;
        if(enchant<0||enchant>15||enchant>found.HostMaxEnchant)return Failure.UnreviewedEnchantLevel;
        candidate=found;return Failure.None;
    }
}
