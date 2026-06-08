using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Event;

/// <summary>
/// Periodic inventory item drop for an event (given to players on a timer).
/// Java parity: model/templates/event/InventoryDrop.
/// </summary>
[XmlType("InventoryDrop")]
public class InventoryDrop
{
    [XmlAttribute("item_id")]   public int ItemId     { get; set; }
    [XmlAttribute("startlevel")] public int StartLevel { get; set; }
    [XmlAttribute("interval")]  public int Interval   { get; set; }
    [XmlAttribute("count")]     public int Count      { get; set; } = 1;

    public int GetItemId()     => ItemId;
    public int GetStartLevel() => StartLevel;
    public int GetInterval()   => Interval;
    public int GetCount()      => Count;
}
