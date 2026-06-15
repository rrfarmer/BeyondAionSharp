using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _1479HelpingMemnes : AbstractQuestHandler
{
    private static readonly int[] npcIds = { 730020, 203912, 203898 }; // Demro, Memnes, Coeus

    public _1479HelpingMemnes() : base(1479)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(npcIds[0]).AddOnQuestStart(questId);
        foreach (int npcId in npcIds)
            qe.RegisterQuestNpc(npcId).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == npcIds[0]) // Demro
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
            if (targetId == npcIds[1]) // Memnes
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                        {
                            return SendQuestDialog(env, 1352);
                        }
                        else if (var == 2)
                        {
                            return SendQuestDialog(env, 2375);
                        }
                        return false;
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1); // 1
                    case DialogAction.SELECT_QUEST_REWARD:
                        ChangeQuestStep(env, 2, 2, true);
                        return SendQuestDialog(env, 5);
                }
            }
            else if (targetId == npcIds[2]) // Coeus
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
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == npcIds[1]) // Memnes
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
