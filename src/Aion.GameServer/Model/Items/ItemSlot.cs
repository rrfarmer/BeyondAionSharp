namespace Aion.GameServer.Model.Items;

/// <summary>
/// Inventory/equipment slots an item can occupy, as bit masks (incl. combo slots).
/// Java parity: model/items/ItemSlot.
/// </summary>
/// <remarks>
/// Underlying type is <c>long</c> to match Java's <c>long slotIdMask</c> (slots go up to 1L&lt;&lt;35).
/// The enum value IS the slot mask; the per-constant <c>combo</c> flag lives in
/// <see cref="ItemSlotExtensions"/> (the combo members are the OR-combinations).
/// </remarks>
public enum ItemSlot : long
{
    MAIN_HAND = 1L,
    SUB_HAND = 1L << 1,
    HELMET = 1L << 2,
    TORSO = 1L << 3,
    GLOVES = 1L << 4,
    BOOTS = 1L << 5,
    EARRINGS_LEFT = 1L << 6,
    EARRINGS_RIGHT = 1L << 7,
    RING_LEFT = 1L << 8,
    RING_RIGHT = 1L << 9,
    NECKLACE = 1L << 10,
    SHOULDER = 1L << 11,
    PANTS = 1L << 12,
    POWER_SHARD_RIGHT = 1L << 13,
    POWER_SHARD_LEFT = 1L << 14,
    WINGS = 1L << 15,
    WAIST = 1L << 16,
    MAIN_OFF_HAND = 1L << 17,
    SUB_OFF_HAND = 1L << 18,
    PLUME = 1L << 19,

    // combo
    MAIN_OR_SUB = MAIN_HAND | SUB_HAND, // 3
    MAIN_OFF_OR_SUB_OFF = MAIN_OFF_HAND | SUB_OFF_HAND,
    EARRING_RIGHT_OR_LEFT = EARRINGS_LEFT | EARRINGS_RIGHT, // 192
    RING_RIGHT_OR_LEFT = RING_LEFT | RING_RIGHT, // 768
    SHARD_RIGHT_OR_LEFT = POWER_SHARD_LEFT | POWER_SHARD_RIGHT, // 24576
    RIGHT_HAND = MAIN_HAND | MAIN_OFF_HAND,
    LEFT_HAND = SUB_HAND | SUB_OFF_HAND,
    VISIBLE = MAIN_HAND
            | SUB_HAND
            | HELMET
            | TORSO
            | GLOVES
            | BOOTS
            | EARRINGS_LEFT
            | EARRINGS_RIGHT
            | NECKLACE
            | SHOULDER
            | PANTS
            | POWER_SHARD_RIGHT
            | POWER_SHARD_LEFT
            | WINGS
            | PLUME, // rings were designed to be visible (at the players thumbs), but they have no skins

    // STIGMA slots
    STIGMA1 = 1L << 30,
    STIGMA2 = 1L << 31,
    STIGMA3 = 1L << 32,

    REGULAR_STIGMAS = STIGMA1 | STIGMA2 | STIGMA3,
    ADV_STIGMA1 = 1L << 33,
    ADV_STIGMA2 = 1L << 34,
    ADV_STIGMA3 = 1L << 35,

    ADVANCED_STIGMAS = ADV_STIGMA1 | ADV_STIGMA2 | ADV_STIGMA3,

    ALL_STIGMA = REGULAR_STIGMAS | ADVANCED_STIGMAS,
}

public static class ItemSlotExtensions
{
    // Java parity: the constants declared with the (mask, combo=true) constructor.
    private static readonly HashSet<ItemSlot> Combos = new()
    {
        ItemSlot.MAIN_OR_SUB, ItemSlot.MAIN_OFF_OR_SUB_OFF, ItemSlot.EARRING_RIGHT_OR_LEFT,
        ItemSlot.RING_RIGHT_OR_LEFT, ItemSlot.SHARD_RIGHT_OR_LEFT, ItemSlot.RIGHT_HAND,
        ItemSlot.LEFT_HAND, ItemSlot.VISIBLE, ItemSlot.REGULAR_STIGMAS, ItemSlot.ADVANCED_STIGMAS,
        ItemSlot.ALL_STIGMA,
    };

