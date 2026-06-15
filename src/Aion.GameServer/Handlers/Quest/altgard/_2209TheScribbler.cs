using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Mr. Poke
/// </summary>
public class _2209TheScribbler : AbstractQuestHandler
{
    public _2209TheScribbler() : base(2209)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203555).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203555).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203562).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203592).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
        {
            if (targetId == 203555)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203562:
                    if (qs.GetQuestVarById(0) == 0)
                    {
                        if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1352);
                        else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                        {
                            return DefaultCloseDialog(env, 0, 1); // 1
                        }
                    }
                    break;
                case 203572:
                    if (qs.GetQuestVarById(0) == 1)
                    {
                        if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1693);
                        else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                        {
                            return DefaultCloseDialog(env, 1, 2); // 2
                        }
                    }
                    break;
                case 203592:
                    if (qs.GetQuestVarById(0) == 2)
                    {
                        if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 2034);
                        else if (env.GetDialogActionId() == DialogAction.SETPRO3)
                        {
                            return DefaultCloseDialog(env, 2, 3); // 3
                        }
                    }
                    break;
                case 203555:
                    if (qs.GetQuestVarById(0) == 3)
                    {
                        if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 2375);
                        else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                        {
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return SendQuestEndDialog(env);
                        }
                        else
                            return SendQuestEndDialog(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203555)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
