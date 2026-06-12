namespace Aion.GameServer.Model.GameObjects.State;

/// <summary>
/// Bit-flag (and a few multibit) creature states sent to the client.
/// Java parity: model/gameobjects/state/CreatureState.
/// </summary>
/// <remarks>
/// Java carries two fields per enum constant: <c>id</c> and <c>mustMatchExact</c>. The single-bit
/// states have unique power-of-two ids; CHAIR and PRIVATE_SHOP are multibit and must match exactly.
/// Here the ids are the explicit enum values and <see cref="CreatureStateExtensions.MustMatchExact"/>
/// reproduces the exact-match flag.
/// </remarks>
public enum CreatureState
{
    ACTIVE = 1,                  // 1
    FLYING = 1 << 1,             // 2
    RESTING = 1 << 2,            // 4
    FLOATING_CORPSE = 1 << 3,     // 8
    UNK = 1 << 4,                // 16
    WEAPON_EQUIPPED = 1 << 5,     // 32
    WALK_MODE = 1 << 6,           // 64 (set = walking, unset = running)
    POWERSHARD = 1 << 7,         // 128
    TREATMENT = 1 << 8,          // 256
    GLIDING = 1 << 9,            // 512

    // multibit (id = combined value of multiple single-bit states)
    CHAIR = FLYING + RESTING,                       // 2 + 4 (mustMatchExact)
    DEAD = ACTIVE + FLYING + RESTING,               // 1 + 2 + 4
    PRIVATE_SHOP = ACTIVE + FLYING + FLOATING_CORPSE, // 1 + 2 + 8 (mustMatchExact)
    LOOTING = RESTING + FLOATING_CORPSE,             // 4 + 8
}

public static class CreatureStateExtensions
{
    // Java parity: getId()
    public static int GetId(this CreatureState state) => (int)state;

    // Java parity: mustMatchExact() — only CHAIR and PRIVATE_SHOP are declared exact-match.
    public static bool MustMatchExact(this CreatureState state) =>
        state == CreatureState.CHAIR || state == CreatureState.PRIVATE_SHOP;
}
