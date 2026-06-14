namespace Aion.GameServer.Services;

/// <summary>
/// Java parity: dao/CraftCooldownsDAO query constants used by the per-connection Java-style
/// craft cooldown delete/insert flow in PlayerEnterWorldRepository.
/// </summary>
public static class CraftCooldownPersistencePlanService
{
    // Java parity: CraftCooldownsDAO.DELETE_QUERY.
    public const string JavaCraftCooldownDeleteSql = "DELETE FROM `craft_cooldowns` WHERE `player_id`=?";

    // Java parity: CraftCooldownsDAO.INSERT_QUERY.
    public const string JavaCraftCooldownInsertSql = "INSERT INTO `craft_cooldowns` (`player_id`, `delay_id`, `reuse_time`) VALUES (?,?,?)";
}
