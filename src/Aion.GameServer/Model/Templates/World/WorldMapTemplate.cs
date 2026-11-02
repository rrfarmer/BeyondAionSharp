using System.Xml.Serialization;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Model.Templates.World;

/// <summary>Java parity: model/templates/world/WorldMapTemplate.</summary>
[XmlRoot("map")]
[XmlType("WorldMapTemplate")]
public class WorldMapTemplate : IL10n
{
    [XmlAttribute("id")]         public int    MapId  { get; set; }
    [XmlAttribute("cName")]      public string CName  { get; set; } = null!; // Java: required
    [XmlAttribute("name")]       public string? Name  { get; set; }
    [XmlAttribute("name_id")]    public int    NameId { get; set; }
    [XmlAttribute("twin_count")] public int    TwinCount          { get; set; }
    [XmlAttribute("beginner_twin_count")] public int BeginnerTwinCount { get; set; }
    [XmlAttribute("max_user")]   public int    MaxUser            { get; set; }
    [XmlAttribute("prison")]     public bool   Prison             { get; set; }
    [XmlAttribute("instance")]   public bool   Instance           { get; set; }
    [XmlAttribute("water_level")] public int   WaterLevel         { get; set; } = 16;
    [XmlAttribute("world_type")] public WorldType WorldType       { get; set; } = WorldType.NONE;
    [XmlAttribute("world_size")] public int    WorldSize          { get; set; }
    [XmlAttribute("drop_type")]  public WorldDropType DropWorldType { get; set; } = WorldDropType.None;
    [XmlElement("ai_info")]      public AiInfo AiInfo             { get; set; } = AiInfo.Default;
    [XmlAttribute("except_buff")] public bool  ExceptBuff         { get; set; }
    [XmlAttribute("pve_attack_ratio")] public int PveAttackRatio  { get; set; }
    [XmlAttribute("pve_defend_ratio")] public int PveDefendRatio  { get; set; }

    // Java EnumSet: membership uses the enum value, not its bit mask or ordinal.
    private HashSet<ZoneAttributes> zoneAttributes = new();

    [XmlAttribute("flags")]
    public string? FlagsRaw
    {
        get => zoneAttributes.Count == 0 ? null : string.Join(" ", zoneAttributes.OrderBy(a => (int)a).Select(a => a switch
        {
            ZoneAttributes.PvpEnabled => "PVP",
            ZoneAttributes.DuelSameRaceEnabled => "DUEL_SAME_RACE",
            ZoneAttributes.DuelOtherRaceEnabled => "DUEL_OTHER_RACE",
            ZoneAttributes.FlyRide => "FLY_RIDE",
            ZoneAttributes.NoReturnBattle => "NO_RETURN_BATTLE",
            _ => a.ToString().ToUpperInvariant()
        }));
        set => zoneAttributes = value == null ? new() : value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseFlagToken).ToHashSet();
    }

    // Java parity: @XmlEnumValue wire-token → ZoneAttributes constant.
    private static ZoneAttributes ParseFlagToken(string token) => token switch
    {
        "BIND"             => ZoneAttributes.Bind,
        "RECALL"           => ZoneAttributes.Recall,
        "GLIDE"            => ZoneAttributes.Glide,
        "FLY"              => ZoneAttributes.Fly,
        "RIDE"             => ZoneAttributes.Ride,
        "FLY_RIDE"         => ZoneAttributes.FlyRide,
        "PVP"              => ZoneAttributes.PvpEnabled,
        "DUEL_SAME_RACE"   => ZoneAttributes.DuelSameRaceEnabled,
        "DUEL_OTHER_RACE"  => ZoneAttributes.DuelOtherRaceEnabled,
        "NO_RETURN_BATTLE" => ZoneAttributes.NoReturnBattle,
        _                  => Enum.Parse<ZoneAttributes>(token, ignoreCase: true),
    };

    // ── getters ──────────────────────────────────────────────────────────────
    public string GetCName()          => CName;
    // Java parity: getName() falls back to the client name when no display name is set.
    public string GetName()           => Name ?? CName;
    // Java parity: L10n::getL10nId()
    public int    GetL10nId()         => NameId;
    public int    GetMapId()          => MapId;
    public bool   IsPrison()          => Prison;
    public bool   IsInstance()        => Instance;
    public bool   IsInstanceType()    => Instance;
    public int    GetWaterLevel()     => WaterLevel;
    public WorldType GetWorldType()   => WorldType;
    public int    GetWorldSize()      => WorldSize;
    public WorldDropType GetWorldDropType() => DropWorldType;
    public bool   IsExceptBuff()      => ExceptBuff;
    public AiInfo GetAiInfo()         => AiInfo;
    public bool HasAttribute(ZoneAttributes attribute) => zoneAttributes.Contains(attribute);
    public int    GetMaxUser()        => MaxUser;
    public int    GetPvEAttackRatio() => PveAttackRatio;
    public int    GetPvEDefendRatio() => PveDefendRatio;

    // Java parity: getTwinCount() — clamps by WorldConfig.WORLD_MAX_TWINS_USUAL
    public int GetTwinCount()
    {
        if (Configs.Main.WorldConfig.WorldMaxTwinsUsual == 0)
            return TwinCount;
        return System.Math.Min(Configs.Main.WorldConfig.WorldMaxTwinsUsual, TwinCount);
    }

    // Java parity: getBeginnerTwinCount() — clamps by WorldConfig.WORLD_MAX_TWINS_BEGINNER
    public int GetBeginnerTwinCount()
    {
        if (Configs.Main.WorldConfig.WorldMaxTwinsBeginner == 0)
            return BeginnerTwinCount;
        else if (Configs.Main.WorldConfig.WorldMaxTwinsBeginner == -1) // disabled
            return 0;
        return System.Math.Min(Configs.Main.WorldConfig.WorldMaxTwinsBeginner, BeginnerTwinCount);
    }

}
