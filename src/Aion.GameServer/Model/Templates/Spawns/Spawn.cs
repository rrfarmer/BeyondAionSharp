using System.Globalization;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Event;
using Aion.GameServer.SpawnEngine;

namespace Aion.GameServer.Model.Templates.Spawns;

/// <summary>Java parity: model/templates/spawns/Spawn.</summary>
[XmlType("Spawn")]
public class Spawn
{
    [XmlAttribute("npc_id")]      public int             NpcId        { get; set; }
    [XmlAttribute("difficult_id")] public byte           DifficultId  { get; set; }
    [XmlElement("temporary_spawn")] public TemporarySpawn? TemporarySpawn { get; set; }
    [XmlElement("spot")]            public List<SpawnSpotTemplate>? SpawnTemplates { get; set; }

    // Java parity: nullable Integer/Boolean/enum attributes. XmlSerializer cannot bind a Nullable<T>/Nullable<enum>
    // attribute, so the typed backing properties stay faithful ([XmlIgnore]) and public string proxies carry the
    // wire value (parse null/empty -> null, else the typed value — identical to JAXB's adapter result).
    [XmlIgnore] public int?              RespawnTime  { get; set; } = 0;
    [XmlIgnore] public int?              Pool         { get; set; } = 0;
    [XmlIgnore] public bool?             IsCustom     { get; set; } = false;
    [XmlIgnore] public SpawnHandlerType? Handler      { get; set; }

    [XmlAttribute("respawn_time")]
    public string? RespawnTimeRaw
    {
        get => RespawnTime?.ToString(CultureInfo.InvariantCulture);
        set => RespawnTime = string.IsNullOrEmpty(value) ? null : int.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlAttribute("pool")]
    public string? PoolRaw
    {
        get => Pool?.ToString(CultureInfo.InvariantCulture);
        set => Pool = string.IsNullOrEmpty(value) ? null : int.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlAttribute("custom")]
    public string? IsCustomRaw
    {
        get => IsCustom?.ToString(CultureInfo.InvariantCulture).ToLowerInvariant();
        set => IsCustom = string.IsNullOrEmpty(value) ? null : bool.Parse(value);
    }

    [XmlAttribute("handler")]
    public string? HandlerRaw
    {
        get => Handler?.ToString();
        set => Handler = string.IsNullOrEmpty(value) ? null : System.Enum.Parse<SpawnHandlerType>(value);
    }

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
    public bool ShouldSerializeRespawnTimeRaw()  => RespawnTime != 0;
    public bool ShouldSerializePoolRaw()         => Pool      != 0;
    public bool ShouldSerializeIsCustomRaw()     => IsCustom  == true;
    public bool ShouldSerializeHandlerRaw()      => Handler.HasValue;

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
