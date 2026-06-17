using Aion.Commons.Configuration;

namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/FallDamageConfig. [Property] keys/defaults bound at boot from config/*.properties.</summary>
public static class FallDamageConfig
{
    /// <summary>Percentage of damage per meter. Property key: gameserver.falldamage.percentage</summary>
    [Property(key: "gameserver.falldamage.percentage", defaultValue: "1.0")]
    public static float FALL_DAMAGE_PERCENTAGE = 1.0f;

    /// <summary>Minimum fall damage range. Property key: gameserver.falldamage.distance.minimum</summary>
    [Property(key: "gameserver.falldamage.distance.minimum", defaultValue: "10")]
    public static int MINIMUM_DISTANCE_DAMAGE = 10;

    /// <summary>Maximum fall distance after which you will die after hitting the ground. Property key: gameserver.falldamage.distance.maximum</summary>
    [Property(key: "gameserver.falldamage.distance.maximum", defaultValue: "50")]
    public static int MAXIMUM_DISTANCE_DAMAGE = 50;

    /// <summary>Maximum fall distance after which you will die in mid air. Property key: gameserver.falldamage.distance.midair</summary>
    [Property(key: "gameserver.falldamage.distance.midair", defaultValue: "200")]
    public static int MAXIMUM_DISTANCE_MIDAIR = 200;
}
