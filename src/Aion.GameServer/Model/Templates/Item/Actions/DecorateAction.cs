using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/DecorateAction.</summary>
public class DecorateAction : AbstractItemAction
{
    [XmlAttribute("id")] private int? partId;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        // TODO Auto-generated method stub
        return false;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        // TODO Auto-generated method stub
    }

    public int GetTemplateId()
    {
        if (partId == null) // Addons missing in client
            return 0;
        return partId.Value;
    }
}
