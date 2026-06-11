using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Items.Bonuses;

/// <summary>Java parity: model/templates/item/bonuses/StatBonusType (Rolandas).</summary>
[XmlType("StatBonusType")]
public enum StatBonusType
{
    INVENTORY,
    POLISH
}
