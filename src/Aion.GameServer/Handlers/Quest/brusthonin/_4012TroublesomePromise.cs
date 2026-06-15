using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _4012TroublesomePromise : AbstractQuestHandler
{
    public _4012TroublesomePromise() : base(4012)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(205142).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(205142).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730104).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700342).AddOnTalkEvent(questId);
        qe.AddHandlerSideQuestDrop(questId, 700342, 182209005, 1, 100);
        qe.RegisterOnGetItem(182209005, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 205142)
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
                case 700342:
                    if (qs.GetQuestVarById(0) == 0 && env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    {
                        return true; // loot
                    }
                    break;
                case 730104:
                    if (qs.GetQuestVarById(0) == 1)
                    {
                        if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 2375);
                        else if (env.GetDialogActionId() == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                        {
                            RemoveQuestItem(env, 182209005, 1);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return SendQuestDialog(env, 5);
                        }
                        else
                            return SendQuestEndDialog(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 730104)
                return SendQuestEndDialog(env);
        }
        return false;
    }

    public override bool OnGetItemEvent(QuestEnv env)
    {
        return DefaultOnGetItemEvent(env, 0, 0, true); // reward
    }
}
