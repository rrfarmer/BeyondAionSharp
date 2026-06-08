namespace Aion.GameServer.Model;

/// <summary>
/// Identifies a scheduled per-creature task slot in the controller task map.
/// Java parity: model/TaskId.
/// </summary>
public enum TaskId
{
    Decay,
    Teleport,          // player teleport task after leave animation
    Prison,
    ProtectionActive,
    Drown,
    Despawn,           // npc despawn task / player leaveWorld task after dc/sendlog error
    QuestTimer,        // Quest task with timer
    QuestFollow,       // Follow task checker
    PlayerUpdate,
    InventoryUpdate,
    InstanceKick,      // scheduled instance kick after team leave/kick
    Gag,
    ItemUse,
    ActionItemNpc,
    HouseObjectUse,
    ExpressMailUse,
    SkillUse,
    PetUpdate,
    SummonFollow,
    ZoneMaterialAction,
    TerrainMaterialAction,
    Shout,
}
