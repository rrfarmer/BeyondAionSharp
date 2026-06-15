using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author zhkchi
/// </summary>
public class _18805GoingThrifting : AbstractQuestHandler
{
    public _18805GoingThrifting() : base(18805)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(830070).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(830070).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(830660).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(830661).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(830520).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730525).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730522).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 830070)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 830660:
                case 830661:
                case 830520:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            {
                                if (var == 0)
                                    return SendQuestDialog(env, 1352);
                                else if (var == 2)
                                    return SendQuestDialog(env, 2375);
                                return false;
                            }
                        case DialogAction.SETPRO1:
                            {
                                return DefaultCloseDialog(env, 0, 1);
                            }
                        case DialogAction.SELECT_QUEST_REWARD:
                            {
                                ChangeQuestStep(env, 2, 2, true);
                                return SendQuestDialog(env, 5);
                            }
                    }
                    return false;
                case 730525:
                case 730522:
                    switch (dialogActionId)
                    {
                        case DialogAction.USE_OBJECT:
                            {
                                if (var == 1)
                                    return SendQuestDialog(env, 1693);
                                return false;
                            }
                        case DialogAction.SETPRO2:
                            {
                                return DefaultCloseDialog(env, 1, 2);
                            }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            switch (targetId)
            {
                case 830660:
                case 830661:
                case 830520:
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
