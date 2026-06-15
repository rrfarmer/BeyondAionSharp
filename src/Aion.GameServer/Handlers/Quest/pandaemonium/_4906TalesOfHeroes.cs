using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _4906TalesOfHeroes : AbstractQuestHandler
{
    public _4906TalesOfHeroes() : base(4906)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204211).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204211).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(205188).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(278003).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204057).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204211)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env, 182207075, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 205188)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                    {
                        return SendQuestDialog(env, 1352);
                    }
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    RemoveQuestItem(env, 182207075, 1);
                    GiveQuestItem(env, 182207076, 1);
                    return DefaultCloseDialog(env, 0, 1);
                }
            }
            if (targetId == 278003)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 1)
                    {
                        return SendQuestDialog(env, 1693);
                    }
                }
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    RemoveQuestItem(env, 182207076, 1);
                    GiveQuestItem(env, 182207077, 1);
                    return DefaultCloseDialog(env, 1, 2);
                }
            }
            if (targetId == 204057)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 2)
                    {
                        return SendQuestDialog(env, 2034);
                    }
                }
                else if (dialogActionId == DialogAction.SETPRO3)
                {
                    RemoveQuestItem(env, 182207077, 1);
                    GiveQuestItem(env, 182207078, 1);
                    qs.SetQuestVar(3);
                    return DefaultCloseDialog(env, 3, 3, true, false);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204211)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 2375);
                }
                RemoveQuestItem(env, 182207078, 1);
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
