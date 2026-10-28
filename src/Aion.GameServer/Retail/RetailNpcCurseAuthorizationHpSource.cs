using System.Collections.Frozen;
using System.Text.Json;
using Aion.GameServer.Dataholders.RetailNpc;

namespace Aion.GameServer.Retail;

/// <summary>Offline-only, source-pinned authorization HP component for the
/// reviewed ordinary-server equipment identities. It is not complete player
/// HP and must not authorize the curse or change player equipment behavior.</summary>
internal sealed class RetailNpcCurseAuthorizationHpSource
{
    internal const string ReviewedSourceSha="9f42bc42da798233843fedfbeb6ca50eb3c03a14639b7dd1100f21e6c7bbea7c";
    internal const string ReviewedExpandedSourceSha="ed518ff28c1af257f6f77e86691a28e7dac908db6a54f5e24df68a717f9ff57e";
    internal enum Failure { None, SourceUnavailable, UnknownItem, IdentityMismatch, UnreviewedLevel, MissingTable, MissingLevel }
    internal sealed record Level(int Hp,bool Explicit);
    internal sealed record Table(int Threshold,IReadOnlyDictionary<int,Level> Levels,int LimitlessHp,bool LimitlessExplicit)
    {
        internal bool TryHp(int level,out int hp)
        {
            hp=0;
            if(level is <0 or >255||Threshold is <1 or >256)return false;
            if(level==0)return true;
            int baseLevel=level<Threshold?level:Threshold-1;
            if(baseLevel!=0)
            {
                if(!Levels.TryGetValue(baseLevel,out var row))return false;
                hp=row.Hp;
            }
            if(level>=Threshold)hp=unchecked(hp+unchecked((level-Threshold+1)*LimitlessHp));
            return true;
        }
    }
    private sealed record Row(int Id,string Name,string Group,string Table,string Kind);
    private readonly FrozenDictionary<int,Row> items;
    private readonly FrozenDictionary<string,Table> tables;
    private RetailNpcCurseAuthorizationHpSource(Dictionary<int,Row> items,Dictionary<string,Table> tables)
    {this.items=items.ToFrozenDictionary();this.tables=tables.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);}
    internal int ItemCount=>items.Count;
    internal int TableCount=>tables.Count;

    internal static RetailNpcCurseAuthorizationHpSource Parse(byte[] bytes,string sha)
    {
        bool expanded=sha==ReviewedExpandedSourceSha;
        if(sha!=ReviewedSourceSha&&!expanded)throw new InvalidDataException("Unreviewed authorization HP source identity");
        RetailNpcStatCatalog.CheckHash(bytes,sha,"curse authorization HP source");
        using var doc=JsonDocument.Parse(bytes,new JsonDocumentOptions{MaxDepth=16});
        var root=doc.RootElement;RetailNpcDatasetValidation.UniqueJson(root);
        if(root.GetProperty("version").GetInt32()!=(expanded?3:2)||root.GetProperty("purpose").GetString()!=(expanded?"retail-authorization-hp-bounded-v3":"retail-authorization-hp-bounded-v2")
            ||root.GetProperty("special_server_type").GetInt32()!=0)
            throw new InvalidDataException("Unsupported authorization HP source");
        if(root.GetProperty("missing_tables").GetArrayLength()!=0)throw new InvalidDataException("Missing authorization HP tables");
        var tables=new Dictionary<string,Table>(StringComparer.OrdinalIgnoreCase);
        foreach(var prop in root.GetProperty("tables").EnumerateObject())
        {
            var v=prop.Value;int threshold=v.GetProperty("threshold").GetInt32();
            if(threshold is <1 or >256||string.IsNullOrEmpty(prop.Name)||prop.Name.Any(c=>c>0x7f))
                throw new InvalidDataException("Invalid authorization table identity");
            var levels=new Dictionary<int,Level>();
            foreach(var entry in v.GetProperty("levels").EnumerateObject())
            {
                if(!int.TryParse(entry.Name,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int n)
                    ||n<1||n>=threshold||n>255||!levels.TryAdd(n,new(entry.Value.GetProperty("hp").GetInt32(),entry.Value.GetProperty("explicit").GetBoolean())))
                    throw new InvalidDataException("Invalid authorization table level");
            }
            if(!tables.TryAdd(prop.Name,new(threshold,levels.ToFrozenDictionary(),v.GetProperty("limitless_hp_per_level").GetInt32(),
                v.GetProperty("limitless_hp_explicit").GetBoolean())))throw new InvalidDataException("Duplicate authorization table");
        }
        var items=new Dictionary<int,Row>();
        foreach(var item in root.GetProperty("items").EnumerateArray())
        {
            int id=item.GetProperty("id").GetInt32();string? name=item.GetProperty("name").GetString();
            string? group=item.GetProperty("host_group").GetString(),table=item.GetProperty("table").GetString();
            string? kind=expanded?item.GetProperty("kind").GetString():"other";
            if(id<=0||string.IsNullOrEmpty(name)||name.Any(c=>c>0x7f)||string.IsNullOrEmpty(group)
                ||string.IsNullOrEmpty(table)||kind is not ("other" or "armor" or "weapon")
                ||!tables.ContainsKey(table)||!items.TryAdd(id,new(id,name,group,table,kind)))
                throw new InvalidDataException("Invalid authorization equipment identity");
        }
        if(items.Count!=(expanded?54153:11249)||tables.Count!=(expanded?56:23))throw new InvalidDataException("Authorization source coverage changed");
        return new(items,tables);
    }

    internal Failure Lookup(RetailNpcCurseOtherEquipmentHpSource.Candidate candidate,int level,out int hp)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return Lookup(candidate.ItemId,candidate.Name,candidate.HostGroup,"other",level,out hp);
    }
    internal Failure Lookup(RetailNpcCurseWeaponHpSource.Candidate candidate,int level,out int hp)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return Lookup(candidate.ItemId,candidate.Name,candidate.HostGroup,"weapon",level,out hp);
    }
    internal Failure Lookup(RetailNpcCurseArmorSource.Candidate candidate,int level,out int hp)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        string group=candidate.Table.ToUpperInvariant();
        if(group.EndsWith("_GLOVES",StringComparison.Ordinal))group=group[..^1];
        return Lookup(candidate.ItemId,candidate.Name,group,"armor",level,out hp);
    }
    private Failure Lookup(int id,string name,string group,string kind,int level,out int hp)
    {
        hp=0;
        if(level is <0 or >255)return Failure.UnreviewedLevel;
        if(!items.TryGetValue(id,out var row))return Failure.UnknownItem;
        if(!string.Equals(name,row.Name,StringComparison.OrdinalIgnoreCase)||group!=row.Group||kind!=row.Kind)
            return Failure.IdentityMismatch;
        if(!tables.TryGetValue(row.Table,out var table))return Failure.MissingTable;
        return table.TryHp(level,out hp)?Failure.None:Failure.MissingLevel;
    }
}
