using System;

namespace Aion.GameServer.Model.Autogroup;

/// <summary>Java parity: model/autogroup/EntryRequestType. Per-instance byte id → enum + extension. Java byte → sbyte.</summary>
public enum EntryRequestType
{
    NEW_GROUP_ENTRY,
    QUICK_GROUP_ENTRY,
    GROUP_ENTRY
}

public static class EntryRequestTypeExtensions
{
    public static sbyte GetId(this EntryRequestType t) => t switch
    {
        EntryRequestType.NEW_GROUP_ENTRY => 0,
        EntryRequestType.QUICK_GROUP_ENTRY => 1,
        EntryRequestType.GROUP_ENTRY => 2,
        _ => throw new ArgumentOutOfRangeException(),
    };

    // Java parity: static getTypeById(byte) — returns null if not found.
    public static EntryRequestType? GetTypeById(sbyte id)
    {
        foreach (EntryRequestType ert in Enum.GetValues(typeof(EntryRequestType)))
        {
            if (ert.GetId() == id)
            {
                return ert;
            }
        }
        return null;
    }
}
