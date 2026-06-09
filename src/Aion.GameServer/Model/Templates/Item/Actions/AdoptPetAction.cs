using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Item.Actions;

/// <summary>Java parity: model/templates/item/actions/AdoptPetAction.</summary>
public class AdoptPetAction : AbstractItemAction
{
    [XmlAttribute("petId")] private int petId;
    [XmlAttribute("minutes")] private int expireMinutes;
    [XmlAttribute("sidekick")] private bool? isSideKick = false;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Player.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        return false;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Player.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
    }

    public int GetPetId()
    {
        return petId;
    }

    public int GetExpireMinutes()
    {
        return expireMinutes;
    }

    public bool? IsSideKick()
    {
        return isSideKick;
    }
}
