using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _30207SoulInvocationCeremony : AbstractQuestHandler
{
    public _30207SoulInvocationCeremony() : base(30207)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798941).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798941).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798941)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 798941:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            long itemCount1 = player.GetInventory().GetItemCountByItemId(182209609);
                            if (itemCount1 > 19)
                            {
                                RemoveQuestItem(env, 182209609, 20);
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                return SendQuestDialog(env, 2375);
                            }
                            else
                                return SendQuestDialog(env, 2716);
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798941)
            {
                if (env.GetDialogActionId() == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
