using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu
/// </summary>
public class _30600FightOfTheNavigators : AbstractQuestHandler
{
    public _30600FightOfTheNavigators() : base(30600)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(205842).AddOnQuestStart(questId); // Ancanus
        qe.RegisterQuestNpc(205842).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(800325).AddOnTalkEvent(questId); // Hejitor,Jerot
        qe.RegisterQuestNpc(219256).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(219257).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(219264).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();

        int dialogActionId = env.GetDialogActionId();
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 205842)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0); // the station is important
            switch (targetId)
            {
                case 800325:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1352);
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 0);
                    }
                    return false;
                case 205842:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 2375);
                        case DialogAction.SELECT_QUEST_REWARD:
                            if (var == 1)
                            {
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                return SendQuestDialog(env, 5);
                            }
                            break;
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            switch (targetId)
            {
                case 205842:
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = 0;
        if (qs == null || qs.GetStatus() != QuestStatus.START)
        {
            return false;
        }
        else
        {
            if (env.GetVisibleObject() is Npc npc)
                targetId = npc.GetNpcId();
            switch (targetId)
            {
                case 219256:
                case 219257:
                    if (qs.GetQuestVarById(0) == 0)
                    {
                        qs.SetQuestVar(1);
                        UpdateQuestStatus(env);
                        return true;
                    }
                    return false;
                case 219264:
                    if (qs.GetQuestVarById(0) == 1)
                    {
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return true;
                    }
                    break;
            }
        }
        return false;
    }
}
