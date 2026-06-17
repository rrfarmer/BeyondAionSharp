using System.Xml.Serialization;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Model.Templates.World;

/// <summary>Java parity: model/templates/world/WorldMapTemplate.</summary>
[XmlRoot("map")]
[XmlType("WorldMapTemplate")]
public class WorldMapTemplate
{
    [XmlAttribute("name")]       public string Name   { get; set; } = "";
    [XmlAttribute("cName")]      public string CName  { get; set; } = "";
    [XmlAttribute("id")]         public int    MapId  { get; set; }
    [XmlAttribute("twin_count")] public int    TwinCount          { get; set; }
    [XmlAttribute("beginner_twin_count")] public int BeginnerTwinCount { get; set; }
    [XmlAttribute("max_user")]   public int    MaxUser            { get; set; }
    [XmlAttribute("prison")]     public bool   Prison             { get; set; }
    [XmlAttribute("instance")]   public bool   Instance           { get; set; }
    [XmlAttribute("death_level")] public int   DeathLevel         { get; set; }
    [XmlAttribute("water_level")] public int   WaterLevel         { get; set; } = 16;
    [XmlAttribute("world_type")] public WorldType WorldType       { get; set; } = WorldType.NONE;
    [XmlAttribute("world_size")] public int    WorldSize          { get; set; }
    [XmlAttribute("drop_type")]  public WorldDropType DropWorldType { get; set; } = WorldDropType.None;
    [XmlElement("ai_info")]      public AiInfo AiInfo             { get; set; } = AiInfo.Default;
    [XmlAttribute("except_buff")] public bool  ExceptBuff         { get; set; }
    [XmlAttribute("pve_attack_ratio")] public int PveAttackRatio  { get; set; }
    [XmlAttribute("pve_defend_ratio")] public int PveDefendRatio  { get; set; }

    // Java: @XmlList @XmlAttribute("flags") List<ZoneAttributes>
    // C# XmlSerializer cannot serialize List<T> as an XmlAttribute directly → string backing
    private List<ZoneAttributes>? _flagValues;
    private int _flags;

    [XmlAttribute("flags")]
    public string? FlagsRaw
    {
        get => _flagValues is { Count: > 0 } ? string.Join(" ", _flagValues) : null;
        set
        {
            if (value is null) { _flagValues = null; _flags = 0; return; }
            // Java parity: ZoneAttributes carries @XmlEnumValue wire names ("PVP","DUEL_SAME_RACE",
            // "NO_RETURN_BATTLE", ...) that differ from the C# member identifiers, so map the wire tokens
            // explicitly rather than Enum.Parse (which would throw on "PVP"/"DUEL_SAME_RACE"/...).
            _flagValues = value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                               .Select(ParseFlagToken)
                               .ToList();
            // Java afterUnmarshal: flags = ZoneAttributes.fromList(flagValues)
            _flags = ZoneAttributesExtensions.FromList(_flagValues);
        }
    }

    // Java parity: @XmlEnumValue wire-token → ZoneAttributes constant (matches WorldMapSummary.ParseFlags).
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
    public string GetName()           => Name;
    public string GetCName()          => CName;
    public int    GetMapId()          => MapId;
    public bool   IsPrison()          => Prison;
    public bool   IsInstance()        => Instance;
    public bool   IsInstanceType()    => Instance;
    public int    GetWaterLevel()     => WaterLevel;
    public int    GetDeathLevel()     => DeathLevel;
    public WorldType GetWorldType()   => WorldType;
    public int    GetWorldSize()      => WorldSize;
    public WorldDropType GetWorldDropType() => DropWorldType;
    public bool   IsExceptBuff()      => ExceptBuff;
    public AiInfo GetAiInfo()         => AiInfo;
    public int    GetFlags()          => _flags;
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

    // ── flag bit-checks ───────────────────────────────────────────────────────
    public bool IsFly()                    => (_flags & (int)ZoneAttributes.Fly)                  != 0;
    public bool CanGlide()                 => (_flags & (int)ZoneAttributes.Glide)                != 0;
    public bool CanPutKisk()               => (_flags & (int)ZoneAttributes.Bind)                 != 0;
    public bool CanRecall()                => (_flags & (int)ZoneAttributes.Recall)               != 0;
    public bool CanRide()                  => (_flags & (int)ZoneAttributes.Ride)                 != 0;
    public bool CanFlyRide()               => (_flags & (int)ZoneAttributes.FlyRide)              != 0;
    public bool IsPvpAllowed()             => (_flags & (int)ZoneAttributes.PvpEnabled)           != 0;
    public bool IsSameRaceDuelsAllowed()   => (_flags & (int)ZoneAttributes.DuelSameRaceEnabled)  != 0;
    public bool IsOtherRaceDuelsAllowed()  => (_flags & (int)ZoneAttributes.DuelOtherRaceEnabled) != 0;
    public bool CanReturnToBattle()        => (_flags & (int)ZoneAttributes.NoReturnBattle)       != 0;
}
