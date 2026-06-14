using Aion.GameServer.Model;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _28600SuspiciousErrand : AbstractQuestHandler
{
    public _28600SuspiciousErrand() : base(28600)
    {
    }

    public override void Register()
    {
        int[] npcs = { 204702, 205233, 204254, 804607 };
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204702).AddOnQuestStart(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        if (SendQuestNoneDialog(env, 204702, 182213004, 1))
            return true;
        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;
        int var = qs.GetQuestVarById(0);
        if (qs.GetStatus() == QuestStatus.START)
        {
            if (env.GetTargetId() == 205233)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1352);
                        else if (var == 2)
                            return SendQuestDialog(env, 2034);
                        return false;
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1);
                    case DialogAction.SETPRO3:
                        qs.SetQuestVarById(0, 3);
                        return DefaultCloseDialog(env, 3, 3, true, false, 182213005, 1, 182213004, 1);
                }
            }
            else if (env.GetTargetId() == 804607)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                            return SendQuestDialog(env, 1693);
                        return false;
                    case DialogAction.SETPRO2:
                        return DefaultCloseDialog(env, 1, 2, 182213004, 1);
                }
            }
        }
        return SendQuestRewardDialog(env, 204702, 2375);
    }
}
