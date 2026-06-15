using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/quests/QuestStartItemNpcAI (Cheatkiller).</summary>
[AIName("quest_start_use_item")]
public class QuestStartItemNpcAI : ActionItemNpcAI
{
    public QuestStartItemNpcAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        ISet<int> relatedQuests = QuestEngine.QuestEngine.GetInstance().GetQuestNpc(GetOwner().GetNpcId()).GetOnQuestStart();
        if (!QuestEngine.QuestEngine.GetInstance().OnDialog(new QuestEnv(GetOwner(), player, 0, relatedQuests.Count == 0 ? DialogAction.USE_OBJECT : DialogAction.QUEST_SELECT)))
            if (GetObjectTemplate().IsDialogNpc()) // show default dialog
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }
}
