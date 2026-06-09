namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/FallDamageConfig.</summary>
public static class FallDamageConfig
{
    /// <summary>Property key: gameserver.falldamage.percentage</summary>
    public static float FALL_DAMAGE_PERCENTAGE = 1.0f;

    /// <summary>Property key: gameserver.falldamage.distance.minimum</summary>
    public static int MINIMUM_DISTANCE_DAMAGE = 10;

    /// <summary>Property key: gameserver.falldamage.distance.maximum</summary>
    public static int MAXIMUM_DISTANCE_DAMAGE = 50;

    /// <summary>Property key: gameserver.falldamage.distance.midair</summary>
    public static int MAXIMUM_DISTANCE_MIDAIR = 200;
}
