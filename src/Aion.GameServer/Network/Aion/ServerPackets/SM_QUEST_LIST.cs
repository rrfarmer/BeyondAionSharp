using System;
using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_QUEST_LIST. Sends uncompleted quest states (id, status value, quest-vars|flags, complete count capped at 255). Converges PlayerEnterWorldService. -size&amp;0xFFFF preserved; Math.min->Math.Min. QuestState/AionServerPacket red-tolerated.</summary>
public class SM_QUEST_LIST : AionServerPacket
{
    private List<QuestState> questStates;

    public SM_QUEST_LIST(List<QuestState> questState)
    {
        this.questStates = questState;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(0x01); // unk
        WriteH(-questStates.Count & 0xFFFF);
        foreach (QuestState qs in questStates)
        {
            WriteD(qs.GetQuestId());
            WriteC(qs.GetStatus().Value());
            WriteD(qs.GetQuestVars().GetQuestVars() | (qs.GetFlags() << 24));
            WriteC(Math.Min(qs.GetCompleteCount(), 255));
        }
    }
}
