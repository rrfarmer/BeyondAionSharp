using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Gigi, Shepper, Pad
    /// </summary>
    public class _1563TheLegendofVindachinerk : AbstractQuestHandler
    {
        public _1563TheLegendofVindachinerk() : base(1563)
        {
        }

        public override void Register()
        {
            qe.RegisterQuestNpc(798096).AddOnQuestStart(questId); // Poporinerk
            qe.RegisterQuestNpc(798096).AddOnTalkEvent(questId); // Poporinerk
            qe.RegisterQuestNpc(279005).AddOnTalkEvent(questId); // Kohrunerk
            qe.RegisterQuestItem(182201729, questId); // Jaiorunerk's Diary
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            int targetId = env.GetTargetId();
            int dialogActionId = env.GetDialogActionId();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);

            if (qs == null || qs.IsStartable())
            {
                if (targetId == 798096)
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 4762);
                    else
                        return SendQuestStartDialog(env);
                }
            }

            if (qs == null)
                return false;

            int var = qs.GetQuestVarById(0);

            if (qs.GetStatus() == QuestStatus.START && var == 1)
            {
                switch (targetId)
                {
                    case 798096: // Poporinerk
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 1352);
                            case DialogAction.SETPRO2:
                                if (player.GetInventory().GetItemCountByItemId(182201729) < 1)
                                {
                                    return SendQuestDialog(env, 1353);
                                }
                                else
                                {
                                    player.GetInventory().DecreaseByItemId(182201729, 1);
                                    qs.SetQuestVar(2);
                                    qs.SetStatus(QuestStatus.REWARD);
                                    UpdateQuestStatus(env);
                                    return SendQuestDialog(env, 5);
                                }
                        }
                        break;
                    case 279005: // Kohrunerk
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 1438);
                            case DialogAction.SETPRO2:
                                if (player.GetInventory().GetItemCountByItemId(182201729) < 1)
                                {
                                    return SendQuestDialog(env, 1439);
                                }
                                else
                                {
                                    player.GetInventory().DecreaseByItemId(182201729, 1);
                                    qs.SetQuestVar(2);
                                    qs.SetStatus(QuestStatus.REWARD);
                                    UpdateQuestStatus(env);
                                    return SendQuestDialog(env, 6);
                                }
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 798096)
                    qs.SetRewardGroup(0);
                else if (targetId == 279005)
                    qs.SetRewardGroup(1);
                else
                    return false;
                return SendQuestEndDialog(env);
            }
            return false;
        }

        public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
        {
            Player player = env.GetPlayer();
            int id = item.GetItemTemplate().GetTemplateId();

            if (id != 182201729)
                return HandlerResult.UNKNOWN;
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null)
                return HandlerResult.UNKNOWN;
            ChangeQuestStep(env, 0, 1);

            return HandlerResult.SUCCESS;
        }
    }
}
