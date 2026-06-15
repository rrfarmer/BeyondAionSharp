using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Gigi, vlog, Pad
/// </summary>
public class _29000ExpertEssencetappersTest : AbstractQuestHandler
{
    private const int itemId1 = 152003004;
    private const int itemId2 = 152003005;
    private const int itemId3 = 152003006;

    public _29000ExpertEssencetappersTest() : base(29000)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204096).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204096).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204097).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204096) // Latatusk
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 204097: // Relir
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            if (!player.GetInventory().IsFullSpecialCube())
                            {
                                return DefaultCloseDialog(env, 0, 1, 122001250, 1, 0, 0); // 1
                            }
                            break;
                    }
                    break;
                case 204096: // Latatusk
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            return false;
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            long itemCount1 = player.GetInventory().GetItemCountByItemId(itemId1);
                            long itemCount2 = player.GetInventory().GetItemCountByItemId(itemId2);
                            long itemCount3 = player.GetInventory().GetItemCountByItemId(itemId3);
                            if (itemCount1 >= 1 && itemCount2 >= 1 && itemCount3 >= 1)
                            {
                                RemoveQuestItem(env, itemId1, itemCount1);
                                RemoveQuestItem(env, itemId2, itemCount2);
                                RemoveQuestItem(env, itemId3, itemCount3);
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                return SendQuestDialog(env, 5);
                            }
                            else
                            {
                                return SendQuestDialog(env, 10001);
                            }
                        case DialogAction.FINISH_DIALOG:
                            return DefaultCloseDialog(env, 1, 1);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204096)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
