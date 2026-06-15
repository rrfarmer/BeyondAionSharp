using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _30055ForMyWife : AbstractQuestHandler
{
    public _30055ForMyWife() : base(30055)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798929).AddOnQuestStart(questId); // Gellius
        qe.RegisterQuestNpc(798929).AddOnTalkEvent(questId); // Gellius
        qe.RegisterQuestNpc(203901).AddOnTalkEvent(questId); // Telemachus
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798929)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798929)
            {
                return SendQuestEndDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 203901)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1352);
                        return false;
                    case DialogAction.SETPRO1:
                        if (var == 0)
                            return DefaultCloseDialog(env, 0, 1, false, false, 182209222, 1, 0, 0);
                        break;
                }
            }
            if (targetId == 798929)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                            return SendQuestDialog(env, 2375);
                        return false;
                    case DialogAction.SELECT_QUEST_REWARD:
                        if (var == 1)
                            return DefaultCloseDialog(env, 1, 2, true, true, 0, 0, 182209222, 1);
                        break;
                }
            }
        }
        return false;
    }
}
