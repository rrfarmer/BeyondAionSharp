using Aion.GameServer.Model;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author VladimirZ
/// </summary>
public class _21004VillageStatusReport : AbstractQuestHandler
{
    public _21004VillageStatusReport() : base(21004)
    {
    }

    public override void Register()
    {
        int[] npcs = { 799227, 799268, 799269 };
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799227).AddOnQuestStart(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        if (SendQuestNoneDialog(env, 799227))
            return true;

        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        if (qs.GetStatus() == QuestStatus.START)
        {
            if (env.GetTargetId() == 799268)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1352);
                        return false;
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (env.GetTargetId() == 799269)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                            return SendQuestDialog(env, 1693);
                        return false;
                    case DialogAction.SETPRO2:
                        return DefaultCloseDialog(env, 1, 2, false, false);
                }
            }
            else if (env.GetTargetId() == 799227)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 2)
                            return SendQuestDialog(env, 2375);
                        return false;
                    case DialogAction.SELECT_QUEST_REWARD:
                        return DefaultCloseDialog(env, 2, 2, true, true);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (env.GetTargetId() == 799227)
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
