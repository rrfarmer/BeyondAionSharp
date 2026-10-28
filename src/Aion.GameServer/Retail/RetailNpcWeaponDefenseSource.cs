using System.Collections.Frozen;
using System.Text.Json;
using Aion.GameServer.Dataholders.RetailNpc;

namespace Aion.GameServer.Retail;

/// <summary>Offline evidence for zero raw and default-enchant weapon defense.
/// Item bonuses, equip eligibility and other live effects remain separate.</summary>
internal sealed class RetailNpcWeaponDefenseSource
{
    internal enum Failure { None, UnknownItem, IdentityMismatch, UnreviewedEnchant }
    private sealed record Row(int Id,string Name,string Group,string Table,int MaxEnchant);
    private readonly FrozenDictionary<int,Row> items;
    private RetailNpcWeaponDefenseSource(Dictionary<int,Row> items)=>this.items=items.ToFrozenDictionary();
    internal int ItemCount=>items.Count;
    internal static RetailNpcWeaponDefenseSource ParseForOfflineFixture(byte[] bytes,string sha)
    {
        if(sha!="ae8a1ebe3fa68fac4faf3b2b477d46e4e45a08fcf80e327a67483639477da11b")
            throw new InvalidDataException("Unreviewed weapon defense source");
        RetailNpcStatCatalog.CheckHash(bytes,sha,"weapon physical-defense source");
        using var doc=JsonDocument.Parse(bytes,new JsonDocumentOptions {MaxDepth=12});var root=doc.RootElement;
        RetailNpcDatasetValidation.UniqueJson(root);
        if(root.GetProperty("version").GetInt32()!=1||root.GetProperty("purpose").GetString()!="offline-reviewed-weapon-physical-defense-zero"
            ||root.GetProperty("region").GetString()!="USA"||root.GetProperty("max_reviewed_enchant").GetInt32()!=15)
            throw new InvalidDataException("Unreviewed weapon defense identity");
        var provenance=root.GetProperty("provenance");
        foreach(var (key,value) in new[]{
            ("weapons_sha256","f10df47b68c161ba1d76aa1f4bd2853634a57e114dc61c50f63e89d2e657344f"),
            ("enchant_sha256","1938ed5e4be74f2ba4fd8bac6c7f25e232987090bab27c3684724b02f39a6850"),
            ("identity_sha256","09c3411e58241d3b51b82d676841e4f2b09d29fffeb2114eeae7e115c8f6a53a"),
            ("default_proof_sha256","1f50138ffcf06c31e5abb2cf1aa54e047cf3f052d9d95f79632235152c8eece2")})
            if(provenance.GetProperty(key).GetString()!=value)throw new InvalidDataException("Unreviewed weapon defense provenance: "+key);
        if(provenance.GetProperty("selection").GetString()!="USA missing; base fallback")
            throw new InvalidDataException("Unreviewed weapon defense region");
        var tables=root.GetProperty("tables").EnumerateArray().Select(x=>x.GetString()).ToHashSet(StringComparer.Ordinal);
        if(tables.Count!=13 ||tables.Contains(null))throw new InvalidDataException("Invalid weapon defense tables");
        var items=new Dictionary<int,Row>();
        foreach(var row in root.GetProperty("items").EnumerateArray())
        {
            int id=row.GetProperty("id").GetInt32(),cap=row.GetProperty("host_max_enchant").GetInt32();
            string? name=row.GetProperty("name").GetString(),group=row.GetProperty("host_group").GetString(),table=row.GetProperty("table").GetString();
            if(id<=0||cap is <0 or >15||string.IsNullOrEmpty(name)||name.Any(c=>c>0x7f)||string.IsNullOrEmpty(group)
                ||string.IsNullOrEmpty(table)||!tables.Contains(table)||!items.TryAdd(id,new(id,name,group,table,cap)))
                throw new InvalidDataException("Invalid weapon defense item");
        }
        if(items.Count!=17780)throw new InvalidDataException("Unexpected weapon defense coverage");
        return new(items);
    }
    internal Failure Lookup(RetailNpcCurseWeaponHpSource.Candidate candidate,int enchant)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if(!items.TryGetValue(candidate.ItemId,out var row))return Failure.UnknownItem;
        if(!string.Equals(candidate.Name,row.Name,StringComparison.OrdinalIgnoreCase)||candidate.HostGroup!=row.Group
            ||candidate.Table!=row.Table||candidate.HostMaxEnchant!=row.MaxEnchant)return Failure.IdentityMismatch;
        return enchant<0||enchant>15||enchant>row.MaxEnchant?Failure.UnreviewedEnchant:Failure.None;
    }
}
