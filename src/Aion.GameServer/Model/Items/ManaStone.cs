using System.Collections.Generic;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Model.Items;

/// <summary>Java parity: model/items/ManaStone extends ItemStone.</summary>
public class ManaStone : ItemStone
{
    private List<Aion.GameServer.Model.Stats.Calc.Functions.StatFunction> modifiers;

    public ManaStone(int itemObjId, int itemId, int slot, Aion.GameServer.Model.GameObjects.IPersistable.PersistentState persistentState)
        : base(itemObjId, itemId, slot, persistentState)
    {
        Aion.GameServer.Model.Templates.Item.ItemTemplate stoneTemplate = DataManager.ITEM_DATA.GetItemTemplate(itemId);
        if (stoneTemplate != null && stoneTemplate.GetModifiers() != null)
        {
            this.modifiers = stoneTemplate.GetModifiers();
        }
    }

    /// <summary>modifiers</summary>
    public List<Aion.GameServer.Model.Stats.Calc.Functions.StatFunction> GetModifiers()
    {
        return modifiers;
    }

    public Aion.GameServer.Model.Stats.Calc.Functions.StatFunction GetFirstModifier()
    {
        return (modifiers != null && modifiers.Count > 0) ? modifiers[0] : null;
    }
}
