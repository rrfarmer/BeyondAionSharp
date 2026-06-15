using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

public class _1467TheFourLeaders : AbstractQuestHandler
{
    private static readonly int npcId = 204045;
    private static readonly int[] mobIds = { 211696, 211697, 211698, 211699 };

    public _1467TheFourLeaders() : base(1467)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(npcId).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(npcId).AddOnTalkEvent(questId);
        foreach (int mobId in mobIds)
            qe.RegisterQuestNpc(mobId).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == npcId)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 4762);
                    case DialogAction.QUEST_ACCEPT_1:
                        QuestService.StartQuest(env);
                        return SendQuestDialog(env, 1011);
                    default:
                    {
                        return SendQuestStartDialog(env);
                    }
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == npcId)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.SETPRO1:
                        qs.SetRewardGroup(0);
                        return DefaultCloseDialog(env, 0, 1);
                    case DialogAction.SETPRO2:
                        qs.SetRewardGroup(1);
                        return DefaultCloseDialog(env, 0, 2);
                    case DialogAction.SETPRO3:
                        qs.SetRewardGroup(2);
                        return DefaultCloseDialog(env, 0, 3);
                    case DialogAction.SETPRO4:
                        qs.SetRewardGroup(3);
                        return DefaultCloseDialog(env, 0, 4);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == npcId)
                return SendQuestEndDialog(env);
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.GetStatus() != QuestStatus.START)
        {
            return false;
        }

        int targetId = env.GetTargetId();
        int var0 = qs.GetQuestVarById(0);
        if (targetId == mobIds[0] && var0 == 1 || targetId == mobIds[1] && var0 == 2 || targetId == mobIds[2] && var0 == 3
            || targetId == mobIds[3] && var0 == 4)
        {
            qs.SetStatus(QuestStatus.REWARD);
            UpdateQuestStatus(env);
            return true;
        }

        return false;
    }
}
