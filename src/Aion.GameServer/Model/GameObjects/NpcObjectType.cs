namespace Aion.GameServer.Model.GameObjects;

/// <summary>
/// Classifies the kind of NPC-derived object (normal monster, summon, trap, servant, ...).
/// Java parity: model/gameobjects/NpcObjectType.
/// </summary>
public enum NpcObjectType
{
    Normal = 1,
    Summon = 2,
    Homing = 16,
    Trap = 32,
    SkillArea = 64,
    Totem = 128,    // TODO not implemented
    GroupGate = 256,
    Servant = 1024,
    Pet = 2048,     // TODO not used
}

public static class NpcObjectTypeExtensions
{
    // Java parity: getId()
    public static int GetId(this NpcObjectType type) => (int)type;
}
