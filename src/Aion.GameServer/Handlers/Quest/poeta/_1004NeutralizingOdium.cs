using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author MrPoke, Majka
/// </summary>
public class _1004NeutralizingOdium : AbstractQuestHandler
{
    public _1004NeutralizingOdium() : base(1004)
    {
    }

    public override void Register()
    {
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterQuestNpc(203082).AddOnTalkEvent(questId); // Tula
        qe.RegisterQuestNpc(700030).AddOnTalkEvent(questId); // The Cauldron
        qe.RegisterQuestNpc(790001).AddOnTalkEvent(questId); // Pernos
        qe.RegisterQuestNpc(203067).AddOnTalkEvent(questId); // Kalio
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 203082)
            { // Tula
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1011);
                        else if (var == 5)
                            return SendQuestDialog(env, 2034);
                        return false;
                    case DialogAction.SELECT1_1_1:
                        if (var == 0)
                            PlayQuestMovie(env, 19);
                        return false;
                    case DialogAction.SETPRO1:
                        qs.SetQuestVarById(0, var + 1);
                        UpdateQuestStatus(env);
                        SendQuestSelectionDialog(env);
                        return true;
                    case DialogAction.SETPRO3:
                        if (var == 5)
                        {
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            SendQuestSelectionDialog(env);
                            return true;
                        }
                        break;
                }
            }
            else if (targetId == 700030 && var == 1 || var == 4)
            { // The Cauldron
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        if (var == 1)
                        {
                            if (GiveQuestItem(env, 182200005, 1))
                                qs.SetQuestVarById(0, var + 1);
                        }
                        else if (var == 4)
                        {
                            qs.SetQuestVarById(0, var + 1);
                            RemoveQuestItem(env, 182200005, 1);
                        }
                        UpdateQuestStatus(env);
                        return false;
                }
            }
            else if (targetId == 790001)
            { // Pernos
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 2)
                            return SendQuestDialog(env, 1352);
                        else if (var == 3)
                            return SendQuestDialog(env, 1693);
                        else if (var == 11)
                            return SendQuestDialog(env, 1694);
                        return false;
                    case DialogAction.SETPRO2:
                        if (var == 2)
                        {
                            qs.SetQuestVarById(0, var + 1);
                            UpdateQuestStatus(env);
                            SendQuestSelectionDialog(env);
                            return true;
                        }
                        return false;
                    case DialogAction.SETPRO3:
                        if (var == 11)
                        {
                            if (!GiveQuestItem(env, 182200006, 1))
                                return true;
                            qs.SetQuestVarById(0, 4);
                            UpdateQuestStatus(env);
                            RemoveQuestItem(env, 182200006, 1);
                            SendQuestSelectionDialog(env);
                            return true;
                        }
                        return false;
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        if (QuestService.CollectItemCheck(env, true))
                        {
                            qs.SetQuestVarById(0, 11);
                            UpdateQuestStatus(env);
                            return SendQuestDialog(env, 1694);
                        }
                        else
                            return SendQuestDialog(env, 1779);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203067) // Kalio
                return SendQuestEndDialog(env);
        }
        return false;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 1100);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 1100);
    }
}
