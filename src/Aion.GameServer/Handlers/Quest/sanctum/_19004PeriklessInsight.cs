using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>Java parity: data/handlers/quest/sanctum/_19004PeriklessInsight (Rolandas, vlog). Talk-chain Perikles->Jucleas->Lavirintos->Mysteris.</summary>
public class _19004PeriklessInsight : AbstractQuestHandler
{
    private static readonly int[] npcs = { 203757, 203752, 203701, 798500 };

    public _19004PeriklessInsight()
        : base(19004)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203757).AddOnQuestStart(questId);
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203757) // Perikles
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
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
            if (targetId == 203752) // Jucleas
            {
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
            }
            else if (targetId == 203701) // Lavirintos
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                        {
                            return SendQuestDialog(env, 1693);
                        }
                        return false;
                    case DialogAction.SETPRO2:
                        return DefaultCloseDialog(env, 1, 2); // 2
                }
            }
            else if (targetId == 798500) // Mysteris
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 2)
                        {
                            return SendQuestDialog(env, 2375);
                        }
                        return false;
                    case DialogAction.SELECT_QUEST_REWARD:
                        ChangeQuestStep(env, 2, 2, true); // reward
                        return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798500) // Mysteris
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
