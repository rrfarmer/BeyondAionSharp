namespace Aion.GameServer.Model.Items;

// added by Blackhive original credits to xTr 2.0.0.5 mod by Tomate
/// <summary>
/// Bit-mask flags for item capabilities (tradeable, sellable, storable, …).
/// Java parity: model/items/ItemMask.
/// </summary>
public static class ItemMask
{
    public const int LIMIT_ONE = 1;
    public const int TRADEABLE = 1 << 1;
    public const int SELLABLE = 1 << 2;
    public const int STORABLE_IN_WH = 1 << 3;
    public const int STORABLE_IN_AWH = 1 << 4;
    public const int STORABLE_IN_LWH = 1 << 5;
    public const int BREAKABLE = 1 << 6;
    public const int SOUL_BOUND = 1 << 7;
    public const int REMOVE_LOGOUT = 1 << 8;
    public const int NO_ENCHANT = 1 << 9;
    public const int CAN_PROC_ENCHANT = 1 << 10;
    public const int CAN_COMPOSITE_WEAPON = 1 << 11;
    public const int REMODELABLE = 1 << 12;
    public const int CAN_SPLIT = 1 << 13;
    public const int DELETABLE = 1 << 14;
    public const int DYEABLE = 1 << 15;
    public const int CAN_AP_EXTRACT = 1 << 16; // not sure
    public const int CAN_POLISH = 1 << 17; // not sure
    public const int LEGION_TRADEABLE = 1 << 18;
}
