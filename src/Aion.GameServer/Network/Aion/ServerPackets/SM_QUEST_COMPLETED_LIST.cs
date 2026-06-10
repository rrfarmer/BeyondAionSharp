using System;
using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_QUEST_COMPLETED_LIST (MrPoke, Neon). Completed-quest list (rewrite/insert). Function<QuestState,Integer>->Func<QuestState,int>; -size & 0xFFFF; Math.min->Math.Min. QuestState red-tolerated.</summary>
public class SM_QUEST_COMPLETED_LIST : AionServerPacket
{
    public const int STATIC_BODY_SIZE = 4;
    public static readonly Func<QuestState, int> DYNAMIC_BODY_PART_SIZE_CALCULATOR = (questState) => 6;

    private readonly int updateMode;
    private readonly List<QuestState> questStates;

    public SM_QUEST_COMPLETED_LIST(int updateMode, List<QuestState> questStates)
    {
        this.updateMode = updateMode;
        this.questStates = questStates;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(1); // unk, always 1 (when 0, no entries change)
        WriteC(updateMode); // 0 = rewrite all entries, 1 = insert new entries
        WriteH(-questStates.Count & 0xFFFF);
        foreach (QuestState qs in questStates)
        {
            WriteD(qs.GetQuestId());
            WriteC(Math.Min(qs.GetCompleteCount(), 255));
            WriteC(qs.CanRepeat() ? 0 : 1); // wrong! most times equal to the complete count on retail (else 0), not clear what it is
        }
    }
}
