using System.Collections.Generic;

namespace Aion.GameServer.Model.Items.Storage;

/// <summary>
/// Java parity: model/items/storage/StorageType. Java enum with per-instance fields
/// (id/limit/length/specialLimit) - modeled as a class-enum (static instances) since C# enums
/// cannot carry constructor data. PET_BAG_*/HOUSE_WH_* are plain int constants as in Java.
/// </summary>
public sealed class StorageType
{
    public static readonly StorageType CUBE = new StorageType(0, 27, 9, 102);
    public static readonly StorageType REGULAR_WAREHOUSE = new StorageType(1, 24, 8);
    public static readonly StorageType ACCOUNT_WAREHOUSE = new StorageType(2, 16, 8);
    public static readonly StorageType LEGION_WAREHOUSE = new StorageType(3, 56, 8);
    public static readonly StorageType PET_BAG_6 = new StorageType(32, 6, 6);
    public static readonly StorageType PET_BAG_12 = new StorageType(33, 12, 6);
    public static readonly StorageType PET_BAG_18 = new StorageType(34, 18, 6);
    public static readonly StorageType PET_BAG_24 = new StorageType(35, 24, 6);
    public static readonly StorageType CASH_PET_BAG_12 = new StorageType(36, 12, 6);
    public static readonly StorageType CASH_PET_BAG_18 = new StorageType(37, 18, 6);
    public static readonly StorageType CASH_PET_BAG_30 = new StorageType(38, 30, 6);
    public static readonly StorageType CASH_PET_BAG_24 = new StorageType(39, 24, 6);
    public static readonly StorageType PET_BAG_30 = new StorageType(40, 30, 6);
    public static readonly StorageType CASH_PET_BAG_26 = new StorageType(41, 26, 6);
    public static readonly StorageType CASH_PET_BAG_32 = new StorageType(42, 32, 6);
    public static readonly StorageType CASH_PET_BAG_34 = new StorageType(43, 34, 6);
    public static readonly StorageType HOUSE_STORAGE_01 = new StorageType(60, 9, 9); // Plain 1-Drawer Cabinet
    public static readonly StorageType HOUSE_STORAGE_02 = new StorageType(61, 9, 9); // Simple 1-Drawer Cabinet
    public static readonly StorageType HOUSE_STORAGE_03 = new StorageType(62, 9, 9); // Clean 1-Drawer Cabinet
    public static readonly StorageType HOUSE_STORAGE_04 = new StorageType(63, 9, 9); // Convenient 1-Drawer Cabinet
    public static readonly StorageType HOUSE_STORAGE_05 = new StorageType(64, 9, 9); // Strong 1-Drawer Cabinet
    public static readonly StorageType HOUSE_STORAGE_06 = new StorageType(65, 9, 9); // Firm 1-Drawer Cabinet
    public static readonly StorageType HOUSE_STORAGE_07 = new StorageType(66, 9, 9); // Fine 1-Drawer Cabinet
    public static readonly StorageType HOUSE_STORAGE_08 = new StorageType(67, 9, 9); // Decorated 1-Drawer Cabinet
    public static readonly StorageType HOUSE_STORAGE_09 = new StorageType(68, 18, 9); // Small 2-Drawer Cabinet
    public static readonly StorageType HOUSE_STORAGE_10 = new StorageType(69, 18, 9); // Simple 2-Drawer Cabinet
    public static readonly StorageType HOUSE_STORAGE_11 = new StorageType(70, 18, 9); // Clean 2-Drawer Cabinet
    public static readonly StorageType HOUSE_STORAGE_12 = new StorageType(71, 18, 9); // Convenient 2-Drawer Cabinet
    public static readonly StorageType HOUSE_STORAGE_13 = new StorageType(72, 18, 9); // Strong 2-Drawer Cabinet
    public static readonly StorageType HOUSE_STORAGE_14 = new StorageType(73, 18, 9); // Firm 2-Drawer Cabinet
    public static readonly StorageType HOUSE_STORAGE_15 = new StorageType(74, 27, 9); // Spacious 3-Drawer Cabinet
    public static readonly StorageType HOUSE_STORAGE_16 = new StorageType(75, 27, 9); // Simple 3-Drawer Cabinet
    public static readonly StorageType HOUSE_STORAGE_17 = new StorageType(76, 27, 9); // Clean 3-Drawer Cabinet
    public static readonly StorageType HOUSE_STORAGE_18 = new StorageType(77, 27, 9); // Convenient 3-Drawer Cabinet
    public static readonly StorageType HOUSE_STORAGE_19 = new StorageType(78, 27, 9); // Strong 3-Drawer Cabinet
    public static readonly StorageType HOUSE_STORAGE_20 = new StorageType(79, 27, 9); // Firm 3-Drawer Cabinet
    public static readonly StorageType BROKER = new StorageType(126);
    public static readonly StorageType MAILBOX = new StorageType(127);

