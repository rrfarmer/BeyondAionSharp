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
    Active = 1,                  // 1
    Flying = 1 << 1,             // 2
    Resting = 1 << 2,            // 4
    FloatingCorpse = 1 << 3,     // 8
    Unk = 1 << 4,                // 16
    WeaponEquipped = 1 << 5,     // 32
    WalkMode = 1 << 6,           // 64 (set = walking, unset = running)
    Powershard = 1 << 7,         // 128
    Treatment = 1 << 8,          // 256
    Gliding = 1 << 9,            // 512

    // multibit (id = combined value of multiple single-bit states)
    Chair = Flying + Resting,                       // 2 + 4 (mustMatchExact)
    Dead = Active + Flying + Resting,               // 1 + 2 + 4
    PrivateShop = Active + Flying + FloatingCorpse, // 1 + 2 + 8 (mustMatchExact)
    Looting = Resting + FloatingCorpse,             // 4 + 8
}

public static class CreatureStateExtensions
{
    // Java parity: getId()
    public static int GetId(this CreatureState state) => (int)state;

    // Java parity: mustMatchExact() — only CHAIR and PRIVATE_SHOP are declared exact-match.
    public static bool MustMatchExact(this CreatureState state) =>
        state == CreatureState.Chair || state == CreatureState.PrivateShop;
}
