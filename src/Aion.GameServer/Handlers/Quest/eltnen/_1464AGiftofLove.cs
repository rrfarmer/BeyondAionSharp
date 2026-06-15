using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Balthazar
/// </summary>
public class _1464AGiftofLove : AbstractQuestHandler
{
    public _1464AGiftofLove() : base(1464)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204424).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204424).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203755).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204424)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 204424:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            {
                                long itemCount1 = player.GetInventory().GetItemCountByItemId(152000455);
                                if (qs.GetQuestVarById(0) == 0 && itemCount1 >= 15)
                                {
                                    qs.SetQuestVar(0);
                                    qs.SetStatus(QuestStatus.REWARD);
                                    RemoveQuestItem(env, 152000455, itemCount1);
                                    UpdateQuestStatus(env);
                                    return SendQuestDialog(env, 10000);
                                }
                                else
                                    return SendQuestDialog(env, 10001);
                            }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203755)
            {
                if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
