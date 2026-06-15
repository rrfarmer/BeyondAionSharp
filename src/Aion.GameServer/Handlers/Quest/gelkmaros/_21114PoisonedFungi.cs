using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _21114PoisonedFungi : AbstractQuestHandler
{
    public _21114PoisonedFungi() : base(21114)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799282).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(700727).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700729).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700728).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799282).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799405).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(216563).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799282)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 799282)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                        return SendQuestDialog(env, 1011);
                    else if (qs.GetQuestVarById(0) == 1)
                        return SendQuestDialog(env, 1352);
                    else if (qs.GetQuestVarById(0) == 3)
                        return SendQuestDialog(env, 2034);
                }
                else if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                    return CheckQuestItems(env, 0, 1, false, 10000, 10001);
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    GiveQuestItem(env, 182207862, 1);
                    return DefaultCloseDialog(env, 1, 2);
                }
                else if (dialogActionId == DialogAction.SETPRO4)
                    return DefaultCloseDialog(env, 3, 4);
            }
            else if (targetId == 700727 || targetId == 700728)
            {
                if (qs.GetQuestVarById(0) == 0)
                    return true;
            }
            else if (targetId == 700729)
            {
                if (qs.GetQuestVarById(0) == 2)
                {
                    RemoveQuestItem(env, 182207862, 1);
                    ChangeQuestStep(env, 2, 3);
                    return true;
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799282)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        return DefaultOnKillEvent(env, 216563, 4, true);
    }
}
