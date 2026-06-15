using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _21244SearchForTheBiolab : AbstractQuestHandler
{
    public _21244SearchForTheBiolab() : base(21244)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799317).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799318).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799320).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799317).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799317)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 799318)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                        return SendQuestDialog(env, 1352);
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (targetId == 799320)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 1)
                        return SendQuestDialog(env, 1693);
                }
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    qs.SetQuestVar(2);
                    GiveQuestItem(env, 182207924, 1);
                    return DefaultCloseDialog(env, 2, 2, true, false);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799317)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 2375);
                RemoveQuestItem(env, 182207924, 1);
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
