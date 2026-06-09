using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Item.Actions;

/// <summary>Java parity: model/templates/item/actions/SummonHouseObjectAction.</summary>
[XmlType("SummonHouseObjectAction")]
public class SummonHouseObjectAction : AbstractItemAction
{
    [XmlAttribute("id")] private int objectId;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Player.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        // TODO Auto-generated method stub
        return false;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Player.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        // TODO Auto-generated method stub
    }

    public int GetTemplateId()
    {
        return objectId;
    }
}
