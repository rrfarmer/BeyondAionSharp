using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rikka
/// </summary>
public class _30365ARayOfHope : AbstractQuestHandler
{
    public _30365ARayOfHope() : base(30365)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204241).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203574).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(278040).AddOnTalkEvent(questId);
        qe.RegisterQuestItem(182209824, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            return false;
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 204241: // Annemari
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                    }
                    break;
                case 203574: // Arekedil
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                            {
                                return SendQuestDialog(env, 1693);
                            }
                            else if (var == 3)
                            {
                                return SendQuestDialog(env, 2375);
                            }
                            return false;
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 1, 2); // 2
                        case DialogAction.SELECT_QUEST_REWARD:
                            ChangeQuestStep(env, 3, 3, true); // reward
                            return SendQuestEndDialog(env);
                    }
                    break;
                case 278040: // Haug
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 2)
                            {
                                return SendQuestDialog(env, 2034);
                            }
                            return false;
                        case DialogAction.SETPRO3:
                            return DefaultCloseDialog(env, 2, 3); // 3
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203574) // Arekedil
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            return HandlerResultExtensions.FromBoolean(QuestService.StartQuest(env));
        }
        return HandlerResult.FAILED;
    }
}
