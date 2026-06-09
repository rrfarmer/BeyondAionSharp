using System.Collections.Generic;

namespace Aion.GameServer.Model.Team.Legion;

/// <summary>
/// Java parity: model/team/legion/LegionHistoryAction. Java enum with per-instance fields (id + nested Type)
/// → class-enum (static instances) preserving the nested Type enum and id/type accessors.
/// </summary>
public sealed class LegionHistoryAction
{
    public enum Type
    {
        LEGION,
        REWARD,
        WAREHOUSE,
    }

    public static readonly LegionHistoryAction CREATE = new LegionHistoryAction(0, Type.LEGION); // No parameters
    public static readonly LegionHistoryAction JOIN = new LegionHistoryAction(1, Type.LEGION); // Parameter: name
    public static readonly LegionHistoryAction KICK = new LegionHistoryAction(2, Type.LEGION); // Parameter: name
    public static readonly LegionHistoryAction LEVEL_UP = new LegionHistoryAction(3, Type.LEGION); // Parameter: legion level
    public static readonly LegionHistoryAction APPOINTED = new LegionHistoryAction(4, Type.LEGION); // Parameter: legion level
    public static readonly LegionHistoryAction EMBLEM_REGISTER = new LegionHistoryAction(5, Type.LEGION); // No parameters
    public static readonly LegionHistoryAction EMBLEM_MODIFIED = new LegionHistoryAction(6, Type.LEGION); // No parameters
    // 7 to 10 are not used anymore or never implemented
    public static readonly LegionHistoryAction DEFENSE = new LegionHistoryAction(11, Type.REWARD); // Parameter: name = kinah amount, description = fortress id
    public static readonly LegionHistoryAction OCCUPATION = new LegionHistoryAction(12, Type.REWARD); // Parameter: name = kinah amount, description = fortress id
    public static readonly LegionHistoryAction LEGION_RENAME = new LegionHistoryAction(13, Type.LEGION); // Parameter: old name, new name
    public static readonly LegionHistoryAction CHARACTER_RENAME = new LegionHistoryAction(14, Type.LEGION); // Parameter: old name, new name
    public static readonly LegionHistoryAction ITEM_DEPOSIT = new LegionHistoryAction(15, Type.WAREHOUSE); // Parameter: name
    public static readonly LegionHistoryAction ITEM_WITHDRAW = new LegionHistoryAction(16, Type.WAREHOUSE); // Parameter: name
    public static readonly LegionHistoryAction KINAH_DEPOSIT = new LegionHistoryAction(17, Type.WAREHOUSE); // Parameter: name
    public static readonly LegionHistoryAction KINAH_WITHDRAW = new LegionHistoryAction(18, Type.WAREHOUSE); // Parameter: name

    private readonly byte id;
    private readonly Type type;

    private LegionHistoryAction(int id, Type type)
    {
        this.id = (byte)id;
        this.type = type;
    }

    public byte GetId()
    {
        return id;
    }

    public Type GetType_()
    {
        return type;
    }

    // Java parity: enum values() declaration order.
    private static readonly LegionHistoryAction[] VALUES =
    {
        CREATE, JOIN, KICK, LEVEL_UP, APPOINTED, EMBLEM_REGISTER, EMBLEM_MODIFIED,
        DEFENSE, OCCUPATION, LEGION_RENAME, CHARACTER_RENAME, ITEM_DEPOSIT, ITEM_WITHDRAW, KINAH_DEPOSIT, KINAH_WITHDRAW,
    };

    public static IReadOnlyList<LegionHistoryAction> Values()
    {
        return VALUES;
    }
}
