using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;
using Aion.GameServer.Dataholders.RetailNpc;

namespace Aion.GameServer.Retail;

/// <summary>Reviewed retail raw HP and default-enchant inputs. A lookup does
/// not establish that an item is equipped, eligible, or correctly revised for
/// a player, and must not be used as a complete curse base by itself.</summary>
internal sealed class RetailNpcCurseArmorSource
{
    internal const string ReviewedBindingsSha="e89f9ae26361f86ec109e326e543bd9028664e50f66fefb106b3e1b43bae7caa";
    internal const string ReviewedRetailItemsSha="11167024df7400bf6ff051f84bcf6ef1dffdec1949b33ff409f42e39e3e9716a";
    internal const string ReviewedRetailEnchantsSha="1938ed5e4be74f2ba4fd8bac6c7f25e232987090bab27c3684724b02f39a6850";
    internal const string ReviewedHostItemsSha="dae844fb7b6837b0d898105e2466cb3fac557a6b6a4ada6c6d6e9f177745ce47";
    internal enum Failure { None, UnknownItem, IdentityMismatch, UnreviewedEnchantLevel }
    internal sealed record Candidate(int ItemId,string Name,int RawMaxHp,bool MaxHpExplicit,
        int EnchantMaxHp,int HostMaxEnchant,int? RetailMaxEnchant,string Table,
        ImmutableArray<byte> ClassPermits,int ClassPresentMask)
    {
        internal bool EquipmentEligibilityVerified=>false;
        internal bool ItemRevisionApplied=>false;
        internal bool TryGetClassPermit(int nativeClass,out byte level,out bool sourcePresent)
        {
            level=0;sourcePresent=false;
            if((uint)nativeClass>=17 || ClassPermits.IsDefaultOrEmpty || ClassPermits.Length!=17)return false;
            level=ClassPermits[nativeClass];sourcePresent=(ClassPresentMask&(1<<nativeClass))!=0;return true;
        }
    }
    private sealed record Item(int Id,string Name,string Table,int RawMaxHp,bool MaxHpExplicit,
        int HostMaxEnchant,int? RetailMaxEnchant,ImmutableArray<byte> ClassPermits,int ClassPresentMask);
    private readonly FrozenDictionary<int,Item> items;
    private readonly FrozenDictionary<string,int[]> tables;
    internal int ItemCount=>items.Count;
    internal int TableCount=>tables.Count;

    private RetailNpcCurseArmorSource(Dictionary<int,Item> items,Dictionary<string,int[]> tables)
    { this.items=items.ToFrozenDictionary();this.tables=tables.ToFrozenDictionary(StringComparer.Ordinal); }

    internal static RetailNpcCurseArmorSource Parse(byte[] bytes,string sha)
    {
        RetailNpcStatCatalog.CheckHash(bytes,sha,"curse armor source");
        using var doc=JsonDocument.Parse(bytes,new JsonDocumentOptions {MaxDepth=16});
        var root=doc.RootElement;RetailNpcDatasetValidation.UniqueJson(root);
        if(root.GetProperty("version").GetInt32()!=2 || root.GetProperty("purpose").GetString()!="default-armor-hp-and-class-v2"
            ||root.GetProperty("region").GetString()!="USA")throw new InvalidDataException("Unsupported curse armor source identity");
        var provenance=root.GetProperty("provenance");
        foreach(var (key,expected) in new[]{("bindings_sha256",ReviewedBindingsSha),("retail_items_sha256",ReviewedRetailItemsSha),
            ("retail_enchant_sha256",ReviewedRetailEnchantsSha),("host_items_sha256",ReviewedHostItemsSha)})
        {
            string? hash=provenance.GetProperty(key).GetString();
            if(hash!=expected)
                throw new InvalidDataException("Invalid curse armor provenance: "+key);
        }
        foreach(string key in new[]{"retail_items_selection","retail_enchant_selection"})
            if(provenance.GetProperty(key).GetString() is not ("USA override" or "USA missing; base fallback"))
                throw new InvalidDataException("Unreviewed curse armor region selection");
        var tables=new Dictionary<string,int[]>(StringComparer.Ordinal);
        foreach(var row in root.GetProperty("tables").EnumerateArray())
        {
            string name=Text(row,"name");int threshold=row.GetProperty("threshold").GetInt32();
            if(threshold is <=15 or >255)throw new InvalidDataException("Unreviewed curse armor enchant threshold");
            var levels=row.GetProperty("hp_levels_1_to_15").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            if(levels.Length!=15||!tables.TryAdd(name,levels))throw new InvalidDataException("Invalid or duplicate curse armor table");
        }
        if(tables.Count is <1 or >20)throw new InvalidDataException("Invalid curse armor table count");
        var items=new Dictionary<int,Item>();
        foreach(var row in root.GetProperty("items").EnumerateArray())
        {
            int id=row.GetProperty("id").GetInt32();string name=Text(row,"name"),table=Text(row,"table");
            int hp=row.GetProperty("raw_max_hp").GetInt32(),host=row.GetProperty("host_max_enchant").GetInt32();
            bool explicitHp=row.GetProperty("max_hp_explicit").GetBoolean();var retail=row.GetProperty("retail_max_enchant");
            int? cap=retail.ValueKind==JsonValueKind.Null?null:retail.GetInt32();
            var permits=row.GetProperty("class_permits").EnumerateArray().Select(x=>x.GetByte()).ToImmutableArray();
            int present=row.GetProperty("class_present_mask").GetInt32();
            if(id<=0||!tables.ContainsKey(table)||host is <0 or >15||cap is <0 or >255
                ||permits.Length!=17||permits.Any(value=>value>=100)||present is <0 or >=(1<<17)
                ||permits.Where((value,index)=>(present&(1<<index))==0&&value!=0).Any()
                ||!explicitHp&&hp!=0||!items.TryAdd(id,new(id,name,table,hp,explicitHp,host,cap,permits,present)))
                throw new InvalidDataException("Invalid or duplicate curse armor item");
        }
        if(items.Count is <1 or >100000)throw new InvalidDataException("Invalid curse armor item count");
        return new(items,tables);
    }

