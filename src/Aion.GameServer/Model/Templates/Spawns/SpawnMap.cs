using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Spawns.Basespawns;
using Aion.GameServer.Model.Templates.Spawns.Mercenaries;
using Aion.GameServer.Model.Templates.Spawns.Panesterra;
using Aion.GameServer.Model.Templates.Spawns.Riftspawns;
using Aion.GameServer.Model.Templates.Spawns.Siegespawns;
using Aion.GameServer.Model.Templates.Spawns.Vortexspawns;

namespace Aion.GameServer.Model.Templates.Spawns;

/// <summary>Java parity: model/templates/spawns/SpawnMap.</summary>
[XmlType("SpawnMap")]
public class SpawnMap
{
    [XmlAttribute("map_id")]          public int MapId { get; set; }
    [XmlElement("spawn")]             public List<Spawn>?               Spawns          { get; set; }
    [XmlElement("base_spawn")]        public List<BaseSpawn>?           BaseSpawns      { get; set; }
    [XmlElement("rift_spawn")]        public List<RiftSpawn>?           RiftSpawns      { get; set; }
    [XmlElement("siege_spawn")]       public List<SiegeSpawn>?          SiegeSpawns     { get; set; }
    [XmlElement("mercenary_spawn")]   public List<MercenarySpawn>?      MercenarySpawns { get; set; }
    [XmlElement("vortex_spawn")]      public List<VortexSpawn>?         VortexSpawns    { get; set; }
    [XmlElement("ahserion_spawn")]    public List<AhserionsFlightSpawn>? AhserionSpawns { get; set; }

    public SpawnMap() { }

    public SpawnMap(int mapId)
    {
        MapId  = mapId;
        Spawns = [];
    }

    public int GetMapId() => MapId;
    public List<Spawn>                GetSpawns()          => Spawns          ?? [];
    public List<BaseSpawn>            GetBaseSpawns()      => BaseSpawns      ?? [];
    public List<MercenarySpawn>       GetMercenarySpawns() => MercenarySpawns ?? [];
    public List<RiftSpawn>            GetRiftSpawns()      => RiftSpawns      ?? [];
    public List<SiegeSpawn>           GetSiegeSpawns()     => SiegeSpawns     ?? [];
    public List<VortexSpawn>          GetVortexSpawns()    => VortexSpawns    ?? [];
    public List<AhserionsFlightSpawn> GetAhserionSpawns()  => AhserionSpawns  ?? [];
}
