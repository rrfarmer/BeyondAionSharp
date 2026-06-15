using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _1948WheresVindachinerk : AbstractQuestHandler
{
    public _1948WheresVindachinerk() : base(1948)
    {
    }

    public override void Register()
    {
        int[] npcs = { 798012, 798004, 798132, 279006 };
        qe.RegisterQuestNpc(798012).AddOnQuestStart(questId);
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798012)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 798004:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            return false;
                        }
                        case DialogAction.SELECT2_1:
                        {
                            PlayQuestMovie(env, 0);
                            return SendQuestDialog(env, 1353);
                        }
                        case DialogAction.SETPRO1:
                        {
                            return DefaultCloseDialog(env, 0, 1);
                        }
                    }
                    return false;
                case 279006:
                    if (var == 2)
                    {
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 2375);
                            case DialogAction.SELECT_QUEST_REWARD:
                                ChangeQuestStep(env, var, var, true);
                                return SendQuestEndDialog(env);
                        }
                    }
                    return false;
                case 798132:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                                return SendQuestDialog(env, 1693);
                            break;
                        case DialogAction.SELECT3_1:
                            return SendQuestDialog(env, 1694);
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 1, 2);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 279006)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
