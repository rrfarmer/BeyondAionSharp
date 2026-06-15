using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author dta3000, Gigi
/// </summary>
public class _11010AngelToTheWounded : AbstractQuestHandler
{
    public _11010AngelToTheWounded() : base(11010)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798931).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798931).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799071).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798906).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730323).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc) env.GetVisibleObject()).GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798931)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 799071:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1352);
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                    }
                    break;
                case 798906:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                                return SendQuestDialog(env, 1693);
                            return false;
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 1, 2); // 2
                    }
                    break;
                case 730323:
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                        return UseQuestObject(env, 2, 3, true, 0); // reward
                    return false;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799071)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 2375);
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
