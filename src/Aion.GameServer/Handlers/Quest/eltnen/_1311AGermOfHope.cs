using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Mr.Poke, Nephis and quest helper team
/// </summary>
public class _1311AGermOfHope : AbstractQuestHandler
{
    public _1311AGermOfHope() : base(1311)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203997).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203997).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700164).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203997).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203997)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (env.GetDialogActionId() == DialogAction.SELECT1_1_1)
                {
                    if (GiveQuestItem(env, 182201305, 1))
                        return SendQuestDialog(env, 4);
                    else
                        return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 700164:
                    if (qs.GetQuestVarById(0) == 0 && env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    {
                        RemoveQuestItem(env, 182201305, 1);
                        qs.SetStatus(QuestStatus.REWARD);
                        qs.SetQuestVarById(0, 3);
                        UpdateQuestStatus(env);
                        return true;
                    }
                    return false;
                case 203997:
                    if (qs.GetQuestVarById(0) == 1)
                    {
                        if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 2375);
                        else if (env.GetDialogActionId() == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                        {
                            RemoveQuestItem(env, 182201305, 1);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return SendQuestDialog(env, 5);
                        }
                        else
                            return SendQuestEndDialog(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203997)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