    public const int PET_BAG_MIN = 32;
    public const int PET_BAG_MAX = 43;
    public const int HOUSE_WH_MIN = 60;
    public const int HOUSE_WH_MAX = 79; // Custom cabinets ?? // since 3.0 to 4.0

    private readonly int id;
    private readonly int limit;
    private readonly int length;
    private readonly int specialLimit;

    private StorageType(int id, int limit, int length, int specialLimit) : this(id, limit, length)
    {
        this.specialLimit = specialLimit;
    }

    private StorageType(int id, int limit, int length) : this(id)
    {
        this.limit = limit;
        this.length = length;
    }

    private StorageType(int id)
    {
        this.id = id;
    }

    public int GetId()
    {
        return id;
    }

    public int GetLimit()
    {
        return limit;
    }

    public int GetLength()
    {
        return length;
    }

    public int GetSpecialLimit()
    {
        return specialLimit;
    }

    // Java parity: enum values() declaration order.
    private static readonly StorageType[] VALUES =
    {
        CUBE,
        REGULAR_WAREHOUSE,
        ACCOUNT_WAREHOUSE,
        LEGION_WAREHOUSE,
        PET_BAG_6,
        PET_BAG_12,
        PET_BAG_18,
        PET_BAG_24,
        CASH_PET_BAG_12,
        CASH_PET_BAG_18,
        CASH_PET_BAG_30,
        CASH_PET_BAG_24,
        PET_BAG_30,
        CASH_PET_BAG_26,
        CASH_PET_BAG_32,
        CASH_PET_BAG_34,
        HOUSE_STORAGE_01,
        HOUSE_STORAGE_02,
        HOUSE_STORAGE_03,
        HOUSE_STORAGE_04,
        HOUSE_STORAGE_05,
        HOUSE_STORAGE_06,
        HOUSE_STORAGE_07,
        HOUSE_STORAGE_08,
        HOUSE_STORAGE_09,
        HOUSE_STORAGE_10,
        HOUSE_STORAGE_11,
        HOUSE_STORAGE_12,
        HOUSE_STORAGE_13,
        HOUSE_STORAGE_14,
        HOUSE_STORAGE_15,
        HOUSE_STORAGE_16,
        HOUSE_STORAGE_17,
        HOUSE_STORAGE_18,
        HOUSE_STORAGE_19,
        HOUSE_STORAGE_20,
        BROKER,
        MAILBOX,
    };

    public static IReadOnlyList<StorageType> Values()
    {
        return VALUES;
    }

    public static StorageType GetStorageTypeById(int id)
    {
        foreach (StorageType st in VALUES)
        {
            if (st.id == id)
                return st;
        }
        return null;
    }

    public static int GetStorageId(int limit, int length)
    {
        foreach (StorageType st in VALUES)
        {
            if (st.limit == limit && st.length == length)
                return st.id;
        }
        return -1;
    }
}
