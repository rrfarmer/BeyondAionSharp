using System.Collections.Frozen;
using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders.RetailNpc;

namespace Aion.GameServer.Retail;

/// <summary>Offline source-bound armor physical-defense inputs. This does not
/// establish equipment eligibility, live flags, other bonuses, or a complete
/// player defense value.</summary>
internal sealed class RetailNpcArmorDefenseSource
{
    internal enum Failure { None, UnknownItem, IdentityMismatch, UnreviewedEnchant }
    internal enum Restriction { Computed, Original, Zero }
    internal sealed record Candidate(int Id,string Name,int RawPhysicalDefense,int EnchantPhysicalDefense,
        int HostMaxEnchant,int RetailMaxEnchant,string Table)
    {
        internal bool EquipmentEligibilityVerified=>false;
    }
    private sealed record Item(int Id,string Name,string Table,int Raw,int HostMax,int RetailMax);
    private readonly FrozenDictionary<int,Item> items;
    private readonly FrozenDictionary<string,int[]> tables;
    internal int ItemCount=>items.Count;
    internal int TableCount=>tables.Count;
    private RetailNpcArmorDefenseSource(Dictionary<int,Item> items,Dictionary<string,int[]> tables)
    {this.items=items.ToFrozenDictionary();this.tables=tables.ToFrozenDictionary(StringComparer.Ordinal);}

    internal static RetailNpcArmorDefenseSource ParseForOfflineFixture(byte[] bytes,string sha)
    {
        RetailNpcStatCatalog.CheckHash(bytes,sha,"armor physical-defense source");
        using var doc=JsonDocument.Parse(bytes,new JsonDocumentOptions {MaxDepth=12});var root=doc.RootElement;
        RetailNpcDatasetValidation.UniqueJson(root);
        if(root.GetProperty("version").GetInt32()!=1||root.GetProperty("purpose").GetString()!="offline-reviewed-default-armor-physical-defense"
            ||root.GetProperty("region").GetString()!="USA")throw new InvalidDataException("Unreviewed armor defense source");
        var provenance=root.GetProperty("provenance");
        foreach(var (key,value) in new[]{
            ("items_sha256","11167024df7400bf6ff051f84bcf6ef1dffdec1949b33ff409f42e39e3e9716a"),
            ("hp_identity_sha256","72fa5bf580b1ace67426d6a15081531a75b7760962d601de59eed6fdb62a28ff"),
            ("pdef_matrix_sha256","e38973e71c95b5415f39da07c8952ceb0dbd8402d604a8a2cd51e956a1598645")})
            if(provenance.GetProperty(key).GetString()!=value)throw new InvalidDataException("Unreviewed armor defense provenance: "+key);
        if(provenance.GetProperty("selection").GetString()!="USA missing; base fallback")
            throw new InvalidDataException("Unreviewed armor defense region");
        var tables=new Dictionary<string,int[]>(StringComparer.Ordinal);
        foreach(var row in root.GetProperty("tables").EnumerateArray())
        {
            string name=Text(row,"name");var levels=row.GetProperty("levels").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            if(levels.Length!=15||levels.Any(x=>x<0)||!tables.TryAdd(name,levels))throw new InvalidDataException("Invalid armor defense table");
        }
        if(tables.Count!=20)throw new InvalidDataException("Unexpected armor defense table count");
        var items=new Dictionary<int,Item>();
        foreach(var row in root.GetProperty("items").EnumerateArray())
        {
            int id=row.GetProperty("id").GetInt32(),raw=row.GetProperty("raw_pdef").GetInt32();
            int host=row.GetProperty("host_max_enchant").GetInt32(),retail=row.GetProperty("retail_max_enchant").GetInt32();
            string name=Text(row,"name"),table=Text(row,"table");
            if(id<=0||raw<0||host is <0 or >15||retail is <0 or >255||!tables.ContainsKey(table)
                ||!items.TryAdd(id,new(id,name,table,raw,host,retail)))throw new InvalidDataException("Invalid armor defense item");
        }
        if(items.Count!=25124)throw new InvalidDataException("Unexpected armor defense item count");
        return new(items,tables);
    }

    internal Failure Lookup(int id,string? hostName,int enchant,out Candidate? candidate)
    {
        candidate=null;
        if(!items.TryGetValue(id,out var item))return Failure.UnknownItem;
        if(hostName is null||hostName.Any(c=>c>0x7f)||!string.Equals(hostName,item.Name,StringComparison.OrdinalIgnoreCase))
            return Failure.IdentityMismatch;
        if(enchant<0||enchant>15||enchant>item.HostMax||enchant>item.RetailMax)return Failure.UnreviewedEnchant;
        candidate=new(id,item.Name,item.Raw,enchant==0?0:tables[item.Table][enchant-1],item.HostMax,item.RetailMax,item.Table);
        return Failure.None;
    }

    // Selector 2 follows the same native float revision structure as selector
    // 5, but reads physical-defense inputs. Restriction is an explicit caller
    // input; it is not inferred from a live item or player.
    internal static int RevisedPhysicalDefense(Candidate candidate,ushort requiredLevel,ushort playerLevel,
        bool revisionEnabled,Restriction restriction)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if(restriction is not (Restriction.Computed or Restriction.Original or Restriction.Zero))
            throw new ArgumentOutOfRangeException(nameof(restriction));
        int original=unchecked(candidate.RawPhysicalDefense+candidate.EnchantPhysicalDefense);
        if(!revisionEnabled)return original;
        int difference=requiredLevel-playerLevel;int revised=candidate.RawPhysicalDefense;
        if(difference>0)
        {
            difference=Math.Min(difference,49);
            float raw=RetailNpcUserMagicalInputs.FromInt(candidate.RawPhysicalDefense);
            float ratio=RetailNpcUserMagicalInputs.Div(RetailNpcUserMagicalInputs.FromInt(difference*2),100f);
            revised=RetailNpcUserMagicalInputs.Cvtt(RetailNpcUserMagicalInputs.Sub(raw,RetailNpcUserMagicalInputs.Mul(ratio,raw)));
        }
        return restriction switch {Restriction.Original=>original,Restriction.Zero=>0,
            _=>unchecked(revised+candidate.EnchantPhysicalDefense)};
    }

    private static string Text(JsonElement row,string key)
    {
        string? value=row.GetProperty(key).GetString();
        if(string.IsNullOrWhiteSpace(value)||value.Length>256||value.Any(c=>c>0x7f))throw new InvalidDataException("Invalid armor defense "+key);
        return value;
    }
}
