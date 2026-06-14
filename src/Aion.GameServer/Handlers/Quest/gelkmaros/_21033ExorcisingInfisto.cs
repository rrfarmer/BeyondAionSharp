using Aion.GameServer.Model;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author VladimirZ
/// </summary>
public class _21033ExorcisingInfisto : AbstractQuestHandler
{
    public _21033ExorcisingInfisto() : base(21033)
    {
    }

    public override void Register()
    {
        int[] npcs = { 799256, 204734 };
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799256).AddOnQuestStart(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        if (SendQuestNoneDialog(env, 799256, 182207829, 1))
            return true;

        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        if (qs.GetStatus() == QuestStatus.START)
        {
            if (env.GetTargetId() == 204734)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1352);
                        return false;
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1, true, false, 182207830, 1, 182207829, 1);
                }
            }
        }
        return SendQuestRewardDialog(env, 799256, 2375);
    }
}
