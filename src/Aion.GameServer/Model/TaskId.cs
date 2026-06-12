namespace Aion.GameServer.Model;

/// <summary>
/// Identifies a scheduled per-creature task slot in the controller task map.
/// Java parity: model/TaskId (ATracer) — UPPER_CASE values match Java.
/// </summary>
public enum TaskId
{
    DECAY,
    TELEPORT,          // player teleport task after leave animation
    PRISON,
    PROTECTION_ACTIVE,
    DROWN,
    DESPAWN,           // npc despawn task / player leaveWorld task after dc/sendlog error
    QUEST_TIMER,       // Quest task with timer
    QUEST_FOLLOW,      // Follow task checker
    PLAYER_UPDATE,
    INVENTORY_UPDATE,
    INSTANCE_KICK,     // scheduled instance kick after team leave/kick
    GAG,
    ITEM_USE,
    ACTION_ITEM_NPC,
    HOUSE_OBJECT_USE,
    EXPRESS_MAIL_USE,
    SKILL_USE,
    PET_UPDATE,
    SUMMON_FOLLOW,
    ZONE_MATERIAL_ACTION,
    TERRAIN_MATERIAL_ACTION,
    SHOUT,
}
