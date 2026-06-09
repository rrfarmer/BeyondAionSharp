using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Model.Templates.Item.Enums;
using Aion.GameServer.Model.Templates.Restriction;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/ItemData (Luno). @XmlRootElement(item_templates); afterUnmarshal→AfterUnmarshal(object). Map.compute→TryGetValue+init.</summary>
[XmlRoot("item_templates")]
public class ItemData
{
    [XmlElement("item_template")] private List<ItemTemplate> its;

    [XmlIgnore] private readonly Dictionary<int, ItemTemplate> items = new();
    [XmlIgnore] private readonly Dictionary<int, List<ItemTemplate>> manastones = new();
    [XmlIgnore] private readonly Dictionary<int, List<ItemTemplate>> ancientManastones = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (ItemTemplate it in its)
        {
            items[it.GetTemplateId()] = it;
            if (it.GetItemGroup() == ItemGroup.MANASTONE)
            {
                Add(manastones, it, it.GetLevel());
                AddStonesForHigherLevels(manastones, it, it.GetLevel());
            }
            else if (it.GetItemGroup() == ItemGroup.SPECIAL_MANASTONE)
            {
                if (!it.GetName().ToLower().Contains("pvp"))
                    Add(ancientManastones, it, it.GetLevel());
            }
        }
        its = null;
    }

    private void Add(Dictionary<int, List<ItemTemplate>> itemMap, ItemTemplate item, int manastoneLevel)
    {
        if (item.GetName().StartsWith("[")) // skip [Stamp], [Event], [Legion], [Legion reward], ...
            return;
        if (!itemMap.TryGetValue(manastoneLevel, out var list))
        {
            list = new List<ItemTemplate>();
            itemMap[manastoneLevel] = list;
        }
        list.Add(item);
    }

    private void AddStonesForHigherLevels(Dictionary<int, List<ItemTemplate>> itemMap, ItemTemplate item, int manastoneLevel)
    {
        if (manastoneLevel == 60 && item.GetTemplateId() == 167000563) // Manastone: Healing Boost +3
            Add(itemMap, item, 70);
        else if (manastoneLevel == 10 || manastoneLevel == 30 || manastoneLevel == 50)
        {
            if (item.GetItemQuality() == ItemQuality.COMMON || item.GetItemQuality() == ItemQuality.RARE)
            {
                if (item.GetName().Contains("Manastone: Attack +"))
                {
                    Add(itemMap, item, manastoneLevel + 10); // add 10 as 20 / 30 as 40 / 50 as 60
                    if (manastoneLevel == 50)
                        Add(itemMap, item, 70);
                }
            }
        }
    }

    public void Cleanup()
    {
        foreach (ItemCleanupTemplate ict in DataManager.ITEM_CLEAN_UP.GetList())
        {
            ItemTemplate template = GetItemTemplate(ict.GetId());
            ApplyCleanup(template, ict.ResultTrade(), ItemMask.TRADEABLE);
            ApplyCleanup(template, ict.ResultSell(), ItemMask.SELLABLE);
            ApplyCleanup(template, ict.ResultWH(), ItemMask.STORABLE_IN_WH);
            ApplyCleanup(template, ict.ResultAccountWH(), ItemMask.STORABLE_IN_AWH);
            ApplyCleanup(template, ict.ResultLegionWH(), ItemMask.STORABLE_IN_LWH);
        }
    }

    private void ApplyCleanup(ItemTemplate item, sbyte result, int mask)
    {
        switch (result)
        {
            case 1:
                item.ModifyMask(true, mask);
                break;
            case 0:
                item.ModifyMask(false, mask);
                break;
        }
    }

    public ItemTemplate GetItemTemplate(int itemId)
    {
        return items.TryGetValue(itemId, out var v) ? v : null;
    }

    public ICollection<ItemTemplate> GetItemTemplates()
    {
        return items.Values;
    }

    public int Size()
    {
        return items.Count;
    }

    public List<ItemTemplate> GetManastones(int level)
    {
        return manastones.TryGetValue(level, out var v) ? v : null;
    }

    public List<ItemTemplate> GetAncientManastones(int level)
    {
        return ancientManastones.TryGetValue(level, out var v) ? v : null;
    }
}
