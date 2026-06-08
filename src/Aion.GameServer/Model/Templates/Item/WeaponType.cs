namespace Aion.GameServer.Model.Templates.Item;

/// <summary>
/// Weapon type, each with its required gathering/crafting skills and hand-slot count.
/// Java parity: model/templates/item/WeaponType (@XmlType("weapon_type") @XmlEnum).
/// </summary>
/// <remarks>
/// Declaration order is preserved so the ordinal-based <see cref="WeaponTypeExtensions.GetMask"/>
/// (1 &lt;&lt; ordinal) matches Java. Per-constant requiredSkills/slots live in the extensions class.
/// </remarks>
public enum WeaponType
{
    DAGGER_1H,
    MACE_1H,
    SWORD_1H,
    TOOLHOE_1H,
    GUN_1H,
    BOOK_2H,
    ORB_2H,
    POLEARM_2H,
    STAFF_2H,
    SWORD_2H,
    TOOLPICK_2H,
    TOOLROD_2H,
    BOW,
    CANNON_2H,
    HARP_2H,
    GUN_2H,
    KEYBLADE_2H,
    KEYHAMMER_2H,
}

public static class WeaponTypeExtensions
{
    private static readonly Dictionary<WeaponType, (int[] Skills, int Slots)> Table = new()
    {
        [WeaponType.DAGGER_1H] = (new[] { 30, 9 }, 1),
        [WeaponType.MACE_1H] = (new[] { 3, 10 }, 1),
        [WeaponType.SWORD_1H] = (new[] { 1, 8 }, 1),
        [WeaponType.TOOLHOE_1H] = (Array.Empty<int>(), 1),
        [WeaponType.GUN_1H] = (new[] { 83, 76 }, 1),
        [WeaponType.BOOK_2H] = (new[] { 64 }, 2),
        [WeaponType.ORB_2H] = (new[] { 64 }, 2),
        [WeaponType.POLEARM_2H] = (new[] { 16 }, 2),
        [WeaponType.STAFF_2H] = (new[] { 53 }, 2),
        [WeaponType.SWORD_2H] = (new[] { 15 }, 2),
        [WeaponType.TOOLPICK_2H] = (Array.Empty<int>(), 2),
        [WeaponType.TOOLROD_2H] = (Array.Empty<int>(), 2),
        [WeaponType.BOW] = (new[] { 17 }, 2),
        [WeaponType.CANNON_2H] = (new[] { 77 }, 2),
        [WeaponType.HARP_2H] = (new[] { 92, 78 }, 2),
        [WeaponType.GUN_2H] = (Array.Empty<int>(), 2),
        [WeaponType.KEYBLADE_2H] = (new[] { 76, 79 }, 2),
        [WeaponType.KEYHAMMER_2H] = (Array.Empty<int>(), 2),
    };

    // Java parity: getRequiredSkills()
    public static int[] GetRequiredSkills(this WeaponType type) => Table[type].Skills;

    // Java parity: getRequiredSlots()
    public static int GetRequiredSlots(this WeaponType type) => Table[type].Slots;

    // Java parity: getMask() — 1 << ordinal()
    public static int GetMask(this WeaponType type) => 1 << (int)type;
}
