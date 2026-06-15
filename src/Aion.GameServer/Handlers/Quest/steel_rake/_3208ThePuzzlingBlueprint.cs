using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/**
 * @author sky123
 */
public class _3208ThePuzzlingBlueprint : AbstractQuestHandler
{
    public _3208ThePuzzlingBlueprint() : base(3208)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(730195).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(730195).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798026).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203830).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798320).AddOnTalkEvent(questId);
        qe.RegisterCanAct(questId, 730195);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 730195)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, QuestService.CheckStartConditions(player, questId, false) ? 1011 : 1004);
                else
                    return SendQuestStartDialog(env, 182209088, 1);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 798026)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1352);
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (targetId == 203830)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1693);
                    case DialogAction.SETPRO2:
                        return DefaultCloseDialog(env, 1, 2);
                }
            }
            else if (targetId == 798320)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 2375);
                    case DialogAction.SELECT_QUEST_REWARD:
                        return DefaultCloseDialog(env, 2, 2, true, true);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            return SendQuestEndDialog(env);
        }
        return false;
    }

    public override bool OnCanAct(QuestEnv env, QuestActionType questEventType, params object[] objects)
    {
        return env.GetTargetId() == 730195 || base.OnCanAct(env, questEventType, objects);
    }
}
