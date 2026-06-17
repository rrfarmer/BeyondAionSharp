using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>
/// Java parity: configs/main/GeoDataConfig. SCREAMING_SNAKE field names + [Property] keys/defaults bound at boot by
/// <see cref="ConfigurableProcessor"/> from the gameserver.geodata.* keys in config/*.properties.
/// </summary>
public static class GeoDataConfig
{
    /// <summary>Geodata enable. Property key: gameserver.geodata.enable (default true).</summary>
    [Property(key: "gameserver.geodata.enable", defaultValue: "true")]
    public static bool GEO_ENABLE = true;

    /// <summary>Enable canSee checks using geodata. Property key: gameserver.geodata.cansee.enable (default true).</summary>
    [Property(key: "gameserver.geodata.cansee.enable", defaultValue: "true")]
    public static bool CANSEE_ENABLE = true;

    /// <summary>Enable Fear skill using geodata. Property key: gameserver.geodata.fear.enable (default true).</summary>
    [Property(key: "gameserver.geodata.fear.enable", defaultValue: "true")]
    public static bool FEAR_ENABLE = true;

    /// <summary>Enable Geo checks during npc movement (prevent flying mobs). Property key: gameserver.geodata.npc.move (default true).</summary>
    [Property(key: "gameserver.geodata.npc.move", defaultValue: "true")]
    public static bool GEO_NPC_MOVE = true;

    /// <summary>Enable geo materials using skills. Property key: gameserver.geodata.materials.enable (default true).</summary>
    [Property(key: "gameserver.geodata.materials.enable", defaultValue: "true")]
    public static bool GEO_MATERIALS_ENABLE = true;

    /// <summary>Show collision zone name and skill id. Property key: gameserver.geodata.materials.showdetails (default false).</summary>
    [Property(key: "gameserver.geodata.materials.showdetails", defaultValue: "false")]
    public static bool GEO_MATERIALS_SHOWDETAILS = false;

    /// <summary>Enable geo shields. Property key: gameserver.geodata.shields.enable (default true).</summary>
    [Property(key: "gameserver.geodata.shields.enable", defaultValue: "true")]
    public static bool GEO_SHIELDS_ENABLE = true;
}
