using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Item;

/// <summary>
/// What an item activates on (standalone/target/mento, or a specific NPC race).
/// Java parity: model/templates/item/ItemActivationTarget (@XmlEnum).
/// </summary>
/// <remarks>Per-constant <c>race</c> (null for the non-race constants) lives in the extensions class.</remarks>
public enum ItemActivationTarget
{
    STANDALONE,
    TARGET,
    MYMENTO,
    WORLD_EVENT_CAKE_D,
    WORLD_EVENT_CAKE_L,

    // races
    BROWNIE,
    GCHIEF_LIGHT,
    GHENCHMAN_LIGHT,
    GHENCHMAN_DARK,
    KRALL,
    LF5_Q_ITEM,
    LIVINGWATER,
    LYCAN,
    EVENT_TOWER_LIGHT,
    EVENT_TOWER_DARK,
    WORLD_EVENT_DEFTOWER,
}

public static class ItemActivationTargetExtensions
{
    private static readonly Dictionary<ItemActivationTarget, Race> Races = new()
    {
        [ItemActivationTarget.BROWNIE] = Race.BROWNIE,
        [ItemActivationTarget.GCHIEF_LIGHT] = Race.GCHIEF_LIGHT,
        [ItemActivationTarget.GHENCHMAN_LIGHT] = Race.GHENCHMAN_LIGHT,
        [ItemActivationTarget.GHENCHMAN_DARK] = Race.GHENCHMAN_DARK,
        [ItemActivationTarget.KRALL] = Race.KRALL,
        [ItemActivationTarget.LF5_Q_ITEM] = Race.LF5_Q_ITEM,
        [ItemActivationTarget.LIVINGWATER] = Race.LIVINGWATER,
        [ItemActivationTarget.LYCAN] = Race.LYCAN,
        [ItemActivationTarget.EVENT_TOWER_LIGHT] = Race.EVENT_TOWER_LIGHT,
        [ItemActivationTarget.EVENT_TOWER_DARK] = Race.EVENT_TOWER_DARK,
        [ItemActivationTarget.WORLD_EVENT_DEFTOWER] = Race.WORLD_EVENT_DEFTOWER,
    };

    // Java parity: getRace() — null for the non-race constants.
    public static Race? GetRace(this ItemActivationTarget target) =>
        Races.TryGetValue(target, out var race) ? race : null;
}
