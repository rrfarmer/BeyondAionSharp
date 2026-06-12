using System;
using System.Collections.Generic;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;
using Type = global::Aion.GameServer.Model.Team.Legion.LegionHistoryAction.Type;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LEGION_HISTORY (Simple, KID, xTz). Paged legion history (8/page). LegionHistoryEntry record accessors -> PascalCase; Type enum is sequential (LEGION/REWARD/WAREHOUSE) so ordinal()->(int); subList->GetRange; Collections.emptyList->new List. LegionHistory* red-tolerated.</summary>
public class SM_LEGION_HISTORY : AionServerPacket
{
    private const int ENTRIES_PER_PAGE = 8;

    private readonly int totalEntries;
    private readonly int page;
    private readonly List<LegionHistoryEntry> pageEntries;
    private readonly Type type;

    public SM_LEGION_HISTORY(List<LegionHistoryEntry> history, Type type)
        : this(history, 0, type)
    {
    }

    public SM_LEGION_HISTORY(List<LegionHistoryEntry> history, int page, Type type)
    {
        this.totalEntries = history.Count;
        this.page = page;
        this.pageEntries = FindEntriesForCurrentPage(history);
        this.type = type;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(totalEntries);
        WriteD(page); // current page
        WriteD(pageEntries.Count);
        foreach (LegionHistoryEntry entry in pageEntries)
        {
            WriteD(entry.EpochSeconds());
            WriteC(entry.Action().GetId());
            WriteC(0); // unk
            WriteS(entry.Name(), 32);
            WriteS(entry.Description(), 32);
            WriteH(0);
        }
        WriteH((int)type);
    }

    private List<LegionHistoryEntry> FindEntriesForCurrentPage(List<LegionHistoryEntry> legionHistory)
    {
        int startIndex = page * ENTRIES_PER_PAGE;
        if (startIndex < 0 || startIndex >= legionHistory.Count)
            return new List<LegionHistoryEntry>();
        int endIndex = Math.Min(startIndex + ENTRIES_PER_PAGE, legionHistory.Count);
        return legionHistory.GetRange(startIndex, endIndex - startIndex);
    }
}
