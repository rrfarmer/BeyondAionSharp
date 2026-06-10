using System.Collections.Generic;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_QUEST_REPEAT (Rolandas, Neon). Sends the list of repeatable quest ids.</summary>
public class SM_QUEST_REPEAT : AionServerPacket
{
    private List<int> repeatableQuests;

    public SM_QUEST_REPEAT(List<int> repeatableQuests)
    {
        this.repeatableQuests = repeatableQuests;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(repeatableQuests.Count);
        foreach (int questId in repeatableQuests)
            WriteD(questId);

        // There are following messages after this packet:
        // You can receive the daily quest. - STR_MSG_QUEST_LIMIT_RESET_DAILY = 1400854
        // You can receive the daily quest again at %0 in the morning. - STR_MSG_QUEST_LIMIT_START_DAILY = 1400855
        // You can receive the weekly quest. - STR_MSG_QUEST_LIMIT_RESET_WEEK = 1400856
        // You can receive the weekly quest again at %1 in the morning on %0. - STR_MSG_QUEST_LIMIT_START_WEEK = 1400857
    }
}
