using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rhys2002, zhkchi
/// </summary>
public class _1158VillageSealFound : AbstractQuestHandler
{
    public _1158VillageSealFound() : base(1158)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798003).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798003).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700003).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203128).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798003)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
        }
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 700003 && var == 0)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 1352);
                    case DialogAction.SELECT2_1:
                        return SendQuestDialog(env, 1353);
                    case DialogAction.SETPRO1:
                        if (!GiveQuestItem(env, 182200502, 1))
                            return true;
                        qs.SetQuestVarById(0, 1);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return CloseDialogWindow(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203128)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 2375);
                    case DialogAction.SELECT_QUEST_REWARD:
                        RemoveQuestItem(env, 182200502, 1);
                        return SendQuestDialog(env, 5);
                    default:
                        return SendQuestEndDialog(env);
                }
            }
            return false;
        }
        return false;
    }
}
