using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _2988TheWiseInDisguise : AbstractQuestHandler
{
    public _2988TheWiseInDisguise() : base(2988)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204182).AddOnQuestStart(questId); // Heimdall
        qe.RegisterQuestNpc(204182).AddOnTalkEvent(questId); // Heimdall
        qe.RegisterQuestNpc(204338).AddOnTalkEvent(questId); // Utgar
        qe.RegisterQuestNpc(204213).AddOnTalkEvent(questId); // Brakan
        qe.RegisterQuestNpc(204146).AddOnTalkEvent(questId); // Kanensa
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204182) // Heimdall
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 204338) // Utgar
        {
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                    return DefaultCloseDialog(env, 0, 1);
            }
        }
        else if (targetId == 204213) // Brakan
        {
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 1)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1693);
                else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                    return DefaultCloseDialog(env, 1, 2);
            }
        }
        else if (targetId == 204146) // Kanensa
        {
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 2)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2034);
                else if (env.GetDialogActionId() == DialogAction.SELECT4_1)
                    return CheckQuestItems(env, 2, 2, true, 2035, 2120);
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                return SendQuestEndDialog(env);
            }
        }

        return false;
    }
}
