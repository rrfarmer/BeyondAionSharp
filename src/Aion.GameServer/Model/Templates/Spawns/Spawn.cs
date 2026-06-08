using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Event;
using Aion.GameServer.SpawnEngine;

namespace Aion.GameServer.Model.Templates.Spawns;

/// <summary>Java parity: model/templates/spawns/Spawn.</summary>
[XmlType("Spawn")]
public class Spawn
{
    [XmlAttribute("npc_id")]      public int             NpcId        { get; set; }
    [XmlAttribute("respawn_time")] public int?           RespawnTime  { get; set; } = 0;
    [XmlAttribute("pool")]         public int?           Pool         { get; set; } = 0;
    [XmlAttribute("difficult_id")] public byte           DifficultId  { get; set; }
    [XmlAttribute("custom")]       public bool?          IsCustom     { get; set; } = false;
    [XmlAttribute("handler")]      public SpawnHandlerType? Handler   { get; set; }
    [XmlElement("temporary_spawn")] public TemporarySpawn? TemporarySpawn { get; set; }
    [XmlElement("spot")]            public List<SpawnSpotTemplate>? SpawnTemplates { get; set; }

    // XmlTransient — set at runtime by event loading
    [XmlIgnore] public EventTemplate? EventTemplate { get; set; }

    public Spawn() { }

    public Spawn(int npcId, int respawnTime, SpawnHandlerType handler)
    {
        NpcId       = npcId;
        RespawnTime = respawnTime;
        Handler     = handler;
    }

    // Java beforeMarshal: omit default values when serializing
    public bool ShouldSerializePool()         => Pool      != 0;
    public bool ShouldSerializeIsCustom()     => IsCustom  == true;
    public bool ShouldSerializeRespawnTime()  => RespawnTime != 0;
    public bool ShouldSerializeHandler()      => Handler.HasValue;

    public int                     GetNpcId()              => NpcId;
    public int                     GetPool()               => Pool ?? 0;
    public TemporarySpawn?         GetTemporarySpawn()     => TemporarySpawn;
    public int                     GetRespawnTime()        => RespawnTime ?? 0;
    public SpawnHandlerType?       GetSpawnHandlerType()   => Handler;
    public byte                    GetDifficultId()        => DifficultId;
    public bool                    IsCustomSpawn()         => IsCustom == true;
    public void                    SetCustom(bool v)       => IsCustom = v;
    public bool                    IsEventSpawn()          => EventTemplate != null;
    public EventTemplate?          GetEventTemplate()      => EventTemplate;
    public void                    SetEventTemplate(EventTemplate? et) => EventTemplate = et;

    public List<SpawnSpotTemplate> GetSpawnSpotTemplates()
    {
        SpawnTemplates ??= [];
        return SpawnTemplates;
    }
}
