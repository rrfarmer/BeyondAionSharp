using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Item.Actions;

/// <summary>Java parity: model/templates/item/actions/RemodelAction.</summary>
public class RemodelAction : AbstractItemAction
{
    [XmlAttribute("type")] private int extractType;
    [XmlAttribute("minutes")] private int expireMinutes;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        return false;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
    }

    public int GetExpireMinutes()
    {
        return expireMinutes;
    }

    public int GetExtractType()
    {
        return extractType;
    }
}
