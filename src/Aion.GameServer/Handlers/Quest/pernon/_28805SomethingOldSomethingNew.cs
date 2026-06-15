using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu
/// </summary>
public class _28805SomethingOldSomethingNew : AbstractQuestHandler
{
    public _28805SomethingOldSomethingNew() : base(28805)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(830154).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(830154).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(830521).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(830662).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(830663).AddOnTalkEvent(questId);
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
            if (targetId == 830154)
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
                case 830521:
                case 830662:
                case 830663:
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
                case 830521:
                case 830662:
                case 830663:
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
