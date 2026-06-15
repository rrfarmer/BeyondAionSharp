using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author vlog
/// </summary>
public class _2303DaevaWheresMyHerb : AbstractQuestHandler
{
    public _2303DaevaWheresMyHerb() : base(2303)
    {
    }

    public override void Register()
    {
        int[] mobs = { 211298, 211305, 211304, 211297 };
        qe.RegisterQuestNpc(798082).AddOnQuestStart(questId); // Bicorunerk
        qe.RegisterQuestNpc(798082).AddOnTalkEvent(questId); // Bicorunerk
        qe.RegisterQuestNpc(204378).AddOnTalkEvent(questId); // Favyr
        foreach (int mob in mobs)
        {
            qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798082)
            { // Bicorunerk
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 4762);
                    case DialogAction.QUEST_ACCEPT_1:
                        QuestService.StartQuest(env);
                        break;
                }
                return base.OnDialogEvent(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 798082)
            { // Bicorunerk
                if (dialogActionId == DialogAction.FINISH_DIALOG)
                {
                    return SendQuestSelectionDialog(env);
                }
                else if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    if (var == 0)
                    {
                        return SendQuestDialog(env, 1003);
                    }
                    else
                    {
                        return SendQuestSelectionDialog(env);
                    }
                }
                else if (dialogActionId == DialogAction.SETPRO10)
                {
                    ChangeQuestStep(env, 0, 11); // 11
                    qs.SetRewardGroup(0);
                    return SendQuestDialog(env, 1012);
                }
                else if (dialogActionId == DialogAction.SETPRO20)
                {
                    ChangeQuestStep(env, 0, 21); // 21
                    qs.SetRewardGroup(1);
                    return SendQuestDialog(env, 1097);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 204378)
            { // Favyr
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        if (var == 15)
                        {
                            return SendQuestDialog(env, 1353);
                        }
                        else if (var == 25)
                        {
                            return SendQuestDialog(env, 1438);
                        }
                        return false;
                    default:
                    {
                        return SendQuestEndDialog(env);
                    }
                }
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVars().GetQuestVars();
            int[] daru = { 211298, 211305 };
            int[] ettins = { 211304, 211297 };
            if (var >= 11 && var < 15)
            {
                return DefaultOnKillEvent(env, daru, 10, 15); // 15
            }
            else if (var == 15)
            {
                switch (targetId)
                {
                    case 211298:
                    case 211305:
                        qs.SetQuestVar(15);
                        qs.SetStatus(QuestStatus.REWARD); // reward
                        UpdateQuestStatus(env);
                        return true;
                }
            }
            else if (var >= 21 && var < 25)
            {
                return DefaultOnKillEvent(env, ettins, 20, 25); // 25
            }
            else if (var == 25)
            {
                switch (targetId)
                {
                    case 211304:
                    case 211297:
                        qs.SetQuestVar(25);
                        qs.SetStatus(QuestStatus.REWARD); // reward
                        UpdateQuestStatus(env);
                        return true;
                }
            }
        }
        return false;
    }
}
