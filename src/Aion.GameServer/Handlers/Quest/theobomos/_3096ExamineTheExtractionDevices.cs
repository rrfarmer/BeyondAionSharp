using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _3096ExamineTheExtractionDevices : AbstractQuestHandler
{
    public _3096ExamineTheExtractionDevices() : base(3096)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798225).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798225).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700423).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700424).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700425).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700426).AddOnTalkEvent(questId);
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
            if (targetId == 798225)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
        }

        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 798225:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            return SendQuestDialog(env, 2375);
                        }
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        {
                            return CheckQuestItems(env, 0, 1, true, 5, 2716);
                        }
                    }
                    return false;
                case 700423:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                        {
                            if (player.GetInventory().GetItemCountByItemId(182208067) < 1)
                            {
                                return true;
                            }
                            break;
                        }
                    }
                    return false;
                case 700424:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                        {
                            if (player.GetInventory().GetItemCountByItemId(182208068) < 1)
                            {
                                return true;
                            }
                            break;
                        }
                    }
                    return false;
                case 700425:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                        {
                            if (player.GetInventory().GetItemCountByItemId(182208069) < 1)
                            {
                                return true;
                            }
                            break;
                        }
                    }
                    return false;
                case 700426:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                        {
                            if (player.GetInventory().GetItemCountByItemId(182208070) < 1)
                            {
                                return true;
                            }
                            break;
                        }
                    }
                    return false;
            }
            return false;
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798225)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
