using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author stpavel, Neon
/// </summary>
public class _2332MeatyTreats : AbstractQuestHandler
{
    public _2332MeatyTreats() : base(2332)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798084).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798084).AddOnTalkEvent(questId);
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
            if (targetId == 798084)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 798084)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (QuestService.CollectItemCheck(env, false))
                            return SendQuestDialog(env, 1352);
                        else
                            return SendQuestDialog(env, 1693);
                    case DialogAction.SETPRO1:
                    case DialogAction.SETPRO2:
                    case DialogAction.SETPRO3:
                        if (QuestService.CollectItemCheck(env, true))
                        {
                            qs.SetQuestVar(1);
                            qs.SetRewardGroup(env.GetDialogActionId() - DialogAction.SETPRO1);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return SendQuestDialog(env, DialogPageExtensions.GetRewardPageByIndex(qs.GetRewardGroup()).Id());
                        }
                        break;
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD && targetId == 798084)
        {
            return SendQuestEndDialog(env);
        }
        return false;
    }
}
