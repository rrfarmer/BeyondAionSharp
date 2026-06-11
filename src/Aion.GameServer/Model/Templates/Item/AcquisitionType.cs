namespace Aion.GameServer.Model.Templates.Items;

/// <summary>
/// How an item is acquired (AP/abyss/reward/coupon).
/// Java parity: model/templates/item/AcquisitionType (@XmlType("acquisitionType") @XmlEnum).
/// </summary>
public enum AcquisitionType
{
    AP = 0,
    ABYSS = 1,
    REWARD = 2, // They are the same now
    COUPON = 2,
}

public static class AcquisitionTypeExtensions
{
    // Java parity: getId()
    public static int GetId(this AcquisitionType type) => (int)type;
}
