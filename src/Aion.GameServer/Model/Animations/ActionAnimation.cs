namespace Aion.GameServer.Model.Animations;

/// <summary>
/// Animation IDs for use with SM_ACTION_ANIMATION packets.
/// Java parity: model/animations/ActionAnimation.
/// <br/>
/// Note: Java's <c>getId()</c> method = <c>(int)value</c> in C#.
/// Note: <c>CraftLevelUp</c> and <c>ClassChange</c> share value 4, matching Java.
/// </summary>
public enum ActionAnimation : int
{
    // Java parity: LEVEL_UP(0)
    LevelUp = 0,
    // Java parity: UNK(1)
    Unk = 1,
    // Java parity: BIND_KISK(2)
    BindKisk = 2,
    // Java parity: REPAIR_GATE(3)
    RepairGate = 3,
    // Java parity: CRAFT_LEVEL_UP(4)
    CraftLevelUp = 4,
    // Java parity: CLASS_CHANGE(4) — shares value 4 with CRAFT_LEVEL_UP, same as Java
    ClassChange = 4,

    // Java-name aliases (call sites use the Java SCREAMING_SNAKE names; same values, additive).
    LEVEL_UP = LevelUp,
    UNK = Unk,
    BIND_KISK = BindKisk,
    REPAIR_GATE = RepairGate,
    CRAFT_LEVEL_UP = CraftLevelUp,
    CLASS_CHANGE = ClassChange,
}