    internal Failure Lookup(int itemId,string? hostName,int enchantLevel,out Candidate? candidate)
    {
        candidate=null;
        if(!items.TryGetValue(itemId,out var item))return Failure.UnknownItem;
        if(hostName is null||hostName.Any(c=>c>0x7f)||!string.Equals(item.Name,hostName,StringComparison.OrdinalIgnoreCase))
            return Failure.IdentityMismatch;
        if(enchantLevel is <0 or >15)return Failure.UnreviewedEnchantLevel;
        int enchant=enchantLevel==0?0:tables[item.Table][enchantLevel-1];
        candidate=new(item.Id,item.Name,item.RawMaxHp,item.MaxHpExplicit,enchant,item.HostMaxEnchant,item.RetailMaxEnchant,
            item.Table,item.ClassPermits,item.ClassPresentMask);
        return Failure.None;
    }

    /// <summary>Selector-5 revision with explicit native-stage inputs. The
    /// supplied requirement and flags must be established by the caller; this
    /// arithmetic does not establish that the item is eligible or equipped.</summary>
    internal static int RevisedHp(Candidate item,int requiredLevel,int playerLevel,bool revisionEnabled,bool restrictedBypass)
    {
        ArgumentNullException.ThrowIfNull(item);
        if(requiredLevel is <0 or >ushort.MaxValue||playerLevel is <0 or >ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(requiredLevel));
        if(item.RawMaxHp<0||item.EnchantMaxHp<0)throw new ArgumentOutOfRangeException(nameof(item));
        int original=unchecked(item.RawMaxHp+item.EnchantMaxHp);
        if(!revisionEnabled||restrictedBypass)return original;
        int difference=requiredLevel-playerLevel;
        if(difference<=0)return original;
        difference=Math.Min(difference,49);
        float raw=Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs.FromInt(item.RawMaxHp);
        float ratio=Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs.Div(
            Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs.FromInt(difference*2),100f);
        float reduction=Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs.Mul(ratio,raw);
        int revised=Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs.Cvtt(
            Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs.Sub(raw,reduction));
        return unchecked(revised+item.EnchantMaxHp);
    }

    /// <summary>Add the separately verified authorization contribution to the
    /// selector-5 enchant field. The caller must still establish item eligibility,
    /// revision flags and the remaining player HP/effect inputs.</summary>
    internal static int RevisedHpWithAuthorization(Candidate item,int authorizationHp,int requiredLevel,int playerLevel,
        bool revisionEnabled,bool restrictedBypass)
        =>unchecked(RevisedHp(item,requiredLevel,playerLevel,revisionEnabled,restrictedBypass)+authorizationHp);

    /// <summary>Native Item::AdjustClassPermit with an explicit item reduction
    /// byte and configured floor. It does not verify equipment eligibility.</summary>
    internal static int AdjustClassPermit(byte permit,sbyte levelAdjustment,ushort floor)
    {
        if(permit==0)return 0;
        if(levelAdjustment==0)return permit;
        int result=permit;
        if(result>floor)result-=levelAdjustment;
        return Math.Max(result,floor);
    }

    private static string Text(JsonElement row,string key)
    {
        string? value=row.GetProperty(key).GetString();
        if(string.IsNullOrWhiteSpace(value)||value.Length>256||value.Any(c=>c>0x7f))
            throw new InvalidDataException("Invalid curse armor "+key);
        return value;
    }
}
