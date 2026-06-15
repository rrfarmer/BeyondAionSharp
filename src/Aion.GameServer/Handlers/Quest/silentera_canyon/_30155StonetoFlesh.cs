using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu
/// </summary>
public class _30155StonetoFlesh : AbstractQuestHandler
{
    public _30155StonetoFlesh() : base(30155)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799234).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799234).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204433).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204304).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799234)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 204304)
            {
                if (var == 3)
                    return SendQuestEndDialog(env);
            }
            else if (targetId == 799234)
            {
                return SendQuestEndDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 799234)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                            return SendQuestDialog(env, 1693);
                        return false;
                    case DialogAction.SETPRO2:
                        if (var == 1)
                        {
                            ChangeQuestStep(env, 1, 2);
                            return SendQuestDialog(env, 2375);
                        }
                        return false;
                    case DialogAction.SELECT_QUEST_REWARD:
                        if (var == 2)
                        {
                            RemoveQuestItem(env, 182209252, 1);
                            qs.SetRewardGroup(0);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return SendQuestDialog(env, 6);
                        }
                        break;
                }
            }
            else if (targetId == 204433)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1352);
                        return false;
                    case DialogAction.SETPRO1:
                        if (var == 0)
                            return DefaultCloseDialog(env, 0, 1, 182209252, 1);
                        break;
                }
            }
            else if (targetId == 204304)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                            return SendQuestDialog(env, 2034);
                        return false;
                    case DialogAction.SETPRO3:
                        if (var == 1)
                            ChangeQuestStep(env, 1, 3);
                        return SendQuestDialog(env, 2375);
                    case DialogAction.SELECT_QUEST_REWARD:
                        if (var == 3)
                        {
                            RemoveQuestItem(env, 182209252, 1);
                            qs.SetRewardGroup(1);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                        }
                        break;
                }
            }
        }
        return false;
    }
}
