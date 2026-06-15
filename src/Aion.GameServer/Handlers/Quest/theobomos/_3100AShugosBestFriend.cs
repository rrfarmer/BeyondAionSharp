using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/**
 * @author Balthazar
 */
public class _3100AShugosBestFriend : AbstractQuestHandler
{
    public _3100AShugosBestFriend() : base(3100)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203792).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203792).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798168).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798169).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203792)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.ASK_QUEST_ACCEPT:
                    case DialogAction.QUEST_ACCEPT_1:
                        return SendQuestStartDialog(env, 182208072, 1);
                }
            }
        }

        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 798168:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            {
                                if (qs.GetQuestVarById(0) == 0)
                                {
                                    return SendQuestDialog(env, 1352);
                                }
                                return false;
                            }
                        case DialogAction.SETPRO1:
                            {
                                return DefaultCloseDialog(env, 0, 1, true, false, 0, 0, 182208072, 1);
                            }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798169)
            {
                if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