    // Non-combo single slots, in Java declaration order (for getSlotsFor iteration parity).
    private static readonly ItemSlot[] Singles =
    {
        ItemSlot.MAIN_HAND, ItemSlot.SUB_HAND, ItemSlot.HELMET, ItemSlot.TORSO, ItemSlot.GLOVES,
        ItemSlot.BOOTS, ItemSlot.EARRINGS_LEFT, ItemSlot.EARRINGS_RIGHT, ItemSlot.RING_LEFT,
        ItemSlot.RING_RIGHT, ItemSlot.NECKLACE, ItemSlot.SHOULDER, ItemSlot.PANTS,
        ItemSlot.POWER_SHARD_RIGHT, ItemSlot.POWER_SHARD_LEFT, ItemSlot.WINGS, ItemSlot.WAIST,
        ItemSlot.MAIN_OFF_HAND, ItemSlot.SUB_OFF_HAND, ItemSlot.PLUME,
        ItemSlot.STIGMA1, ItemSlot.STIGMA2, ItemSlot.STIGMA3,
        ItemSlot.ADV_STIGMA1, ItemSlot.ADV_STIGMA2, ItemSlot.ADV_STIGMA3,
    };

    // Java parity: getSlotIdMask()
    public static long GetSlotIdMask(this ItemSlot slot) => (long)slot;

    // Java parity: isCombo()
    public static bool IsCombo(this ItemSlot slot) => Combos.Contains(slot);

    // Java parity: static isAdvancedStigma(long)
    public static bool IsAdvancedStigma(long slot) => ((long)ItemSlot.ADVANCED_STIGMAS & slot) == slot;

    // Java parity: static isRegularStigma(long)
    public static bool IsRegularStigma(long slot) => ((long)ItemSlot.REGULAR_STIGMAS & slot) == slot;

    // Java parity: static isStigma(long)
    public static bool IsStigma(long slot) => ((long)ItemSlot.ALL_STIGMA & slot) == slot;

    // Java parity: static isVisible(long)
    public static bool IsVisible(long slot) => ((long)ItemSlot.VISIBLE & slot) == slot;

    // Java parity: static isTwoHandedWeapon(long)
    public static bool IsTwoHandedWeapon(long slot) =>
        (slot & (long)ItemSlot.MAIN_OR_SUB) == (long)ItemSlot.MAIN_OR_SUB
        || (slot & (long)ItemSlot.MAIN_OFF_OR_SUB_OFF) == (long)ItemSlot.MAIN_OFF_OR_SUB_OFF;

    // Java parity: static getEquipmentSlotType(long)
    public static byte GetEquipmentSlotType(long slot)
    {
        if (!IsVisible(slot))
            return 0; // not equippable

        long leftSlotMask = (long)ItemSlot.SUB_HAND | (long)ItemSlot.EARRINGS_LEFT | (long)ItemSlot.RING_LEFT
                          | (long)ItemSlot.POWER_SHARD_LEFT | (long)ItemSlot.SUB_OFF_HAND;
        if ((slot & leftSlotMask) == 0 || IsTwoHandedWeapon(slot))
            return 1; // default (right-hand) slot

        return 2; // secondary (left-hand) slot
    }

    // Java parity: static getSlotsFor(long)
    public static ItemSlot[] GetSlotsFor(long slotIdMask)
    {
        if (slotIdMask == 0)
            throw new ArgumentException("slotIdMask cannot be 0");
        var slots = new List<ItemSlot>();
        foreach (ItemSlot itemSlot in Singles)
        {
            long mask = (long)itemSlot;
            if ((slotIdMask & mask) == mask)
                slots.Add(itemSlot);
        }
        return slots.ToArray();
    }

    // Java parity: static getSlotFor(long)
    public static ItemSlot GetSlotFor(long slot) => GetSlotsFor(slot)[0];
}
