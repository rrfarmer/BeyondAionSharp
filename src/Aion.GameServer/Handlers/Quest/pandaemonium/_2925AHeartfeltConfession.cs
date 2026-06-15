using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _2925AHeartfeltConfession : AbstractQuestHandler
{
    public _2925AHeartfeltConfession() : base(2925)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204261).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204261).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204235).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204127).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204193).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204261)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 204235)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                    {
                        if (player.GetEquipment().GetEquippedItemsByItemId(110100288).Count != 0)
                            return SendQuestDialog(env, 1011);
                        else
                            return SendQuestDialog(env, 1097);
                    }
                    else if (qs.GetQuestVarById(0) == 4)
                    {
                        return SendQuestDialog(env, 2375);
                    }
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    return DefaultCloseDialog(env, 0, 1);
                }
                else if (dialogActionId == DialogAction.SETPRO5)
                {
                    return DefaultCloseDialog(env, 4, 5);
                }
            }
            else if (targetId == 204261)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 1)
                        return SendQuestDialog(env, 1352);
                    else if (qs.GetQuestVarById(0) == 5)
                        return SendQuestDialog(env, 2716);
                }
                else if (dialogActionId == DialogAction.SETPRO2)
                {
                    RemoveQuestItem(env, 110100288, 1);
                    return DefaultCloseDialog(env, 1, 2);
                }
                else if (dialogActionId == DialogAction.SELECT6_1)
                {
                    ChangeQuestStep(env, 5, 5, true);
                    return SendQuestDialog(env, 10002);
                }
            }
            else if (targetId == 204127)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 2)
                        return SendQuestDialog(env, 1693);
                }
                else if (dialogActionId == DialogAction.SETPRO3)
                {
                    return DefaultCloseDialog(env, 2, 3);
                }
            }
            else if (targetId == 204193)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 3)
                        return SendQuestDialog(env, 2034);
                }
                else if (dialogActionId == DialogAction.SETPRO4)
                {
                    return DefaultCloseDialog(env, 3, 4);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204261)
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
