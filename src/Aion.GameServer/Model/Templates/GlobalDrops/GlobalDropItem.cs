using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>
/// A single item entry in a global drop rule.
/// Java parity: model/templates/globaldrops/GlobalDropItem.
/// </summary>
/// <remarks>
/// Java's afterUnmarshal() validates that itemId exists via DataManager.ITEM_DATA
/// and normalises maxCount. C# XmlSerializer has no afterUnmarshal callback;
/// the equivalent Validate() method is called by the data-loading pipeline after
/// deserialization. The validation is init-time upward behaviour (calls DataManager)
/// — recorded as backlog to wire when DataManager exposes ITEM_DATA faithfully.
///
/// TODO-backlog: call Validate() from the data-loading pipeline (StaticDataListener
/// equivalent) once DataManager.ITEM_DATA is ported. Java: afterUnmarshal validates
/// itemId exists and normalises maxCount.
/// </remarks>
[XmlType("GlobalDropItem")]
public class GlobalDropItem : Aion.GameServer.Model.IChance
{
    // Java parity: @XmlAttribute(name="id") int itemId
    [XmlAttribute("id")] public int ItemId { get; set; }

    // Java parity: @XmlAttribute(name="min_count") int minCount = 1
    [XmlAttribute("min_count")] public int MinCount { get; set; } = 1;

    // Java parity: @XmlAttribute(name="max_count") int maxCount
    [XmlAttribute("max_count")] public int MaxCount { get; set; }

    // Java parity: @XmlAttribute(name="chance") float chance = 100f
    [XmlAttribute("chance")] public float Chance { get; set; } = 100f;

    public int GetId() => ItemId;
    public int GetMinCount() => MinCount;
    public int GetMaxCount() => MaxCount == 0 ? MinCount : MaxCount; // normalised like Java afterUnmarshal
    public float GetChance() => Chance;
}
