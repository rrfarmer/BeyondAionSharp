using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author zhkchi
/// </summary>
public class _21081A_Helping_Hand : AbstractQuestHandler
{
    public _21081A_Helping_Hand() : base(21081)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799225).AddOnQuestStart(questId); // Richelle
        qe.RegisterQuestNpc(799225).AddOnTalkEvent(questId); // Richelle
        qe.RegisterQuestNpc(799332).AddOnTalkEvent(questId); // Agovard
        qe.RegisterQuestNpc(799217).AddOnTalkEvent(questId); // Renato
        qe.RegisterQuestNpc(799202).AddOnTalkEvent(questId); // Ipses
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799225)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    default:
                        return SendQuestStartDialog(env, 182214017, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 799332: // Brontes
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1353);
                        case DialogAction.SELECT2_1:
                            return SendQuestDialog(env, 1353);
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1);
                    }
                    return false;
                }
                case 799217: // Pilipides
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1693);
                        case DialogAction.SELECT3_1:
                            return SendQuestDialog(env, 1694);
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 1, 2);
                    }
                    return false;
                }
                case 799202: // Drenia
                {
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 2375);
                        case DialogAction.SELECT_QUEST_REWARD:
                            return DefaultCloseDialog(env, 2, 3, true, true);
                    }
                    break;
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799202) // Drenia
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.SELECT_QUEST_REWARD:
                        return SendQuestDialog(env, 5);
                    default:
                        return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
