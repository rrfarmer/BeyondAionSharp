using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Mr.Poke, Nephis and quest helper team
/// </summary>
public class _2484OurManInElysea : AbstractQuestHandler
{
    public _2484OurManInElysea() : base(2484)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204407).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204407).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700267).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203331).AddOnTalkEvent(questId);
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
            if (targetId == 204407)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                {
                    if (GiveQuestItem(env, 182204205, 1))
                        return SendQuestStartDialog(env);
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
                case 700267:
                    if (qs.GetQuestVarById(0) == 0 && env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    {
                        qs.SetQuestVarById(0, 1);
                        UpdateQuestStatus(env);
                        RemoveQuestItem(env, 182204205, 1);
                    }
                    return false;
                case 203331:
                    if (qs.GetQuestVarById(0) == 1)
                    {
                        if (env.GetDialogActionId() == DialogAction.SELECTED_QUEST_NOREWARD)
                            return SendQuestDialog(env, 5);
                        else if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                        {
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
            if (targetId == 203331)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
