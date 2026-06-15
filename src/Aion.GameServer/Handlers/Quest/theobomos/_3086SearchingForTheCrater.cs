using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Balthazar
/// </summary>
public class _3086SearchingForTheCrater : AbstractQuestHandler
{
    public _3086SearchingForTheCrater() : base(3086)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798132).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798132).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700418).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798201).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798132)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
        }

        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 700418:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                        {
                            if (player.GetInventory().GetItemCountByItemId(182208062) < 1)
                            {
                                qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                return true;
                            }
                            break;
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798201)
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
