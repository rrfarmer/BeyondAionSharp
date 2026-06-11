namespace Aion.GameServer.Model.Templates.Items;

/// <summary>
/// Broad item category (normal vs abyss/draconic/etc.).
/// Java parity: model/templates/item/ItemType (@XmlType("item_type") @XmlEnum).
/// </summary>
public enum ItemType
{
    NORMAL,
    ABYSS,
    DRACONIC,
    DEVANION,
    LEGEND,
}
