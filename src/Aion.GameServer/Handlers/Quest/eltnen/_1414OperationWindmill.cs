using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Xitanium
/// </summary>
public class _1414OperationWindmill : AbstractQuestHandler
{
    public _1414OperationWindmill() : base(1414)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203989).AddOnQuestStart(questId); // Tumblusen
        qe.RegisterQuestNpc(203989).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700175).AddOnTalkEvent(questId); // Old Gear
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
            if (targetId == 203989) // Tumblusen
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                {
                    if (GiveQuestItem(env, 182201349, 1))
                        return SendQuestStartDialog(env);
                    else
                        return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            return SendQuestEndDialog(env);
        }
        else if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
        {
            switch (targetId)
            {
                case 700175: // Old Gear
                    if (qs.GetQuestVarById(0) == 0 && env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    {
                        qs.SetQuestVar(1);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        RemoveQuestItem(env, 182201349, 1);
                        return true;
                    }
                    break;
            }
        }
        return false;
    }
}
