namespace Aion.GameServer.Model.GameObjects;

/// <summary>
/// Classifies the kind of NPC-derived object (normal monster, summon, trap, servant, ...).
/// Java parity: model/gameobjects/NpcObjectType.
/// </summary>
public enum NpcObjectType
{
    NORMAL = 1,
    SUMMON = 2,
    HOMING = 16,
    TRAP = 32,
    SKILLAREA = 64,
    TOTEM = 128,    // TODO not implemented
    GROUPGATE = 256,
    SERVANT = 1024,
    PET = 2048,     // TODO not used
}

public static class NpcObjectTypeExtensions
{
    // Java parity: getId()
    public static int GetId(this NpcObjectType type) => (int)type;
}
