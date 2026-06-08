using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Spawns.Mercenaries;

/// <summary>Java parity: model/templates/spawns/mercenaries/MercenaryZone.</summary>
[XmlType("MercenaryZone")]
public class MercenaryZone
{
    [XmlAttribute("zone")]        public int Zone       { get; set; }
    [XmlAttribute("costs")]       public int Costs      { get; set; }
    [XmlAttribute("cooldown")]    public int Cooldown   { get; set; }
    [XmlAttribute("msg_id")]      public int MsgId      { get; set; }
    [XmlAttribute("announce_id")] public int AnnounceId { get; set; }
    [XmlElement("spawn")]         public List<Spawn>? Spawns { get; set; }

    // XmlTransient fields — runtime-injected, not deserialized
    [XmlIgnore] public int WorldId  { get; set; }
    [XmlIgnore] public int SiegeId  { get; set; }

    public int GetZoneId()     => Zone;
    public int GetCosts()      => Costs;
    public int GetCooldown()   => Cooldown;
    public int GetMsgId()      => MsgId;
    public int GetAnnounceId() => AnnounceId;
    public List<Spawn>? GetSpawns() => Spawns;
    public int GetWorldId()    => WorldId;
    public void SetWorldId(int worldId) => WorldId = worldId;
    public int GetSiegeId()    => SiegeId;
    public void SetSiegeId(int siegeId) => SiegeId = siegeId;
}
