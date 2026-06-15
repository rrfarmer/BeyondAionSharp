using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu
/// </summary>
public class _2513TheStrangeCottage : AbstractQuestHandler
{
    public _2513TheStrangeCottage() : base(2513)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204732).AddOnQuestStart(questId); // Gnalin
        qe.RegisterQuestNpc(204732).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204827).AddOnTalkEvent(questId); // Hild
        qe.RegisterQuestNpc(204826).AddOnTalkEvent(questId); // Freki
        qe.RegisterQuestNpc(790022).AddOnTalkEvent(questId); // Byggvir
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204732)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 204826) // Freki
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1011);
                        return false;
                    case DialogAction.SETPRO1:
                        if (var == 0)
                        {
                            qs.SetRewardGroup(0);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return CloseDialogWindow(env);
                        }
                        break;
                }
            }
            if (targetId == 204827) // Hild
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1352);
                        return false;
                    case DialogAction.SETPRO2:
                        if (var == 0)
                        {
                            qs.SetRewardGroup(1);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return CloseDialogWindow(env);
                        }
                        break;
                }
            }
            if (targetId == 790022)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1693);
                        return false;
                    case DialogAction.SETPRO3:
                        if (var == 0)
                        {
                            qs.SetRewardGroup(2);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return CloseDialogWindow(env);
                        }
                        break;
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204732)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 10002);
                    default:
                        return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
