using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Handler;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/quests/QuestUseNpcAI (Majka).</summary>
[AIName("quest_use_npc")]
public class QuestUseNpcAI : ActionItemNpcAI
{
    public QuestUseNpcAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        if (GetObjectTemplate().IsDialogNpc())
            TalkEventHandler.OnTalk(this, player);
    }
}
