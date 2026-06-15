using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Balthazar
/// </summary>
public class _1612LepharistSecrets : AbstractQuestHandler
{
    public _1612LepharistSecrets() : base(1612)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204530).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204530).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700352).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204530)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                    return SendQuestStartDialog(env);
            }
        }

        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 700352:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                        {
                            switch (qs.GetQuestVarById(0))
                            {
                                case 0:
                                case 1:
                                case 2:
                                {
                                    return UseQuestObject(env, var, var + 1, false, true); // var++
                                }
                                case 3:
                                {
                                    return UseQuestObject(env, 3, 3, true, true); // reward
                                }
                            }
                            break;
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204530)
            {
                if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
