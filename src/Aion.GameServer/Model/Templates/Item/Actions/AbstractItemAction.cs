using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/AbstractItemAction.</summary>
[XmlType("AbstractItemAction")]
public abstract class AbstractItemAction
{
    /// <summary>
    /// Check if an item can be used. Returns true if act() can be called.
    /// </summary>
    public abstract bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params);

    /// <summary>Performs the item action.</summary>
    public abstract void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params);
}
