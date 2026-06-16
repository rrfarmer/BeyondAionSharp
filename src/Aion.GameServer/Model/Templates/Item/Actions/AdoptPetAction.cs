using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/AdoptPetAction.</summary>
public class AdoptPetAction : AbstractItemAction
{
    [XmlAttribute("petId")] public int petId;
    [XmlAttribute("minutes")] public int expireMinutes;
    // Java parity: @XmlAttribute("sidekick") Boolean (nullable). XmlSerializer cannot bind Nullable<T> as an
    // attribute, so round-trip via a string proxy (null when absent).
    [XmlIgnore] public bool? isSideKick = false;

    [XmlAttribute("sidekick")]
    public string SidekickRaw
    {
        get => isSideKick?.ToString().ToLowerInvariant();
        set => isSideKick = value == null ? (bool?)null : bool.Parse(value);
    }

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        return false;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
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
