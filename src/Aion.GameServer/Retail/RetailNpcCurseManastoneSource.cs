using System.Collections.Frozen;
using System.Text.Json;
using Aion.GameServer.Dataholders.RetailNpc;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Stats.Calc.Functions;

namespace Aion.GameServer.Retail;

/// <summary>Source-matched ordinary 4.8 manastones with reviewed flat retail
/// resource and offensive attributes. Other attributes remain unsupported.</summary>
internal sealed class RetailNpcCurseManastoneSource
{
    private sealed record Row(int Id,string Name,FrozenDictionary<StatEnum,int> Attributes);
    private static readonly FrozenSet<StatEnum> Reviewed= new[] {StatEnum.MAXHP,StatEnum.MAXMP,
        StatEnum.PHYSICAL_ATTACK,StatEnum.PHYSICAL_CRITICAL,StatEnum.BOOST_MAGICAL_SKILL,
        StatEnum.MAGICAL_ACCURACY,StatEnum.PHYSICAL_ACCURACY,StatEnum.HEAL_BOOST,
        StatEnum.MAGICAL_CRITICAL}.ToFrozenSet();
    private readonly FrozenDictionary<int,Row> items;
    private RetailNpcCurseManastoneSource(Dictionary<int,Row> rows)=>items=rows.ToFrozenDictionary();
    internal int Count=>items.Count;

    internal static RetailNpcCurseManastoneSource Parse(byte[] bytes,string hash)
    {
        RetailNpcStatCatalog.CheckHash(bytes,hash,"curse manastone source");
        using var doc=JsonDocument.Parse(bytes,new JsonDocumentOptions {MaxDepth=8});
        var root=doc.RootElement;RetailNpcDatasetValidation.UniqueJson(root);
        var p=root.GetProperty("provenance");
        int version=root.GetProperty("version").GetInt32();
        if(version is not (1 or 2)||root.GetProperty("purpose").GetString()!=$"curse-manastone-resource-v{version}"
            ||root.GetProperty("region").GetString()!="USA"
            ||p.GetProperty("retail_item_etc_sha256").GetString()!="f09736ff6d50b3c778892cb39a6de30a53c5bcce4e191d9c5a5a4a3045fbc359"
            ||p.GetProperty("host_items_sha256").GetString()!=RetailNpcCurseArmorSource.ReviewedHostItemsSha
            ||p.GetProperty("retail_selection").GetString()!="USA missing; base fallback")
            throw new InvalidDataException("Unreviewed manastone source identity");
        var rows=new Dictionary<int,Row>();
        foreach(var element in root.GetProperty("items").EnumerateArray())
        {
            int id=element.GetProperty("id").GetInt32();
            string? name=element.GetProperty("name").GetString();
            var attributes=new Dictionary<StatEnum,int>();
            if(version==1)
            {
                int hp=element.GetProperty("hp").GetInt32(),mp=element.GetProperty("mp").GetInt32();
                if(hp is <0 or >short.MaxValue||mp is <0 or >short.MaxValue||(hp==0&&mp==0))
                    throw new InvalidDataException("Invalid manastone resources");
                if(hp!=0)attributes.Add(StatEnum.MAXHP,hp);
                if(mp!=0)attributes.Add(StatEnum.MAXMP,mp);
            }
            else foreach(var attribute in element.GetProperty("attributes").EnumerateArray())
            {
                string? stat=attribute.GetProperty("stat").GetString();int value=attribute.GetProperty("value").GetInt32();
                if(stat is null||!Enum.TryParse<StatEnum>(stat,false,out var selected)||Enum.GetName(selected)!=stat||!Reviewed.Contains(selected)
                    ||value is <=0 or >short.MaxValue||!attributes.TryAdd(selected,value))
                    throw new InvalidDataException("Unreviewed manastone attribute");
            }
            if(id is <167000000 or >=168000000||string.IsNullOrEmpty(name)||name.Any(c=>c>0x7f)
                ||attributes.Count==0||!rows.TryAdd(id,new(id,name,attributes.ToFrozenDictionary())))
                throw new InvalidDataException("Invalid manastone row");
        }
        if(rows.Count is <1 or >10000)throw new InvalidDataException("Invalid manastone count");
        return new(rows);
    }

    internal bool Matches(ManaStone stone,int itemObjectId)
    {
        if(stone.GetItemObjId()!=itemObjectId||stone.GetSlot() is <0 or >=6
            ||!items.TryGetValue(stone.GetItemId(),out var row))return false;
        var template=stone.GetItemTemplate();
        if(template.GetTemplateId()!=row.Id||template.GetItemGroup().ToString()!="MANASTONE"
            ||!string.Equals(template.GetRetailSourceName(),row.Name,StringComparison.OrdinalIgnoreCase))return false;
        var functions=stone.GetModifiers();
        if(functions is null)return false;
        var seen=new HashSet<StatEnum>();
        foreach(var function in functions)
        {
            if(function.GetType()!=typeof(StatAddFunction)||!function.IsBonus()||function.HasConditions()
                ||!seen.Add(function.GetName()))return false;
            if(!row.Attributes.TryGetValue(function.GetName(),out int expected)||expected!=function.GetValue())return false;
        }
        return seen.Count==row.Attributes.Count;
    }
}
