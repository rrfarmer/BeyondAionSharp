using System.Collections.Generic;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_NEARBY_QUESTS (MrPoke, Rolandas, Neon). Sends nearby quest ids (with not-yet-available bit). Map.entrySet->KeyValuePair iteration; -size & 0xFFFF.</summary>
public class SM_NEARBY_QUESTS : AionServerPacket
{
    private const int notYetAvailableBit = 1 << 17;
    private IDictionary<int, int> nearbyQuestList;

    public SM_NEARBY_QUESTS(IDictionary<int, int> nearbyQuestList)
    {
        this.nearbyQuestList = nearbyQuestList;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(0);
        WriteH(-nearbyQuestList.Count & 0xFFFF);
        foreach (KeyValuePair<int, int> nearbyQuest in nearbyQuestList)
        {
            int questId = nearbyQuest.Key;
            if (nearbyQuest.Value > 0)
                questId |= notYetAvailableBit; // for transparent/grey quest marker above npc's head
            WriteD(questId);
        }
    }
}
