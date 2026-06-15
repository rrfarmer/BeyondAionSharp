using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/**
 * @author Ritsu
 */
public class _30154FleshtoStone : AbstractQuestHandler
{
    public _30154FleshtoStone() : base(30154)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799234).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799234).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204433).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799234)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799234)
                return SendQuestEndDialog(env);
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 204433)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1352);
                        return false;
                    case DialogAction.SETPRO1:
                        if (var == 0)
                            return DefaultCloseDialog(env, 0, 1);
                        break;
                }
            }
            if (targetId == 799234)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                            return SendQuestDialog(env, 2375);
                        return false;
                    case DialogAction.SELECT_QUEST_REWARD:
                        if (var == 1)
                            return DefaultCloseDialog(env, 1, 1, true, true);
                        break;
                }
            }
        }
        return false;
    }
}
