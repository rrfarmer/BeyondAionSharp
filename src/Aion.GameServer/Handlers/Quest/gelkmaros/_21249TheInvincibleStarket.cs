using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _21249TheInvincibleStarket : AbstractQuestHandler
{
    public _21249TheInvincibleStarket() : base(21249)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799416).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799529).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799417).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799416)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 799416)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                        return SendQuestDialog(env, 1011);
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    Npc npc = (Npc)env.GetVisibleObject();
                    npc.GetController().DeleteAndScheduleRespawn();
                    SpawnForFiveMinutes(799529, npc.GetPosition());
                    return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (targetId == 799529)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 1)
                        return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.SET_SUCCEED)
                {
                    ChangeQuestStep(env, 0, 1);
                    env.GetVisibleObject().GetController().DeleteAndScheduleRespawn();
                    return DefaultCloseDialog(env, 1, 1, true, false);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799417)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
