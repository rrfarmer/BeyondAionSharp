using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;

namespace Aion.GameServer.Handlers.Quest;

public class _3074DangerousProbability : AbstractQuestHandler
{
    public _3074DangerousProbability() : base(3074)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798193).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798193).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798193) // Nagrunerk
            {
                if (dialogActionId == DialogAction.EXCHANGE_COIN)
                {
                    if (QuestService.StartQuest(env))
                    {
                        return SendQuestDialog(env, 1011);
                    }
                    else
                    {
                        return SendQuestSelectionDialog(env);
                    }
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 798193) // Nagrunerk
            {
                long kinahAmount = player.GetInventory().GetKinah();
                long angelsEye = player.GetInventory().GetItemCountByItemId(186000037);
                switch (dialogActionId)
                {
                    case DialogAction.EXCHANGE_COIN:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.SELECT1:
                        if (kinahAmount >= 1000 && angelsEye >= 1)
                        {
                            ChangeQuestStep(env, 0, 0, true);
                            qs.SetRewardGroup(0);
                            return SendQuestDialog(env, 5);
                        }
                        else
                        {
                            return SendQuestDialog(env, 1009);
                        }
                    case DialogAction.SELECT2:
                        if (kinahAmount >= 5000 && angelsEye >= 1)
                        {
                            ChangeQuestStep(env, 0, 0, true);
                            qs.SetRewardGroup(1);
                            return SendQuestDialog(env, 6);
                        }
                        else
                        {
                            return SendQuestDialog(env, 1009);
                        }
                    case DialogAction.SELECT3:
                        if (kinahAmount >= 25000 && angelsEye >= 1)
                        {
                            ChangeQuestStep(env, 0, 0, true);
                            qs.SetRewardGroup(2);
                            return SendQuestDialog(env, 7);
                        }
                        else
                        {
                            return SendQuestDialog(env, 1009);
                        }
                    case DialogAction.FINISH_DIALOG:
                        return SendQuestSelectionDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798193) // Nagrunerk
            {
                if (dialogActionId == DialogAction.SELECTED_QUEST_NOREWARD)
                {
                    switch (qs.GetRewardGroup())
                    {
                        case 0:
                            if (player.GetInventory().TryDecreaseKinah(1000) && QuestService.FinishQuest(env))
                            {
                                RemoveQuestItem(env, 186000037, 1);
                                ItemService.AddItem(player, 186000005, 1);
                            }
                            break;
                        case 1:
                            if (player.GetInventory().TryDecreaseKinah(5000) && QuestService.FinishQuest(env))
                            {
                                RemoveQuestItem(env, 186000037, 1);
                                ItemService.AddItem(player, 186000005, Rnd.Get(1, 3));
                            }
                            break;
                        case 2:
                            if (player.GetInventory().TryDecreaseKinah(25000) && QuestService.FinishQuest(env))
                            {
                                RemoveQuestItem(env, 186000037, 1);
                                ItemService.AddItem(player, 186000005, Rnd.Get(1, 6));
                            }
                            break;
                    }
                    return CloseDialogWindow(env);
                }
                else
                {
                    QuestService.AbandonQuest(player, questId);
                }
            }
        }
        return false;
    }
}
