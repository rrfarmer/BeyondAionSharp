using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_DELETE_QUEST. Abandons a quest; cancels the quest timer task and resets its display first if timed. DataManager/QuestService/SM_QUEST_ACTION red-tolerated.</summary>
public class CM_DELETE_QUEST : AionClientPacket
{
    private int questId;

    public CM_DELETE_QUEST(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        questId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        QuestTemplate qt = DataManager.QUEST_DATA.GetQuestById(questId);

        if (qt != null && qt.IsTimer())
        {
            player.GetController().CancelTask(TaskId.QUEST_TIMER);
            SendPacket(new SM_QUEST_ACTION(questId, 0));
        }
        QuestService.AbandonQuest(player, questId);
    }
}
