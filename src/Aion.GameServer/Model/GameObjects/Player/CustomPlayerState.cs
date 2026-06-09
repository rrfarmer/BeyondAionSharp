using System;

namespace Aion.GameServer.Model.GameObjects.Player;

/// <summary>
/// Java parity: model/gameobjects/player/CustomPlayerState. Per-instance bit mask (composite members OR others)
/// → C# enum + extension GetMask().
/// </summary>
public enum CustomPlayerState
{
    WATCHING_CUTSCENE,
    INVULNERABLE,
    EVENT_MODE,
    TELEPORTATION_MODE,
    NO_SKILL_COOLDOWN_MODE,
    NO_WHISPERS_MODE,
    ENEMY_OF_ALL_NPCS,
    ENEMY_OF_ALL_PLAYERS,
    NEUTRAL_TO_ALL_NPCS,
    NEUTRAL_TO_ALL_PLAYERS,
    ENEMY_OF_EVERYONE,
    NEUTRAL_TO_EVERYONE
}

public static class CustomPlayerStateExtensions
{
    public static int GetMask(this CustomPlayerState s) => s switch
    {
        CustomPlayerState.WATCHING_CUTSCENE => 1,
        CustomPlayerState.INVULNERABLE => 1 << 1,
        CustomPlayerState.EVENT_MODE => 1 << 2,
        CustomPlayerState.TELEPORTATION_MODE => 1 << 3,
        CustomPlayerState.NO_SKILL_COOLDOWN_MODE => 1 << 4,
        CustomPlayerState.NO_WHISPERS_MODE => 1 << 5,
        CustomPlayerState.ENEMY_OF_ALL_NPCS => 1 << 6,
        CustomPlayerState.ENEMY_OF_ALL_PLAYERS => 1 << 7,
        CustomPlayerState.NEUTRAL_TO_ALL_NPCS => 1 << 8,
        CustomPlayerState.NEUTRAL_TO_ALL_PLAYERS => 1 << 9,
        CustomPlayerState.ENEMY_OF_EVERYONE => (1 << 6) | (1 << 7),
        CustomPlayerState.NEUTRAL_TO_EVERYONE => (1 << 8) | (1 << 9),
        _ => throw new ArgumentOutOfRangeException(),
    };
}
