using System.Collections.Frozen;
using System.Text.Json;
using Aion.GameServer.Dataholders.RetailNpc;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Retail;

/// <summary>Source-bound identities for the native off-hand weapon stat gate.
/// This does not verify any other item contribution or a complete curse base.</summary>
internal sealed class RetailNpcCurseOffhandSource
{
    internal const string ReviewedWeaponsSha="f10df47b68c161ba1d76aa1f4bd2853634a57e114dc61c50f63e89d2e657344f";
    internal const string ReviewedHostItemsSha=RetailNpcCurseArmorSource.ReviewedHostItemsSha;
    internal enum Failure { None, UnknownItem, IdentityMismatch, HostTemplateDrift }
    private sealed record Item(string Name,string HostGroup);
    private static readonly FrozenSet<string> Groups=new[]{"SWORD","DAGGER","MACE","ORB","SPELLBOOK","GREATSWORD",
        "POLEARM","STAFF","BOW","GUN","CANNON","HARP","KEYBLADE"}.ToFrozenSet(StringComparer.Ordinal);
    private readonly FrozenDictionary<int,Item> items;
    internal int ItemCount=>items.Count;
    private RetailNpcCurseOffhandSource(Dictionary<int,Item> items)=>this.items=items.ToFrozenDictionary();

    internal static RetailNpcCurseOffhandSource Parse(byte[] bytes,string sha)
    {
        RetailNpcStatCatalog.CheckHash(bytes,sha,"curse offhand weapon source");
        using var doc=JsonDocument.Parse(bytes,new JsonDocumentOptions {MaxDepth=12});
        var root=doc.RootElement;RetailNpcDatasetValidation.UniqueJson(root);
        if(root.GetProperty("version").GetInt32()!=1 || root.GetProperty("purpose").GetString()!="retail-offhand-weapon-identity-v1"
            ||root.GetProperty("region").GetString()!="USA")throw new InvalidDataException("Unsupported curse weapon source identity");
        var provenance=root.GetProperty("provenance");
        if(provenance.GetProperty("retail_items_sha256").GetString()!=ReviewedWeaponsSha
            ||provenance.GetProperty("host_items_sha256").GetString()!=ReviewedHostItemsSha
            ||provenance.GetProperty("retail_selection").GetString() is not ("USA override" or "USA missing; base fallback"))
            throw new InvalidDataException("Invalid curse weapon provenance");
        var items=new Dictionary<int,Item>();
        foreach(var row in root.GetProperty("items").EnumerateArray())
        {
            int id=row.GetProperty("id").GetInt32();string? name=row.GetProperty("name").GetString(),group=row.GetProperty("host_group").GetString();
            if(id is <100000000 or >=103000000 || string.IsNullOrEmpty(name) || name.Any(c=>c>0x7f)
                || group is null || !Groups.Contains(group) || !items.TryAdd(id,new(name,group)))
                throw new InvalidDataException("Invalid or duplicate curse weapon item");
        }
        if(items.Count is <1 or >100000)throw new InvalidDataException("Invalid curse weapon item count");
        return new(items);
    }

    internal Failure Lookup(int id,ItemTemplate template)
    {
        if(!items.TryGetValue(id,out var item))return Failure.UnknownItem;
        string? name=template.GetRetailSourceName();
        if(name is null||name.Any(c=>c>0x7f)||!string.Equals(name,item.Name,StringComparison.OrdinalIgnoreCase))return Failure.IdentityMismatch;
        return template.GetTemplateId()!=id||!template.IsWeapon()||template.GetItemGroup().ToString()!=item.HostGroup
            ?Failure.HostTemplateDrift:Failure.None;
    }
}
