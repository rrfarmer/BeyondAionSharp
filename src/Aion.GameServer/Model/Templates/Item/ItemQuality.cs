namespace Aion.GameServer.Model.Templates.Item;

/// <summary>
/// Item rarity/quality tier.
/// Java parity: model/templates/item/ItemQuality (@XmlType("quality") @XmlEnum).
/// </summary>
public enum ItemQuality
{
    JUNK = 0,   // Junk - Gray
    COMMON = 1, // Common - White
    RARE = 2,   // Superior - Green
    LEGEND = 3, // Heroic - Blue
    UNIQUE = 4, // Fabled - Yellow
    EPIC = 5,   // Eternal - Orange
    MYTHIC = 6, // Mythic - Purple
}

public static class ItemQualityExtensions
{
    // Java parity: getQualityId()
    public static int GetQualityId(this ItemQuality quality) => (int)quality;
}
