using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Hellboy, aion4Free
/// </summary>
public class _2946AbyssGeneralKnowledge : AbstractQuestHandler
{
    public _2946AbyssGeneralKnowledge() : base(2946)
    {
    }

    public override void Register()
    {
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterQuestNpc(204075).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204210).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204211).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204208).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204053).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 204075:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1011);
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                    }
                    break;
                case 204210:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                                return SendQuestDialog(env, 1352);
                            return false;
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 1, 2); // 2
                    }
                    break;
                case 204211:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 2)
                                return SendQuestDialog(env, 1693);
                            return false;
                        case DialogAction.SETPRO3:
                            return DefaultCloseDialog(env, 2, 3); // 3
                    }
                    break;
                case 204208:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 3)
                                return SendQuestDialog(env, 2034);
                            return false;
                        case DialogAction.SET_SUCCEED:
                            return DefaultCloseDialog(env, 3, 3, true, false); // reward
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204053)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 2945);
    }
}
