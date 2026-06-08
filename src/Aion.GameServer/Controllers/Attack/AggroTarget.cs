namespace Aion.GameServer.Controllers.Attack;

/// <summary>Aggro target selection mode. Java parity: controllers/attack/AggroTarget.</summary>
public enum AggroTarget
{
    RANDOM,
    RANDOM_EXCEPT_CURRENT_TARGET,
    MOST_HATED,
    SECOND_MOST_HATED,
    THIRD_MOST_HATED,
}
