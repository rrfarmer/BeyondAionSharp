using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author MrPoke, Xitanium
/// </summary>
public class _1483HarumonerksRequest : AbstractQuestHandler
{
    public _1483HarumonerksRequest() : base(1483)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798126).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798126).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203940).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203944).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798127).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 798126)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 203940)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    ChangeQuestStep(env, 0, 1);
                    GiveQuestItem(env, workItems[0].GetItemId(), workItems[0].GetCount());
                    return CloseDialogWindow(env);
                }
            }
        }
        else if (targetId == 203944)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 1)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1693);
                else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                {
                    ChangeQuestStep(env, 1, 2);
                    GiveQuestItem(env, workItems[^1].GetItemId(), workItems[^1].GetCount());
                    return CloseDialogWindow(env);
                }
            }
        }
        else if (targetId == 798127)
        {
            if (qs != null)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT && qs.GetStatus() == QuestStatus.START)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD && qs.GetStatus() != QuestStatus.COMPLETE)
                    ChangeQuestStep(env, 2, 3, true);
                return SendQuestEndDialog(env);
            }
        }
        return base.OnDialogEvent(env);
    }
}
