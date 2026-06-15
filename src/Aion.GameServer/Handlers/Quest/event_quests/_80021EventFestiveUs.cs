using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rolandas
/// </summary>
public class _80021EventFestiveUs : AbstractQuestHandler
{
    public _80021EventFestiveUs() : base(80021)
    {
    }

    public override void Register()
    {
        int[] npcs = { 799784, 799783, 203618, 203650 };
        qe.RegisterQuestNpc(799784).AddOnQuestStart(questId);
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            if (env.GetTargetId() == 799784)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT || env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestNoneDialog(env, 799784, 182214014, 1);
            }
            return false;
        }

        int var = qs.GetQuestVarById(0);

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (env.GetTargetId() == 799783)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT || env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    DefaultCloseDialog(env, 0, 1, 182214015, 2, 182214014, 1);
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
            else if (env.GetTargetId() == 203618 && var == 1)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT || env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1693);
                else if (env.GetDialogActionId() == DialogAction.SELECT3_1)
                {
                    SendEmotion(env, (Creature)env.GetVisibleObject(), EmotionId.NO, true);
                    return SendQuestDialog(env, 1694);
                }
                else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                {
                    DefaultCloseDialog(env, 1, 2, 0, 0, 182214015, 1);
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
            else if (env.GetTargetId() == 203650 && var == 2)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2034);
                else if (env.GetDialogActionId() == DialogAction.SELECT4_1)
                {
                    SendEmotion(env, (Creature)env.GetVisibleObject(), EmotionId.PANIC, true);
                    return SendQuestDialog(env, 2035);
                }
                else if (env.GetDialogActionId() == DialogAction.SETPRO3)
                    return DefaultCloseDialog(env, 2, 3, true, false, 0, 0, 182214015, 1);
                else
                    return SendQuestStartDialog(env);
            }
        }

        return SendQuestRewardDialog(env, 799784, 2375);
    }
}
